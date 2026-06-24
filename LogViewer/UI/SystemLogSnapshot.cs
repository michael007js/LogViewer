using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// 系统日志的只读快照，捕获当前设备ID、筛选条件和记录列表，
/// 为 VirtualMode ListView 提供稳定的渲染数据源。
/// 将序列ID映射到视图索引，支持按序列ID快速定位行号。
/// </summary>
internal sealed class SystemLogSnapshot
{
    // 序列ID → 视图索引的快速查找表，用于 RetrieveVirtualItem 定位
    private readonly Dictionary<long, int> _viewIndicesBySequence = new();

    // 当前快照中的记录引用列表
    private readonly List<SystemLogRecordRef> _records;

    /// <summary>
    /// 私有构造函数，初始化快照并构建序列ID索引映射。
    /// </summary>
    /// <param name="deviceId">设备ID，空字符串转为 null。</param>
    /// <param name="keyword">关键字筛选条件。</param>
    /// <param name="level">日志级别筛选条件。</param>
    /// <param name="tag">标签筛选条件。</param>
    /// <param name="storeStructureVersion">数据存储结构版本号，用于检测数据重建。</param>
    /// <param name="maxSequenceInclusive">当前最大序列ID（含）。</param>
    /// <param name="filterActive">是否有筛选条件生效。</param>
    /// <param name="records">记录引用列表。</param>
    private SystemLogSnapshot(
        string? deviceId,
        string keyword,
        string? level,
        string? tag,
        long storeStructureVersion,
        long maxSequenceInclusive,
        bool filterActive,
        List<SystemLogRecordRef> records)
    {
        DeviceId = string.IsNullOrEmpty(deviceId) ? null : deviceId;
        Keyword = keyword;
        Level = NormalizeFilterValue(level);
        Tag = NormalizeFilterValue(tag);
        StoreStructureVersion = storeStructureVersion;
        MaxSequenceInclusive = maxSequenceInclusive;
        FilterActive = filterActive;
        _records = records;

        for (var i = 0; i < _records.Count; i++)
        {
            _viewIndicesBySequence[_records[i].SequenceId] = i;
        }
    }

    /// <summary>
    /// 空快照单例，表示无设备、无筛选、无记录的初始状态。
    /// </summary>
    public static SystemLogSnapshot Empty { get; } = new(
        deviceId: null,
        keyword: string.Empty,
        level: null,
        tag: null,
        storeStructureVersion: 0,
        maxSequenceInclusive: 0,
        filterActive: false,
        records: new List<SystemLogRecordRef>());

    /// <summary>当前快照所属的设备ID，null 表示不限设备。</summary>
    public string? DeviceId { get; }

    /// <summary>关键字筛选条件。</summary>
    public string Keyword { get; }

    /// <summary>日志级别筛选条件，null 表示不限级别。</summary>
    public string? Level { get; }

    /// <summary>标签筛选条件，null 表示不限标签。</summary>
    public string? Tag { get; }

    /// <summary>数据存储结构版本号，变化时表示底层 RingBuffer 已重建，快照失效。</summary>
    public long StoreStructureVersion { get; }

    /// <summary>当前最大序列ID（含），新数据到达后可通过 Observe/Append 更新。</summary>
    public long MaxSequenceInclusive { get; private set; }

    /// <summary>是否有筛选条件生效。</summary>
    public bool FilterActive { get; }

    /// <summary>快照中的记录数量。</summary>
    public int Count => _records.Count;

    /// <summary>快照中的记录引用列表，供 ListView VirtualMode 读取。</summary>
    public IReadOnlyList<SystemLogRecordRef> Records => _records;

    /// <summary>
    /// 工厂方法，创建新的系统日志快照。
    /// 若传入的 records 已是 List 则直接使用，否则复制为新列表。
    /// </summary>
    /// <param name="deviceId">设备ID。</param>
    /// <param name="keyword">关键字筛选条件。</param>
    /// <param name="level">日志级别筛选条件。</param>
    /// <param name="tag">标签筛选条件。</param>
    /// <param name="storeStructureVersion">数据存储结构版本号。</param>
    /// <param name="maxSequenceInclusive">当前最大序列ID。</param>
    /// <param name="filterActive">是否有筛选条件生效。</param>
    /// <param name="records">记录引用序列。</param>
    /// <returns>新建的系统日志快照。</returns>
    public static SystemLogSnapshot Create(
        string? deviceId,
        string keyword,
        string? level,
        string? tag,
        long storeStructureVersion,
        long maxSequenceInclusive,
        bool filterActive,
        IEnumerable<SystemLogRecordRef> records)
    {
        return new SystemLogSnapshot(
            deviceId,
            keyword,
            level,
            tag,
            storeStructureVersion,
            maxSequenceInclusive,
            filterActive,
            records is List<SystemLogRecordRef> list ? list : new List<SystemLogRecordRef>(records));
    }

    /// <summary>
    /// 判断给定查询条件是否与当前快照的筛选条件匹配（设备、关键字、级别、标签完全一致），
    /// 用于决定是否可以复用已有快照而不需重建。
    /// </summary>
    /// <param name="deviceId">待比较的设备ID。</param>
    /// <param name="keyword">待比较的关键字。</param>
    /// <param name="level">待比较的日志级别。</param>
    /// <param name="tag">待比较的标签。</param>
    /// <returns>查询条件与快照完全匹配返回 true。</returns>
    public bool MatchesQuery(string? deviceId, string keyword, string? level, string? tag)
    {
        return string.Equals(DeviceId ?? string.Empty, deviceId ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(Keyword, keyword, StringComparison.Ordinal) &&
               string.Equals(Level ?? string.Empty, NormalizeFilterValue(level) ?? string.Empty,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Tag ?? string.Empty, NormalizeFilterValue(tag) ?? string.Empty,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 通过序列ID查找对应的视图索引（行号），用于 RetrieveVirtualItem 定位。
    /// </summary>
    /// <param name="sequenceId">记录的序列ID。</param>
    /// <param name="viewIndex">输出对应的视图索引。</param>
    /// <returns>找到返回 true；序列ID不在快照中返回 false。</returns>
    public bool TryGetViewIndex(long sequenceId, out int viewIndex)
    {
        return _viewIndicesBySequence.TryGetValue(sequenceId, out viewIndex);
    }

    /// <summary>
    /// 观察一条新记录但不加入列表，仅更新最大序列ID。
    /// 用于新数据到达时保持 MaxSequenceInclusive 同步但不改变快照内容。
    /// </summary>
    /// <param name="record">新到达的记录引用。</param>
    public void Observe(SystemLogRecordRef record)
    {
        MaxSequenceInclusive = Math.Max(MaxSequenceInclusive, record.SequenceId);
    }

    /// <summary>
    /// 向快照追加一条新记录，同时更新索引映射和最大序列ID。
    /// 用于增量追加新到达的系统日志数据。
    /// </summary>
    /// <param name="record">要追加的记录引用。</param>
    public void Append(SystemLogRecordRef record)
    {
        Observe(record);
        _viewIndicesBySequence[record.SequenceId] = _records.Count;
        _records.Add(record);
    }

    /// <summary>
    /// 规范化筛选值：空字符串、"All" 等通配符转为 null（表示不限），
    /// 其他值保持原样。
    /// </summary>
    /// <param name="value">原始筛选值。</param>
    /// <returns>规范化后的筛选值，null 表示不限。</returns>
    private static string? NormalizeFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }
}