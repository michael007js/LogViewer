using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogViewer.Models;

namespace LogViewer.Utils;

/// <summary>
/// 已存储日志记录的轻量只读引用，包含序列号、时间戳、键索引及文件偏移量，
/// 用于在 UI 虚拟列表中高效传递而不需要反序列化完整条目。
/// </summary>
/// <param name="SequenceId">全局递增序列号，从 1 开始。</param>
/// <param name="TimestampTicks">日志时间戳的 Ticks 值。</param>
/// <param name="ProcessId">进程 ID。</param>
/// <param name="ThreadId">线程 ID。</param>
/// <param name="DeviceKey">设备字符串的驻留键（intern key）。</param>
/// <param name="TagKey">标签字符串的驻留键。</param>
/// <param name="LevelCode">日志级别的字节编码（取首字符大写的 ASCII 值）。</param>
/// <param name="Offset">该条目在 JSONL 文件中的起始字节偏移。</param>
/// <param name="Length">该条目在 JSONL 文件中的字节长度。</param>
internal readonly record struct SystemLogRecordRef(
    long SequenceId,
    long TimestampTicks,
    int ProcessId,
    int ThreadId,
    int DeviceKey,
    int TagKey,
    byte LevelCode,
    long Offset,
    int Length);

/// <summary>
/// 系统日志会话级 JSONL 追加存储，提供热缓存（LRU + 固定条目）、
/// 文件持久化、设备范围查询、标签计数与关键字搜索等功能。
/// </summary>
internal sealed class SystemLogSessionStore : IDisposable
{
    /// <summary>记录已被逻辑删除的位标志。</summary>
    private const byte ClearedFlag = 0x01;

    /// <summary>热缓存最小条目容量。</summary>
    private const int MinHotCapacity = 256;

    /// <summary>热缓存最小字节预算（32 MB）。</summary>
    private const long MinHotBytes = 32L * 1024 * 1024;

    /// <summary>热缓存最大字节预算（128 MB）。</summary>
    private const long MaxHotBytes = 128L * 1024 * 1024;

    /// <summary>每写入 N 条记录后刷新写入流。</summary>
    private const int FlushEveryRecords = 64;

    /// <summary>文件并发读取的最大数。</summary>
    private const int ReadConcurrency = 2;

    /// <summary>
    /// 内部存储记录格式，包含文件偏移/长度用于按需反序列化。
    /// </summary>
    private struct StoredRecord
    {
        /// <summary>全局递增序列号。</summary>
        public long SequenceId;

        /// <summary>时间戳 Ticks。</summary>
        public long TimestampTicks;

        /// <summary>在 JSONL 文件中的起始字节偏移。</summary>
        public long Offset;

        /// <summary>在 JSONL 文件中的字节长度。</summary>
        public int Length;

        /// <summary>进程 ID。</summary>
        public int ProcessId;

        /// <summary>线程 ID。</summary>
        public int ThreadId;

        /// <summary>设备字符串的驻留键。</summary>
        public int DeviceKey;

        /// <summary>标签字符串的驻留键。</summary>
        public int TagKey;

        /// <summary>日志级别的字节编码。</summary>
        public byte LevelCode;

        /// <summary>标志位（如 <see cref="ClearedFlag"/>）。</summary>
        public byte Flags;
    }

    /// <summary>
    /// 热缓存节点，持有反序列化后的条目、预估字节大小及 LRU 链表节点。
    /// </summary>
    private sealed class HotEntryCache
    {
        /// <summary>
        /// 初始化热缓存节点。
        /// </summary>
        /// <param name="entry">反序列化后的日志条目。</param>
        /// <param name="estimatedBytes">该条目预估占用的内存字节数。</param>
        /// <param name="node">LRU 链表中对应的节点。</param>
        public HotEntryCache(SystemLogEntry entry, int estimatedBytes, LinkedListNode<long> node)
        {
            Entry = entry;
            EstimatedBytes = estimatedBytes;
            Node = node;
        }

        /// <summary>反序列化后的日志条目（可被替换以刷新内容）。</summary>
        public SystemLogEntry Entry { get; set; }

        /// <summary>该条目预估占用的内存字节数。</summary>
        public int EstimatedBytes { get; set; }

        /// <summary>LRU 链表节点引用（不可替换，用于 O(1) 移动）。</summary>
        public LinkedListNode<long> Node { get; }
    }

    /// <summary>JSON 序列化选项，启用大小写不敏感以兼容 Android 端 Gson 小写驼峰输出。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>主锁，保护所有可变状态的读写。</summary>
    private readonly object _gate = new();

    /// <summary>读取文件的独立锁，防止并发读取时文件指针冲突。</summary>
    private readonly object _readGate = new();

    /// <summary>会话文件根目录路径。</summary>
    private readonly string _sessionsRoot;

    /// <summary>所有已存储记录的有序列表（按 SequenceId 递增）。</summary>
    private readonly List<StoredRecord> _records = new();

    /// <summary>按设备键索引的记录索引列表，用于设备范围查询。</summary>
    private readonly Dictionary<int, List<int>> _recordIndicesByDeviceKey = new();

    /// <summary>设备字符串→驻留键的映射。</summary>
    private readonly Dictionary<string, int> _deviceKeysByValue = new(StringComparer.Ordinal);

    /// <summary>设备字符串值表，下标即驻留键。</summary>
    private readonly List<string> _deviceValues = new();

    /// <summary>标签字符串→驻留键的映射。</summary>
    private readonly Dictionary<string, int> _tagKeysByValue = new(StringComparer.Ordinal);

    /// <summary>标签字符串值表，下标即驻留键。</summary>
    private readonly List<string> _tagValues = new();

    /// <summary>按设备键分组的标签计数，用于设备范围标签列表。</summary>
    private readonly Dictionary<int, Dictionary<int, int>> _tagCountsByDeviceKey = new();

    /// <summary>全局标签计数，用于全量标签列表。</summary>
    private readonly Dictionary<int, int> _globalTagCounts = new();

    /// <summary>热缓存：序列号→反序列化条目及元数据。</summary>
    private readonly Dictionary<long, HotEntryCache> _hotEntries = new();

    /// <summary>热缓存 LRU 链表，头部最旧、尾部最新。</summary>
    private readonly LinkedList<long> _hotEntryLru = new();

