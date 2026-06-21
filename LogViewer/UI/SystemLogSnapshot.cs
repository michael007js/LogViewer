using LogViewer.Utils;

namespace LogViewer.UI;

internal sealed class SystemLogSnapshot
{
    private readonly Dictionary<long, int> _viewIndicesBySequence = new();
    private readonly List<SystemLogRecordRef> _records;

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

    public static SystemLogSnapshot Empty { get; } = new(
        deviceId: null,
        keyword: string.Empty,
        level: null,
        tag: null,
        storeStructureVersion: 0,
        maxSequenceInclusive: 0,
        filterActive: false,
        records: new List<SystemLogRecordRef>());

    public string? DeviceId { get; }
    public string Keyword { get; }
    public string? Level { get; }
    public string? Tag { get; }
    public long StoreStructureVersion { get; }
    public long MaxSequenceInclusive { get; private set; }
    public bool FilterActive { get; }
    public int Count => _records.Count;
    public IReadOnlyList<SystemLogRecordRef> Records => _records;

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

    public bool MatchesQuery(string? deviceId, string keyword, string? level, string? tag)
    {
        return string.Equals(DeviceId ?? string.Empty, deviceId ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(Keyword, keyword, StringComparison.Ordinal) &&
               string.Equals(Level ?? string.Empty, NormalizeFilterValue(level) ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Tag ?? string.Empty, NormalizeFilterValue(tag) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetViewIndex(long sequenceId, out int viewIndex)
    {
        return _viewIndicesBySequence.TryGetValue(sequenceId, out viewIndex);
    }

    public void Observe(SystemLogRecordRef record)
    {
        MaxSequenceInclusive = Math.Max(MaxSequenceInclusive, record.SequenceId);
    }

    public void Append(SystemLogRecordRef record)
    {
        Observe(record);
        _viewIndicesBySequence[record.SequenceId] = _records.Count;
        _records.Add(record);
    }

    private static string? NormalizeFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }
}
