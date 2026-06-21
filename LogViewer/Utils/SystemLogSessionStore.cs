using System.Buffers;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Utils;

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

internal sealed class SystemLogSessionStore : IDisposable
{
    private const byte ClearedFlag = 0x01;
    private const int MinHotCapacity = 256;
    private const long MinHotBytes = 32L * 1024 * 1024;
    private const long MaxHotBytes = 128L * 1024 * 1024;
    private const int FlushEveryRecords = 64;
    private const int ReadConcurrency = 2;

    private struct StoredRecord
    {
        public long SequenceId;
        public long TimestampTicks;
        public long Offset;
        public int Length;
        public int ProcessId;
        public int ThreadId;
        public int DeviceKey;
        public int TagKey;
        public byte LevelCode;
        public byte Flags;
    }

    private sealed class HotEntryCache
    {
        public HotEntryCache(SystemLogEntry entry, int estimatedBytes, LinkedListNode<long> node)
        {
            Entry = entry;
            EstimatedBytes = estimatedBytes;
            Node = node;
        }

        public SystemLogEntry Entry { get; set; }
        public int EstimatedBytes { get; set; }
        public LinkedListNode<long> Node { get; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly object _readGate = new();
    private readonly string _sessionsRoot;
    private readonly List<StoredRecord> _records = new();
    private readonly Dictionary<int, List<int>> _recordIndicesByDeviceKey = new();
    private readonly Dictionary<string, int> _deviceKeysByValue = new(StringComparer.Ordinal);
    private readonly List<string> _deviceValues = new();
    private readonly Dictionary<string, int> _tagKeysByValue = new(StringComparer.Ordinal);
    private readonly List<string> _tagValues = new();
    private readonly Dictionary<int, Dictionary<int, int>> _tagCountsByDeviceKey = new();
    private readonly Dictionary<int, int> _globalTagCounts = new();
    private readonly Dictionary<long, HotEntryCache> _hotEntries = new();
    private readonly LinkedList<long> _hotEntryLru = new();
    private readonly HashSet<long> _pinnedSequences = new();
    private readonly Dictionary<long, Task<SystemLogEntry?>> _loadTasks = new();
    private readonly SemaphoreSlim _readSemaphore = new(ReadConcurrency, ReadConcurrency);

    private FileStream? _writeStream;
    private FileStream? _readStream;
    private string _sessionDirectory = string.Empty;
    private int _hotCapacity;
    private long _hotByteBudget;
    private long _hotEntryBytes;
    private long _nextSequenceId;
    private long _structureVersion = 1;
    private long _sessionToken = 1;
    private bool _disposed;

    public SystemLogSessionStore(int hotCapacity)
    {
        _hotCapacity = NormalizeHotCapacity(hotCapacity);
        _hotByteBudget = CalculateHotByteBudget(_hotCapacity);
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogViewer",
            "Sessions");

        _deviceValues.Add(string.Empty);
        _deviceKeysByValue[string.Empty] = 0;
        _tagValues.Add(string.Empty);
        _tagKeysByValue[string.Empty] = 0;

        Directory.CreateDirectory(_sessionsRoot);
        CleanupStaleSessions();
        RotateSessionCore();
    }

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

    public void UpdateHotCapacity(int hotCapacity)
    {
        lock (_gate)
        {
            _hotCapacity = NormalizeHotCapacity(hotCapacity);
            _hotByteBudget = CalculateHotByteBudget(_hotCapacity);
            TrimHotEntriesLocked();
        }
    }

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
            stream.WriteByte((byte)'\n');

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
            RememberHotEntryLocked(CloneEntry(entry));