    /// <summary>被固定的序列号集合，不会被热缓存淘汰。</summary>
    private readonly HashSet<long> _pinnedSequences = new();

    /// <summary>正在进行的异步加载任务，用于合并重复请求。</summary>
    private readonly Dictionary<long, Task<SystemLogEntry?>> _loadTasks = new();

    /// <summary>限制并发文件读取数的信号量。</summary>
    private readonly SemaphoreSlim _readSemaphore = new(ReadConcurrency, ReadConcurrency);

    /// <summary>JSONL 写入流（单写者）。</summary>
    private FileStream? _writeStream;

    /// <summary>JSONL 读取流（多读者通过 <see cref="_readGate"/> 串行化）。</summary>
    private FileStream? _readStream;

    /// <summary>当前会话目录路径。</summary>
    private string _sessionDirectory = string.Empty;

    /// <summary>热缓存条目容量上限。</summary>
    private int _hotCapacity;

    /// <summary>热缓存字节预算上限。</summary>
    private long _hotByteBudget;

    /// <summary>当前热缓存已使用的字节估算值。</summary>
    private long _hotEntryBytes;

    /// <summary>下一个待分配的序列号（从 1 开始递增）。</summary>
    private long _nextSequenceId;

    /// <summary>结构版本号，每次增删记录时递增，用于 UI 判断是否需要刷新。</summary>
    private long _structureVersion = 1;

    /// <summary>会话令牌，每次轮转时递增，用于使正在进行的异步加载失效。</summary>
    private long _sessionToken = 1;

    /// <summary>是否已释放。</summary>
    private bool _disposed;

    /// <summary>
    /// 初始化会话存储实例，创建会话根目录、清理旧会话并轮转到新会话。
    /// </summary>
    /// <param name="hotCapacity">期望的热缓存条目容量（会被规范化到最小值以上）。</param>
    public SystemLogSessionStore(int hotCapacity)
    {
        _hotCapacity = NormalizeHotCapacity(hotCapacity);
        _hotByteBudget = CalculateHotByteBudget(_hotCapacity);
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogViewer",
            "Sessions");

        // 缓存0号位预留为空字符串键，避免有效键为0时歧义
        _deviceValues.Add(string.Empty);
        _deviceKeysByValue[string.Empty] = 0;
        _tagValues.Add(string.Empty);
        _tagKeysByValue[string.Empty] = 0;