            return CreateRecordRef(record);
        }
    }

    public IReadOnlyList<SystemLogRecordRef> CopyActiveRecords(string? deviceId, long maxSequenceInclusive = long.MaxValue)
    {
        lock (_gate)
        {
            return CopyActiveRecordsLocked(deviceId, 0, maxSequenceInclusive);
        }
    }

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

    public SystemLogEntry GetDisplayEntry(SystemLogRecordRef record)
    {
        if (TryGetHotEntry(record.SequenceId, out var hotEntry) && hotEntry != null)
        {
            return hotEntry;
        }

        return CreateShellEntry(record);
    }

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

    public bool MatchesLevel(SystemLogRecordRef record, string? level)
    {
        return string.IsNullOrEmpty(level) ||
               string.Equals(DecodeLevel(record.LevelCode), level, StringComparison.OrdinalIgnoreCase);
    }

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

    public string GetTagText(SystemLogRecordRef record)
    {
        lock (_gate)
        {
            return GetTagValueLocked(record.TagKey);
        }
    }

    public async Task<bool> MatchesKeywordAsync(SystemLogRecordRef record, string keyword, CancellationToken cancellationToken)
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
            new DateTime(record.TimestampTicks).ToString("HH:mm:ss.fff").Contains(keyword, StringComparison.OrdinalIgnoreCase))
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
            try { _writeStream?.Dispose(); } catch { }
            try { _readStream?.Dispose(); } catch { }
            _writeStream = null;
            _readStream = null;
        }

        _readSemaphore.Dispose();

        if (!string.IsNullOrWhiteSpace(_sessionDirectory))
        {
            _ = Task.Run(() => TryDeleteDirectory(_sessionDirectory));
        }
    }

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

    private async Task<SystemLogEntry?> LoadAndCacheEntryAsync(long sequenceId, long sessionToken)
    {
        try
        {
            await _readSemaphore.WaitAsync().ConfigureAwait(false);

            SystemLogRecordRef recordRef;
            lock (_gate)
            {
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
                if (_disposed || _sessionToken != sessionToken || !TryGetRecordRefLocked(sequenceId, out var currentRecord))
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

    private async Task<SystemLogEntry?> ReadEntryForScanAsync(SystemLogRecordRef record, CancellationToken cancellationToken)
    {
        return await ReadEntryCoreAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SystemLogEntry?> ReadEntryCoreAsync(SystemLogRecordRef record, CancellationToken cancellationToken)
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

    private void RotateSessionCore()
    {
        _sessionToken++;

        try { _writeStream?.Dispose(); } catch { }
        try { _readStream?.Dispose(); } catch { }

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

        _sessionDirectory = Path.Combine(_sessionsRoot, $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sessionDirectory);

        var filePath = Path.Combine(_sessionDirectory, "systemlogs.jsonl");
        _writeStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);
        _readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
    }

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

    private void TouchHotEntryLocked(HotEntryCache cache)
    {
        if (cache.Node.List == null || cache.Node.List.Last == cache.Node)
        {
            return;
        }

        _hotEntryLru.Remove(cache.Node);
        _hotEntryLru.AddLast(cache.Node);
    }

    private void RemoveHotEntryLocked(long sequenceId)
    {
        if (_hotEntries.TryGetValue(sequenceId, out var cache))
        {
            _hotEntryBytes -= cache.EstimatedBytes;
            _hotEntryLru.Remove(cache.Node);
            _hotEntries.Remove(sequenceId);
        }
    }

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

    private bool TryGetStoredRecordLocked(long sequenceId, out StoredRecord record)
    {
        var index = checked((int)(sequenceId - 1));
        if (index < 0 || index >= _records.Count)
        {
            record = default;
            return false;
        }

        record = _records[index];
        return (record.Flags & ClearedFlag) == 0 && record.SequenceId == sequenceId;
    }

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

    private int InternDeviceKeyLocked(string? value)
    {
        return InternStringKeyLocked(value, _deviceKeysByValue, _deviceValues);
    }

    private int InternTagKeyLocked(string? value)
    {
        return InternStringKeyLocked(value, _tagKeysByValue, _tagValues);
    }

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

    private bool TryGetDeviceKeyLocked(string deviceId, out int deviceKey)
    {
        return _deviceKeysByValue.TryGetValue(deviceId, out deviceKey);
    }

    private string GetDeviceValueLocked(int deviceKey)
    {
        return deviceKey >= 0 && deviceKey < _deviceValues.Count
            ? _deviceValues[deviceKey]
            : string.Empty;
    }

    private string GetTagValueLocked(int tagKey)
    {
        return tagKey >= 0 && tagKey < _tagValues.Count
            ? _tagValues[tagKey]
            : string.Empty;
    }

    private List<int> GetOrCreateDeviceRecordIndicesLocked(int deviceKey)
    {
        if (!_recordIndicesByDeviceKey.TryGetValue(deviceKey, out var indices))
        {
            indices = new List<int>();
            _recordIndicesByDeviceKey[deviceKey] = indices;
        }

        return indices;
    }

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

    private static int NormalizeHotCapacity(int hotCapacity)
    {
        return Math.Max(MinHotCapacity, hotCapacity);
    }

    private static long CalculateHotByteBudget(int hotCapacity)
    {
        var requested = (long)NormalizeHotCapacity(hotCapacity) * 4096L;
        return Math.Max(MinHotBytes, Math.Min(MaxHotBytes, requested));
    }

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

    private static int GetStringBytes(string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : value.Length * sizeof(char);
    }

    private static byte EncodeLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return 0;
        }

        return (byte)char.ToUpperInvariant(level[0]);
    }

    private static string DecodeLevel(byte levelCode)
    {
        return levelCode == 0 ? string.Empty : ((char)levelCode).ToString();
    }

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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SystemLogSessionStore));
        }
    }
}