        Directory.CreateDirectory(_sessionsRoot);
        CleanupStaleSessions();
        RotateSessionCore();
    }

    /// <summary>
    /// 获取当前已分配的最大序列号（即已写入记录数）。
    /// </summary>
    public long LastSequenceId
    {
        get
        {
            lock (_gate)
            {
                return _nextSequenceId;
            }
        }
    }

    /// <summary>
    /// 获取当前结构版本号，UI 可据此判断是否需要刷新列表。
    /// </summary>
    public long StructureVersion
    {
        get
        {
            lock (_gate)
            {
                return _structureVersion;
            }
        }
    }

    /// <summary>
    /// 动态更新热缓存容量，超出新预算的条目将被淘汰。
    /// </summary>
    /// <param name="hotCapacity">新的热缓存条目容量。</param>
    public void UpdateHotCapacity(int hotCapacity)
    {
        lock (_gate)
        {
            _hotCapacity = NormalizeHotCapacity(hotCapacity);
            _hotByteBudget = CalculateHotByteBudget(_hotCapacity);
            TrimHotEntriesLocked();
        }
    }

    /// <summary>
    /// 追加一条日志记录到当前会话，写入 JSONL 文件并更新内存索引与热缓存。
    /// </summary>
    /// <param name="entry">待追加的日志条目（会被赋值序列号和时间戳）。</param>
    /// <returns>该记录的轻量引用 <see cref="SystemLogRecordRef"/>。</returns>
    public SystemLogRecordRef Append(SystemLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            ThrowIfDisposed();

            var stream = _writeStream ?? throw new ObjectDisposedException(nameof(SystemLogSessionStore));
            var sequenceId = ++_nextSequenceId;
            entry.SequenceId = sequenceId;
            entry.SourceDeviceId ??= entry.SourceDeviceSerial ?? string.Empty;
            if (entry.Timestamp == default)
            {
                entry.Timestamp = DateTime.Now;
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
            var offset = stream.Position;
            stream.Write(payload);
            // JSONL 格式：每条记录后追加换行符，便于流式逐行读取
            stream.WriteByte((byte)'\n');

            // 定期刷新写入流，平衡 I/O 性能与数据安全
            if ((sequenceId % FlushEveryRecords) == 0)
            {
                stream.Flush(false);
            }

            var deviceKey = InternDeviceKeyLocked(entry.SourceDeviceId);
            var tagKey = InternTagKeyLocked(entry.Tag);
            var record = new StoredRecord
            {
                SequenceId = sequenceId,
                TimestampTicks = entry.Timestamp.Ticks,
                Offset = offset,
                // Length = JSON字节数（不含换行符），用于反序列化时精确读取
                Length = payload.Length,
                ProcessId = entry.ProcessId,
                ThreadId = entry.ThreadId,
                DeviceKey = deviceKey,
                TagKey = tagKey,
                LevelCode = EncodeLevel(entry.Level)
            };

            var recordIndex = _records.Count;
            _records.Add(record);
            GetOrCreateDeviceRecordIndicesLocked(deviceKey).Add(recordIndex);
            IncrementTagCountLocked(deviceKey, tagKey);
            // 深拷贝条目放入热缓存，与写入流中的对象解耦，防止后续修改影响缓存
            RememberHotEntryLocked(CloneEntry(entry));

            return CreateRecordRef(record);
        }
    }

    /// <summary>
    /// 获取指定设备范围内的所有活跃记录引用（跳过已清除的）。
    /// </summary>
    /// <param name="deviceId">设备 ID，为 null 或空表示全量。</param>
    /// <param name="maxSequenceInclusive">最大序列号（含），默认不限制。</param>
    /// <returns>活跃记录引用列表。</returns>
    public IReadOnlyList<SystemLogRecordRef> CopyActiveRecords(string? deviceId,
        long maxSequenceInclusive = long.MaxValue)
    {
        lock (_gate)
        {
            return CopyActiveRecordsLocked(deviceId, 0, maxSequenceInclusive);
        }
    }

    /// <summary>
    /// 获取指定序列号之后、指定设备范围内的活跃记录引用。
    /// </summary>
    /// <param name="deviceId">设备 ID，为 null 或空表示全量。</param>
    /// <param name="afterSequenceExclusive">起始序列号（不含）。</param>
    /// <param name="maxSequenceInclusive">最大序列号（含），默认不限制。</param>
    /// <returns>活跃记录引用列表。</returns>
    public IReadOnlyList<SystemLogRecordRef> CopyActiveRecordsSince(
        string? deviceId,
        long afterSequenceExclusive,
        long maxSequenceInclusive = long.MaxValue)
    {
        lock (_gate)
        {
            return CopyActiveRecordsLocked(deviceId, afterSequenceExclusive, maxSequenceInclusive);
        }
    }

    /// <summary>
    /// 获取指定设备范围内、按计数降序排列的标签名数组（首项为 "All"）。
    /// 当 <paramref name="maxSequenceInclusive"/> 覆盖全部记录时直接从计数表构建，否则需扫描记录。
    /// </summary>
    /// <param name="deviceId">设备 ID，为 null 或空表示全量。</param>
    /// <param name="maxSequenceInclusive">最大序列号（含），默认不限制。</param>
    /// <returns>有序标签名数组。</returns>
    public string[] GetOrderedTags(string? deviceId, long maxSequenceInclusive = long.MaxValue)
    {
        lock (_gate)
        {
            if (maxSequenceInclusive >= _nextSequenceId)
            {
                return BuildOrderedTagsFromCountsLocked(deviceId);
            }

            var tagKeys = new HashSet<int>();
            var records = CopyActiveRecordsLocked(deviceId, 0, maxSequenceInclusive);
            foreach (var record in records)
            {
                tagKeys.Add(record.TagKey);
            }

            return BuildOrderedTagsLocked(tagKeys);
        }
    }

    /// <summary>
    /// 尝试从热缓存获取已反序列化的条目，命中时同时更新 LRU 顺序。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="entry">输出的日志条目（未命中时为 null）。</param>
    /// <returns>是否命中热缓存。</returns>
    public bool TryGetHotEntry(long sequenceId, out SystemLogEntry? entry)
    {
        lock (_gate)
        {
            if (_hotEntries.TryGetValue(sequenceId, out var cache))
            {
                TouchHotEntryLocked(cache);
                entry = cache.Entry;
                return true;
            }

            entry = null;
            return false;
        }
    }

    /// <summary>
    /// 获取用于显示的日志条目：优先返回热缓存中的完整条目，否则从引用构造外壳条目（Message 为空）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <returns>用于显示的日志条目。</returns>
    public SystemLogEntry GetDisplayEntry(SystemLogRecordRef record)
    {
        if (TryGetHotEntry(record.SequenceId, out var hotEntry) && hotEntry != null)
        {
            return hotEntry;
        }

        return CreateShellEntry(record);
    }

    /// <summary>
    /// 更新被固定的序列号集合，固定条目不会被热缓存淘汰。
    /// </summary>
    /// <param name="sequenceIds">需要固定的序列号集合。</param>
    public void UpdatePinnedSequences(IEnumerable<long> sequenceIds)
    {
        lock (_gate)
        {
            _pinnedSequences.Clear();
            foreach (var sequenceId in sequenceIds)
            {
                if (sequenceId > 0)
                {
                    _pinnedSequences.Add(sequenceId);
                }
            }

            TrimHotEntriesLocked();
        }
    }

    /// <summary>
    /// 批量预取指定序列号对应的日志条目到热缓存。
    /// </summary>
    /// <param name="sequenceIds">需要预取的序列号列表。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次新加载的条目数。</returns>
    public async Task<int> PrefetchAsync(IReadOnlyList<long> sequenceIds, CancellationToken cancellationToken)
    {
        if (sequenceIds.Count == 0)
        {
            return 0;
        }

        var loaded = 0;
        foreach (var sequenceId in sequenceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetHotEntry(sequenceId, out _))
            {
                continue;
            }

            var entry = await GetEntryAsync(sequenceId, cancellationToken).ConfigureAwait(false);
            if (entry != null)
            {
                loaded++;
            }
        }

        return loaded;
    }

    /// <summary>
    /// 异步获取指定序列号的完整日志条目，优先命中热缓存，否则从文件读取并缓存。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>反序列化后的日志条目，不存在则返回 null。</returns>
    public async Task<SystemLogEntry?> GetEntryAsync(long sequenceId, CancellationToken cancellationToken)
    {
        Task<SystemLogEntry?> loadTask;

        lock (_gate)
        {
            if (TryGetHotEntryLocked(sequenceId, out var hotEntry))
            {
                return hotEntry;
            }

            if (!TryGetStoredRecordLocked(sequenceId, out _))
            {
                return null;
            }

            loadTask = GetOrCreateLoadTaskLocked(sequenceId);
        }

        return await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 判断记录是否属于指定设备。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="deviceId">设备 ID，为 null 或空表示匹配所有。</param>
    /// <returns>是否匹配。</returns>
    public bool MatchesScope(SystemLogRecordRef record, string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return true;
        }

        lock (_gate)
        {
            return string.Equals(GetDeviceValueLocked(record.DeviceKey), deviceId, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 判断记录的日志级别是否与指定级别匹配（不区分大小写）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="level">级别字符串，为 null 或空表示匹配所有。</param>
    /// <returns>是否匹配。</returns>
    public bool MatchesLevel(SystemLogRecordRef record, string? level)
    {
        return string.IsNullOrEmpty(level) ||
               string.Equals(DecodeLevel(record.LevelCode), level, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断记录的标签是否与指定标签匹配（不区分大小写）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="tag">标签字符串，为 null 或空表示匹配所有。</param>
    /// <returns>是否匹配。</returns>
    public bool MatchesTag(SystemLogRecordRef record, string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return true;
        }

        lock (_gate)
        {
            return string.Equals(GetTagValueLocked(record.TagKey), tag, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 获取记录引用对应的标签文本。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <returns>标签字符串。</returns>
    public string GetTagText(SystemLogRecordRef record)
    {
        lock (_gate)
        {
            return GetTagValueLocked(record.TagKey);
        }
    }

    /// <summary>
    /// 异步判断记录是否包含指定关键字（搜索标签、级别、PID/TID、时间及消息内容）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="keyword">关键字，为 null 或空表示匹配所有。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否匹配关键字。</returns>
    public async Task<bool> MatchesKeywordAsync(SystemLogRecordRef record, string keyword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return true;
        }

        string tagText;
        SystemLogEntry? hotEntry;

        lock (_gate)
        {
            tagText = GetTagValueLocked(record.TagKey);
            hotEntry = TryGetHotEntryLocked(record.SequenceId, out var loadedEntry) ? loadedEntry : null;
        }

        if (tagText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (DecodeLevel(record.LevelCode).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            record.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            record.ThreadId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            new DateTime(record.TimestampTicks).ToString("HH:mm:ss.fff")
                .Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (hotEntry?.Message?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var coldEntry = await ReadEntryForScanAsync(record, cancellationToken).ConfigureAwait(false);
        return coldEntry?.Message?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// 异步判断记录是否匹配指定正则表达式（搜索标签、级别、PID/TID、时间及消息内容）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="keyword">正则表达式模式，为 null 或空表示匹配所有。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="regex">已编译的正则表达式实例。</param>
    /// <returns>是否匹配关键字。</returns>
    public async Task<bool> MatchesKeywordAsync(SystemLogRecordRef record, string keyword,
        CancellationToken cancellationToken, Regex regex)
    {
        if (string.IsNullOrEmpty(keyword) || regex == null)
        {
            return true;
        }

        string tagText;
        SystemLogEntry? hotEntry;

        lock (_gate)
        {
            tagText = GetTagValueLocked(record.TagKey);
            hotEntry = TryGetHotEntryLocked(record.SequenceId, out var loadedEntry) ? loadedEntry : null;
        }

        if (regex.IsMatch(tagText))
        {
            return true;
        }

        if (regex.IsMatch(DecodeLevel(record.LevelCode)) ||
            regex.IsMatch(record.ProcessId.ToString()) ||
            regex.IsMatch(record.ThreadId.ToString()) ||
            regex.IsMatch(new DateTime(record.TimestampTicks).ToString("HH:mm:ss.fff")))
        {
            return true;
        }

        if (hotEntry?.Message != null && regex.IsMatch(hotEntry.Message))
        {
            return true;
        }

        var coldEntry = await ReadEntryForScanAsync(record, cancellationToken).ConfigureAwait(false);
        return coldEntry?.Message != null && regex.IsMatch(coldEntry.Message);
    }

    /// <summary>
    /// 逻辑删除指定设备的所有记录（设置 ClearedFlag、减少标签计数、移除热缓存）。
    /// </summary>
    /// <param name="deviceId">待清除的设备 ID。</param>
    public void ClearDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        lock (_gate)
        {
            if (!TryGetDeviceKeyLocked(deviceId, out var deviceKey) ||
                !_recordIndicesByDeviceKey.TryGetValue(deviceKey, out var recordIndices) ||
                recordIndices.Count == 0)
            {
                return;
            }

            foreach (var recordIndex in recordIndices)
            {
                var record = _records[recordIndex];
                if ((record.Flags & ClearedFlag) != 0)
                {
                    continue;
                }

                record.Flags |= ClearedFlag;
                _records[recordIndex] = record;
                DecrementTagCountLocked(record.DeviceKey, record.TagKey);
                RemoveHotEntryLocked(record.SequenceId);
            }

            _structureVersion++;
        }
    }

    /// <summary>
    /// 将旧设备的所有记录重新映射到新设备，更新记录索引、标签计数和热缓存中的设备 ID。
    /// </summary>
    /// <param name="oldDeviceId">原设备 ID。</param>
    /// <param name="newDeviceId">新设备 ID。</param>
    public void RemapDevice(string oldDeviceId, string newDeviceId)
    {
        if (string.IsNullOrWhiteSpace(oldDeviceId) ||
            string.IsNullOrWhiteSpace(newDeviceId) ||
            string.Equals(oldDeviceId, newDeviceId, StringComparison.Ordinal))
        {
            return;
        }

        lock (_gate)
        {
            if (!TryGetDeviceKeyLocked(oldDeviceId, out var oldDeviceKey) ||
                !_recordIndicesByDeviceKey.TryGetValue(oldDeviceKey, out var recordIndices) ||
                recordIndices.Count == 0)
            {
                return;
            }

            var newDeviceKey = InternDeviceKeyLocked(newDeviceId);
            if (oldDeviceKey == newDeviceKey)
            {
                return;
            }

            var targetIndices = GetOrCreateDeviceRecordIndicesLocked(newDeviceKey);
            targetIndices.AddRange(recordIndices);

            foreach (var recordIndex in recordIndices)
            {
                var record = _records[recordIndex];
                record.DeviceKey = newDeviceKey;
                _records[recordIndex] = record;

                if (_hotEntries.TryGetValue(record.SequenceId, out var cache))
                {
                    cache.Entry.SourceDeviceId = newDeviceId;
                    TouchHotEntryLocked(cache);
                }
            }

            if (_tagCountsByDeviceKey.TryGetValue(oldDeviceKey, out var oldTagCounts))
            {
                if (!_tagCountsByDeviceKey.TryGetValue(newDeviceKey, out var newTagCounts))
                {
                    newTagCounts = new Dictionary<int, int>();
                    _tagCountsByDeviceKey[newDeviceKey] = newTagCounts;
                }

                foreach (var pair in oldTagCounts)
                {
                    newTagCounts[pair.Key] = newTagCounts.TryGetValue(pair.Key, out var count)
                        ? count + pair.Value
                        : pair.Value;
                }

                _tagCountsByDeviceKey.Remove(oldDeviceKey);
            }

            _recordIndicesByDeviceKey.Remove(oldDeviceKey);
            _structureVersion++;
        }
    }

    /// <summary>
    /// 轮转到新的会话（创建新 JSONL 文件），旧会话目录在后台异步删除。
    /// </summary>
    public void RotateSession()
    {
        string? oldDirectory;

        lock (_gate)
        {
            ThrowIfDisposed();
            oldDirectory = _sessionDirectory;
            RotateSessionCore();
            _structureVersion++;
        }

        if (!string.IsNullOrWhiteSpace(oldDirectory))
        {
            _ = Task.Run(() => TryDeleteDirectory(oldDirectory));
        }
    }

    /// <summary>
    /// 释放所有资源（关闭读写流、释放信号量、后台删除会话目录）。
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sessionToken++;
            try
            {
                _writeStream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _readStream?.Dispose();
            }
            catch
            {
            }

            _writeStream = null;
            _readStream = null;
        }

        _readSemaphore.Dispose();

        if (!string.IsNullOrWhiteSpace(_sessionDirectory))
        {
            _ = Task.Run(() => TryDeleteDirectory(_sessionDirectory));
        }
    }

    /// <summary>
    /// 在锁内拷贝指定设备和序列号范围内的活跃记录引用，跳过已清除的记录。
    /// </summary>
    /// <param name="deviceId">设备 ID，为 null 或空表示全量。</param>
    /// <param name="afterSequenceExclusive">起始序列号（不含）。</param>
    /// <param name="maxSequenceInclusive">最大序列号（含）。</param>
    /// <returns>活跃记录引用列表。</returns>
    private IReadOnlyList<SystemLogRecordRef> CopyActiveRecordsLocked(
        string? deviceId,
        long afterSequenceExclusive,
        long maxSequenceInclusive)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            if (!TryGetDeviceKeyLocked(deviceId, out var deviceKey) ||
                !_recordIndicesByDeviceKey.TryGetValue(deviceKey, out var deviceRecordIndices) ||
                deviceRecordIndices.Count == 0)
            {
                return Array.Empty<SystemLogRecordRef>();
            }

            var startIndex = FindFirstDeviceRecordIndex(deviceRecordIndices, afterSequenceExclusive);
            if (startIndex >= deviceRecordIndices.Count)
            {
                return Array.Empty<SystemLogRecordRef>();
            }

            var records = new List<SystemLogRecordRef>(Math.Max(16, deviceRecordIndices.Count - startIndex));
            for (var i = startIndex; i < deviceRecordIndices.Count; i++)
            {
                var record = _records[deviceRecordIndices[i]];
                if (record.SequenceId > maxSequenceInclusive)
                {
                    break;
                }

                if ((record.Flags & ClearedFlag) != 0)
                {
                    continue;
                }

                records.Add(CreateRecordRef(record));
            }

            return records;
        }

        var allStartIndex = FindFirstRecordIndex(afterSequenceExclusive);
        if (allStartIndex >= _records.Count)
        {
            return Array.Empty<SystemLogRecordRef>();
        }

        var allRecords = new List<SystemLogRecordRef>(Math.Max(16, _records.Count - allStartIndex));
        for (var i = allStartIndex; i < _records.Count; i++)
        {
            var record = _records[i];
            if (record.SequenceId > maxSequenceInclusive)
            {
                break;
            }

            if ((record.Flags & ClearedFlag) != 0)
            {
                continue;
            }

            allRecords.Add(CreateRecordRef(record));
        }

        return allRecords;
    }

    /// <summary>
    /// 在锁内从标签计数表构建有序标签数组（首项为 "All"），适用于全量记录场景。
    /// </summary>
    /// <param name="deviceId">设备 ID，为 null 或空表示全量。</param>
    /// <returns>有序标签名数组。</returns>
    private string[] BuildOrderedTagsFromCountsLocked(string? deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            if (!TryGetDeviceKeyLocked(deviceId, out var deviceKey) ||
                !_tagCountsByDeviceKey.TryGetValue(deviceKey, out var counts) ||
                counts.Count == 0)
            {
                return new[] { "All" };
            }

            return BuildOrderedTagsLocked(counts.Keys);
        }

        return BuildOrderedTagsLocked(_globalTagCounts.Keys);
    }

    /// <summary>
    /// 从给定标签键集合构建按字母升序排列的标签名数组，首项为 "All"。
    /// </summary>
    /// <param name="tagKeys">标签键集合。</param>
    /// <returns>有序标签名数组。</returns>
    private string[] BuildOrderedTagsLocked(IEnumerable<int> tagKeys)
    {
        var ordered = tagKeys
            .Select(GetTagValueLocked)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ordered.Insert(0, "All");
        return ordered.ToArray();
    }

    /// <summary>
    /// 在锁内获取或创建指定序列号的异步加载任务（合并重复请求）。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <returns>异步加载任务。</returns>
    private Task<SystemLogEntry?> GetOrCreateLoadTaskLocked(long sequenceId)
    {
        if (_loadTasks.TryGetValue(sequenceId, out var existingTask))
        {
            return existingTask;
        }

        var sessionToken = _sessionToken;
        var task = LoadAndCacheEntryAsync(sequenceId, sessionToken);
        _loadTasks[sequenceId] = task;
        return task;
    }

    /// <summary>
    /// 异步从文件读取条目并放入热缓存，受读取信号量限制并发数。
    /// 会话令牌不匹配时返回 null 以避免写入过期缓存。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="sessionToken">发起加载时的会话令牌，用于验证加载结果是否仍有效。</param>
    /// <returns>反序列化后的日志条目，失败或过期则返回 null。</returns>
    private async Task<SystemLogEntry?> LoadAndCacheEntryAsync(long sequenceId, long sessionToken)
    {
        try
        {
            await _readSemaphore.WaitAsync().ConfigureAwait(false);

            SystemLogRecordRef recordRef;
            lock (_gate)
            {
                // sessionToken 不匹配意味着会话已轮转，加载结果已过期
                if (_disposed || _sessionToken != sessionToken || !TryGetRecordRefLocked(sequenceId, out recordRef))
                {
                    return null;
                }
            }

            var entry = await ReadEntryCoreAsync(recordRef, CancellationToken.None).ConfigureAwait(false);
            if (entry == null)
            {
                return null;
            }

            lock (_gate)
            {
                // 二次校验：确认加载期间会话未轮转、记录仍有效
                if (_disposed || _sessionToken != sessionToken ||
                    !TryGetRecordRefLocked(sequenceId, out var currentRecord))
                {
                    return null;
                }

                entry.SequenceId = currentRecord.SequenceId;
                entry.SourceDeviceId = GetDeviceValueLocked(currentRecord.DeviceKey);
                RememberHotEntryLocked(entry);
                return entry;
            }
        }
        finally
        {
            _readSemaphore.Release();
            lock (_gate)
            {
                _loadTasks.Remove(sequenceId);
            }
        }
    }

    /// <summary>
    /// 异步从文件读取条目用于关键字扫描（不放入热缓存）。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>反序列化后的日志条目。</returns>
    private async Task<SystemLogEntry?> ReadEntryForScanAsync(SystemLogRecordRef record,
        CancellationToken cancellationToken)
    {
        return await ReadEntryCoreAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 从 JSONL 文件读取指定偏移和长度的字节并反序列化为日志条目，使用 ArrayPool 复用缓冲区。
    /// </summary>
    /// <param name="record">记录引用（包含 Offset 和 Length）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>反序列化后的日志条目，读取失败则返回 null。</returns>
    private async Task<SystemLogEntry?> ReadEntryCoreAsync(SystemLogRecordRef record,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(record.Length);
        try
        {
            var bytesRead = 0;
            lock (_readGate)
            {
                var stream = _readStream;
                if (stream == null)
                {
                    return null;
                }

                stream.Position = record.Offset;
                while (bytesRead < record.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var chunk = stream.Read(buffer, bytesRead, record.Length - bytesRead);
                    if (chunk <= 0)
                    {
                        return null;
                    }

                    bytesRead += chunk;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                var entry = JsonSerializer.Deserialize<SystemLogEntry>(buffer.AsSpan(0, record.Length), JsonOptions);
                if (entry == null)
                {
                    return null;
                }

                entry.SequenceId = record.SequenceId;
                entry.Timestamp = new DateTime(record.TimestampTicks);
                entry.ProcessId = record.ProcessId;
                entry.ThreadId = record.ThreadId;
                entry.Tag ??= string.Empty;
                entry.Level ??= DecodeLevel(record.LevelCode);
                return entry;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 从记录引用构造一个外壳条目（Message 为空），用于热缓存未命中时的快速显示。
    /// </summary>
    /// <param name="record">记录引用。</param>
    /// <returns>外壳日志条目。</returns>
    private SystemLogEntry CreateShellEntry(SystemLogRecordRef record)
    {
        string deviceId;
        string tag;

        lock (_gate)
        {
            deviceId = GetDeviceValueLocked(record.DeviceKey);
            tag = GetTagValueLocked(record.TagKey);
        }

        return new SystemLogEntry
        {
            SequenceId = record.SequenceId,
            Timestamp = new DateTime(record.TimestampTicks),
            ProcessId = record.ProcessId,
            ThreadId = record.ThreadId,
            Level = DecodeLevel(record.LevelCode),
            Tag = tag,
            Message = string.Empty,
            SourceDeviceId = deviceId
        };
    }

    /// <summary>
    /// 执行会话轮转：递增会话令牌、关闭旧流、清空所有内存索引、创建新目录和 JSONL 文件。
    /// </summary>
    private void RotateSessionCore()
    {
        // 递增会话令牌使正在进行的异步加载任务失效
        _sessionToken++;

        try
        {
            _writeStream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _readStream?.Dispose();
        }
        catch
        {
        }

        _records.Clear();
        _recordIndicesByDeviceKey.Clear();
        _tagCountsByDeviceKey.Clear();
        _globalTagCounts.Clear();
        _hotEntries.Clear();
        _hotEntryLru.Clear();
        _pinnedSequences.Clear();
        _loadTasks.Clear();
        _hotEntryBytes = 0;
        _nextSequenceId = 0;

        // 会话目录名包含时间戳和 GUID，确保唯一性
        _sessionDirectory =
            Path.Combine(_sessionsRoot, $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sessionDirectory);

        var filePath = Path.Combine(_sessionDirectory, "systemlogs.jsonl");
        // 写入流：追加模式、顺序扫描优化；读取流：共享读取、随机访问优化
        _writeStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096,
            FileOptions.SequentialScan);
        _readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096,
            FileOptions.RandomAccess);
    }

    /// <summary>
    /// 清理会话根目录下的所有旧会话目录。
    /// </summary>
    private void CleanupStaleSessions()
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_sessionsRoot))
            {
                TryDeleteDirectory(directory);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 尝试递归删除指定目录，失败时静默忽略。
    /// </summary>
    /// <param name="directory">目录路径。</param>
    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 在锁内将条目放入热缓存或更新已有条目，然后根据容量/字节预算淘汰旧条目。
    /// </summary>
    /// <param name="entry">日志条目。</param>
    private void RememberHotEntryLocked(SystemLogEntry entry)
    {
        var estimatedBytes = EstimateEntrySize(entry);
        if (_hotEntries.TryGetValue(entry.SequenceId, out var cache))
        {
            _hotEntryBytes -= cache.EstimatedBytes;
            cache.Entry = entry;
            cache.EstimatedBytes = estimatedBytes;
            TouchHotEntryLocked(cache);
            _hotEntryBytes += estimatedBytes;
        }
        else
        {
            var node = _hotEntryLru.AddLast(entry.SequenceId);
            _hotEntries[entry.SequenceId] = new HotEntryCache(entry, estimatedBytes, node);
            _hotEntryBytes += estimatedBytes;
        }

        TrimHotEntriesLocked();
    }

    /// <summary>
    /// 在锁内将热缓存节点移动到 LRU 链表尾部（标记为最近使用）。
    /// </summary>
    /// <param name="cache">热缓存节点。</param>
    private void TouchHotEntryLocked(HotEntryCache cache)
    {
        if (cache.Node.List == null || cache.Node.List.Last == cache.Node)
        {
            return;
        }

        _hotEntryLru.Remove(cache.Node);
        _hotEntryLru.AddLast(cache.Node);
    }

    /// <summary>
    /// 在锁内从热缓存移除指定序列号的条目并更新字节估算。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    private void RemoveHotEntryLocked(long sequenceId)
    {
        if (_hotEntries.TryGetValue(sequenceId, out var cache))
        {
            _hotEntryBytes -= cache.EstimatedBytes;
            _hotEntryLru.Remove(cache.Node);
            _hotEntries.Remove(sequenceId);
        }
    }

    /// <summary>
    /// 在锁内淘汰热缓存中最旧的条目，直到条目数和字节预算均低于上限，固定条目不被淘汰。
    /// </summary>
    private void TrimHotEntriesLocked()
    {
        var node = _hotEntryLru.First;
        while ((_hotEntries.Count > _hotCapacity || _hotEntryBytes > _hotByteBudget) && node != null)
        {
            var next = node.Next;
            var sequenceId = node.Value;
            if (_pinnedSequences.Contains(sequenceId))
            {
                node = next;
                continue;
            }

            RemoveHotEntryLocked(sequenceId);
            node = next;
        }
    }

    /// <summary>
    /// 在锁内尝试从热缓存获取条目并更新 LRU 顺序。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="entry">输出的日志条目。</param>
    /// <returns>是否命中。</returns>
    private bool TryGetHotEntryLocked(long sequenceId, out SystemLogEntry? entry)
    {
        if (_hotEntries.TryGetValue(sequenceId, out var cache))
        {
            TouchHotEntryLocked(cache);
            entry = cache.Entry;
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// 在锁内按序列号查找存储记录（利用序列号与列表下标的 1-offset 关系），
    /// 同时验证记录未被清除且序列号一致。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="record">输出的存储记录。</param>
    /// <returns>是否找到且记录有效。</returns>
    private bool TryGetStoredRecordLocked(long sequenceId, out StoredRecord record)
    {
        // 序列号从1开始，下标 = sequenceId - 1
        var index = checked((int)(sequenceId - 1));
        if (index < 0 || index >= _records.Count)
        {
            record = default;
            return false;
        }

        record = _records[index];
        // 验证记录未被逻辑删除且序列号一致（防止轮转后下标错位）
        return (record.Flags & ClearedFlag) == 0 && record.SequenceId == sequenceId;
    }

    /// <summary>
    /// 在锁内尝试获取指定序列号的记录引用。
    /// </summary>
    /// <param name="sequenceId">序列号。</param>
    /// <param name="recordRef">输出的记录引用。</param>
    /// <returns>是否成功。</returns>
    private bool TryGetRecordRefLocked(long sequenceId, out SystemLogRecordRef recordRef)
    {
        if (TryGetStoredRecordLocked(sequenceId, out var record))
        {
            recordRef = CreateRecordRef(record);
            return true;
        }

        recordRef = default;
        return false;
    }

    /// <summary>
    /// 在锁内驻留设备字符串，返回或分配对应的整数键。
    /// </summary>
    /// <param name="value">设备 ID 字符串。</param>
    /// <returns>驻留键。</returns>
    private int InternDeviceKeyLocked(string? value)
    {
        return InternStringKeyLocked(value, _deviceKeysByValue, _deviceValues);
    }

    /// <summary>
    /// 在锁内驻留标签字符串，返回或分配对应的整数键。
    /// </summary>
    /// <param name="value">标签字符串。</param>
    /// <returns>驻留键。</returns>
    private int InternTagKeyLocked(string? value)
    {
        return InternStringKeyLocked(value, _tagKeysByValue, _tagValues);
    }

    /// <summary>
    /// 通用字符串驻留逻辑：查找已有键或分配新键，维护值表和反向映射。
    /// </summary>
    /// <param name="value">待驻留的字符串。</param>
    /// <param name="keysByValue">字符串→键的映射表。</param>
    /// <param name="values">键→字符串的值表。</param>
    /// <returns>驻留键。</returns>
    private static int InternStringKeyLocked(string? value, Dictionary<string, int> keysByValue, List<string> values)
    {
        var normalized = value ?? string.Empty;
        if (keysByValue.TryGetValue(normalized, out var key))
        {
            return key;
        }

        key = values.Count;
        values.Add(normalized);
        keysByValue[normalized] = key;
        return key;
    }

    /// <summary>
    /// 在锁内尝试查找设备 ID 对应的驻留键。
    /// </summary>
    /// <param name="deviceId">设备 ID。</param>
    /// <param name="deviceKey">输出的驻留键。</param>
    /// <returns>是否找到。</returns>
    private bool TryGetDeviceKeyLocked(string deviceId, out int deviceKey)
    {
        return _deviceKeysByValue.TryGetValue(deviceId, out deviceKey);
    }

    /// <summary>
    /// 在锁内根据驻留键获取设备字符串。
    /// </summary>
    /// <param name="deviceKey">驻留键。</param>
    /// <returns>设备 ID 字符串，键无效时返回空字符串。</returns>
    private string GetDeviceValueLocked(int deviceKey)
    {
        return deviceKey >= 0 && deviceKey < _deviceValues.Count
            ? _deviceValues[deviceKey]
            : string.Empty;
    }

    /// <summary>
    /// 在锁内根据驻留键获取标签字符串。
    /// </summary>
    /// <param name="tagKey">驻留键。</param>
    /// <returns>标签字符串，键无效时返回空字符串。</returns>
    private string GetTagValueLocked(int tagKey)
    {
        return tagKey >= 0 && tagKey < _tagValues.Count
            ? _tagValues[tagKey]
            : string.Empty;
    }

    /// <summary>
    /// 在锁内获取或创建指定设备键的记录索引列表。
    /// </summary>
    /// <param name="deviceKey">设备驻留键。</param>
    /// <returns>该设备的记录索引列表。</returns>
    private List<int> GetOrCreateDeviceRecordIndicesLocked(int deviceKey)
    {
        if (!_recordIndicesByDeviceKey.TryGetValue(deviceKey, out var indices))
        {
            indices = new List<int>();
            _recordIndicesByDeviceKey[deviceKey] = indices;
        }

        return indices;
    }

    /// <summary>
    /// 在锁内递增指定设备和标签的计数（全局 + 设备范围），标签键为 0 时跳过。
    /// </summary>
    /// <param name="deviceKey">设备驻留键。</param>
    /// <param name="tagKey">标签驻留键。</param>
    private void IncrementTagCountLocked(int deviceKey, int tagKey)
    {
        if (tagKey == 0)
        {
            return;
        }

        _globalTagCounts[tagKey] = _globalTagCounts.TryGetValue(tagKey, out var globalCount)
            ? globalCount + 1
            : 1;

        if (!_tagCountsByDeviceKey.TryGetValue(deviceKey, out var deviceCounts))
        {
            deviceCounts = new Dictionary<int, int>();
            _tagCountsByDeviceKey[deviceKey] = deviceCounts;
        }

        deviceCounts[tagKey] = deviceCounts.TryGetValue(tagKey, out var deviceCount)
            ? deviceCount + 1
            : 1;
    }

    /// <summary>
    /// 在锁内递减指定设备和标签的计数，计数归零时移除条目，标签键为 0 时跳过。
    /// </summary>
    /// <param name="deviceKey">设备驻留键。</param>
    /// <param name="tagKey">标签驻留键。</param>
    private void DecrementTagCountLocked(int deviceKey, int tagKey)
    {
        if (tagKey == 0)
        {
            return;
        }

        if (_globalTagCounts.TryGetValue(tagKey, out var globalCount))
        {
            if (globalCount <= 1)
            {
                _globalTagCounts.Remove(tagKey);
            }
            else
            {
                _globalTagCounts[tagKey] = globalCount - 1;
            }
        }

        if (_tagCountsByDeviceKey.TryGetValue(deviceKey, out var deviceCounts) &&
            deviceCounts.TryGetValue(tagKey, out var deviceCount))
        {
            if (deviceCount <= 1)
            {
                deviceCounts.Remove(tagKey);
            }
            else
            {
                deviceCounts[tagKey] = deviceCount - 1;
            }

            if (deviceCounts.Count == 0)
            {
                _tagCountsByDeviceKey.Remove(deviceKey);
            }
        }
    }

    /// <summary>
    /// 在全部记录中二分查找第一个序列号大于指定值的记录索引。
    /// </summary>
    /// <param name="afterSequenceExclusive">起始序列号（不含）。</param>
    /// <returns>第一个满足条件的索引，无匹配时返回记录总数。</returns>
    private int FindFirstRecordIndex(long afterSequenceExclusive)
    {
        var low = 0;
        var high = _records.Count - 1;
        var result = _records.Count;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (_records[mid].SequenceId > afterSequenceExclusive)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 在指定设备的记录索引列表中二分查找第一个序列号大于指定值的索引位置。
    /// </summary>
    /// <param name="recordIndices">设备的记录索引列表。</param>
    /// <param name="afterSequenceExclusive">起始序列号（不含）。</param>
    /// <returns>第一个满足条件的索引位置，无匹配时返回列表长度。</returns>
    private int FindFirstDeviceRecordIndex(IReadOnlyList<int> recordIndices, long afterSequenceExclusive)
    {
        var low = 0;
        var high = recordIndices.Count - 1;
        var result = recordIndices.Count;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (_records[recordIndices[mid]].SequenceId > afterSequenceExclusive)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    /// <summary>
    /// 将内部存储记录转换为轻量引用结构。
    /// </summary>
    /// <param name="record">存储记录。</param>
    /// <returns>记录引用。</returns>
    private static SystemLogRecordRef CreateRecordRef(StoredRecord record)
    {
        return new SystemLogRecordRef(
            record.SequenceId,
            record.TimestampTicks,
            record.ProcessId,
            record.ThreadId,
            record.DeviceKey,
            record.TagKey,
            record.LevelCode,
            record.Offset,
            record.Length);
    }

    /// <summary>
    /// 规范化热缓存容量到最小值以上。
    /// </summary>
    /// <param name="hotCapacity">原始容量。</param>
    /// <returns>规范化后的容量。</returns>
    private static int NormalizeHotCapacity(int hotCapacity)
    {
        return Math.Max(MinHotCapacity, hotCapacity);
    }

    /// <summary>
    /// 根据热缓存容量计算字节预算：容量×4096，限定在 [MinHotBytes, MaxHotBytes] 范围内。
    /// </summary>
    /// <param name="hotCapacity">热缓存条目容量。</param>
    /// <returns>字节预算上限。</returns>
    private static long CalculateHotByteBudget(int hotCapacity)
    {
        var requested = (long)NormalizeHotCapacity(hotCapacity) * 4096L;
        return Math.Max(MinHotBytes, Math.Min(MaxHotBytes, requested));
    }

    /// <summary>
    /// 估算一条日志条目占用的内存大小（基础 256 字节 + 各字符串字段长度×2）。
    /// </summary>
    /// <param name="entry">日志条目。</param>
    /// <returns>预估字节数。</returns>
    private static int EstimateEntrySize(SystemLogEntry entry)
    {
        return 256 +
               GetStringBytes(entry.PackageName) +
               GetStringBytes(entry.Level) +
               GetStringBytes(entry.Tag) +
               GetStringBytes(entry.Message) +
               GetStringBytes(entry.SourceDeviceSerial) +
               GetStringBytes(entry.SourceDeviceId);
    }

    /// <summary>
    /// 估算字符串占用的内存字节数（长度×sizeof(char)），空字符串返回 0。
    /// </summary>
    /// <param name="value">字符串。</param>
    /// <returns>预估字节数。</returns>
    private static int GetStringBytes(string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : value.Length * sizeof(char);
    }

    /// <summary>
    /// 将日志级别字符串编码为字节（取首字符的大写 ASCII 值），空或空白返回 0。
    /// </summary>
    /// <param name="level">级别字符串（如 "V"/"D"/"I"/"W"/"E"）。</param>
    /// <returns>级别编码字节。</returns>
    private static byte EncodeLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return 0;
        }

        return (byte)char.ToUpperInvariant(level[0]);
    }

    /// <summary>
    /// 将级别编码字节解码为字符串（单字符），编码为 0 时返回空字符串。
    /// </summary>
    /// <param name="levelCode">级别编码字节。</param>
    /// <returns>级别字符串。</returns>
    private static string DecodeLevel(byte levelCode)
    {
        return levelCode == 0 ? string.Empty : ((char)levelCode).ToString();
    }

    /// <summary>
    /// 深拷贝日志条目，确保热缓存中的条目与写入流中的对象解耦。
    /// </summary>
    /// <param name="entry">原始条目。</param>
    /// <returns>拷贝后的新条目。</returns>
    private static SystemLogEntry CloneEntry(SystemLogEntry entry)
    {
        return new SystemLogEntry
        {
            SequenceId = entry.SequenceId,
            Timestamp = entry.Timestamp,
            ProcessId = entry.ProcessId,
            ThreadId = entry.ThreadId,
            PackageName = entry.PackageName,
            Level = entry.Level,
            Tag = entry.Tag,
            Message = entry.Message,
            SourceDeviceSerial = entry.SourceDeviceSerial,
            SourceDeviceId = entry.SourceDeviceId
        };
    }

    /// <summary>
    /// 若实例已被释放则抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SystemLogSessionStore));
        }
    }
}