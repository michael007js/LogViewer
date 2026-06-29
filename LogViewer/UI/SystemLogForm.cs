using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class SystemLogForm : Form
{
    private const int SystemLogFlushBatchSize = 200;
    private const int SystemLogUiRefreshDebounceMs = 80;

    private readonly record struct SystemLogQuery(
        string? DeviceId, string Keyword, string? Level, string? Tag,
        long MaxSequenceInclusive, long StoreStructureVersion, bool IsRegex)
    {
        public bool FilterActive =>
            !string.IsNullOrEmpty(Keyword) || !string.IsNullOrEmpty(Level) || !string.IsNullOrEmpty(Tag);
        public bool KeywordActive => !string.IsNullOrEmpty(Keyword);
    }

    private SystemLogSessionStore _systemLogStore;
    private SystemLogSnapshot _systemLogSnapshot = SystemLogSnapshot.Empty;
    private CancellationTokenSource? _systemSnapshotCts;
    private CancellationTokenSource? _systemPrefetchCts;
    private int _systemSnapshotVersion;
    private bool _systemLogPaused;
    private long _systemFreezeSequenceId;
    private long _systemPausedBacklog;
    private bool _systemVisibleRefreshPending;
    private long _systemContextSequenceId;
    private bool _systemUiRefreshScheduled;
    private bool _systemUiRefreshNeedsSnapshot;
    private bool _systemAutoScrollEnabled = true;

    private readonly AppSettings _settings;
    private readonly Func<string?> _getCurrentDeviceId;
    private readonly Dictionary<string, string> _adbSerialToDeviceId;
    private readonly Func<bool> _isShowingSystemLog;

    public event Action? ScrollStateChanged;
    public event Action? SystemLogPausedChanged;
    public event Action? LogCountChanged;

    internal SystemLogForm(
        SystemLogSessionStore systemLogStore,
        AppSettings settings,
        Func<string?> getCurrentDeviceId,
        Dictionary<string, string> adbSerialToDeviceId,
        Func<bool> isShowingSystemLog)
    {
        _systemLogStore = systemLogStore;
        _settings = settings;
        _getCurrentDeviceId = getCurrentDeviceId;
        _adbSerialToDeviceId = adbSerialToDeviceId;
        _isShowingSystemLog = isShowingSystemLog;
        InitializeComponent();
        ConfigureSystemLogList();
    }

    public bool IsAutoScrollEnabled => _systemAutoScrollEnabled;
    public bool IsPaused => _systemLogPaused;

    internal void SetSystemLogStore(SystemLogSessionStore store)
    {
        _systemLogStore = store;
        _systemFreezeSequenceId = store.LastSequenceId;
    }

    public bool IsRuntimeReady()
    {
        return _systemLogStore != null;
    }

    public void ApplyLanguage()
    {
        _btnSystemScrollToTop.Text = Language.ScrollToTop;
        _btnSystemScrollToBottom.Text = Language.ScrollToBottom;
        _btnSystemPauseResume.Text = Language.Pause;
        _systemFilterPanel.ApplyLanguage(Language.KeywordPlaceholder, Language.RegexMode);
        _systemFilterPanel.SetFilter1Items([Language.All, "V", "D", "I", "W", "E", "F"]);
        _systemFilterPanel.SetFilter2Items([Language.All]);
    }

    public void ApplyFont(Font font)
    {
        _lstSystemLogs.Font = font;
    }

    public void ApplySettings(AppSettings settings)
    {
        _systemFilterPanel.NotifyRegexError = settings.NotifyRegexError;
        _systemFilterPanel.EnsureDefaultSelections();
    }

    public void ClearFilterAndRefresh()
    {
        RefreshSystemLogList();
    }

    public void RebuildFilter()
    {
        RefreshSystemLogList();
    }

    public void ProcessPendingLogs(List<SystemLogEntry> entries)
    {
        if (!IsRuntimeReady()) return;

        var selectedDeviceId = _getCurrentDeviceId();
        var scopeChanged = false;

        foreach (var entry in entries)
        {
            var serial = entry.SourceDeviceSerial ?? string.Empty;
            var deviceId = _adbSerialToDeviceId.TryGetValue(serial, out var mappedDeviceId)
                ? mappedDeviceId
                : serial;
            entry.SourceDeviceId = deviceId;
            _systemLogStore.Append(entry);

            if (!string.IsNullOrEmpty(selectedDeviceId) &&
                !string.Equals(deviceId, selectedDeviceId, StringComparison.Ordinal))
                continue;

            scopeChanged = true;
            if (_systemLogPaused)
                Interlocked.Increment(ref _systemPausedBacklog);
        }

        if (scopeChanged || _systemLogPaused)
            ScheduleSystemUiRefresh(scopeChanged && !_systemLogPaused);
    }

    public void OnStoreAppended(List<SystemLogEntry> entries)
    {
        if (!IsRuntimeReady()) return;

        var selectedDeviceId = _getCurrentDeviceId();
        var scopeChanged = false;

        foreach (var entry in entries)
        {
            var serial = entry.SourceDeviceSerial ?? string.Empty;
            var deviceId = _adbSerialToDeviceId.TryGetValue(serial, out var mappedDeviceId)
                ? mappedDeviceId
                : serial;
            entry.SourceDeviceId = deviceId;

            if (!string.IsNullOrEmpty(selectedDeviceId) &&
                !string.Equals(deviceId, selectedDeviceId, StringComparison.Ordinal))
                continue;

            scopeChanged = true;
            if (_systemLogPaused)
                Interlocked.Increment(ref _systemPausedBacklog);
        }

        if (scopeChanged || _systemLogPaused)
            ScheduleSystemUiRefresh(scopeChanged && !_systemLogPaused);
    }

    public void HandleEndKey()
    {
        _systemAutoScrollEnabled = true;
        BufferedListViewHelper.ScrollToBottom(_lstSystemLogs);
        ScrollStateChanged?.Invoke();
        UpdateSystemLogUiState();
    }

    public int GetCurrentSystemLogCount() => _systemLogSnapshot.Count;

    public void CancelAsyncOperations()
    {
        _systemSnapshotCts?.Cancel();
        _systemSnapshotCts?.Dispose();
        _systemPrefetchCts?.Cancel();
        _systemPrefetchCts?.Dispose();
    }

    private void ConfigureSystemLogList()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstSystemLogs);
        _lstSystemLogs.Columns.Add(Language.TimeColumn, 90);
        _lstSystemLogs.Columns.Add(Language.LevelColumn, 35);
        _lstSystemLogs.Columns.Add(Language.TagColumn, 120);
        _lstSystemLogs.Columns.Add(Language.MessageColumn, 500);
        _lstSystemLogs.RetrieveVirtualItem += OnSystemLogsRetrieveVirtualItem;
        _lstSystemLogs.CacheVirtualItems += OnSystemLogsCacheVirtualItems;
        _lstSystemLogs.SelectedIndexChanged += (_, _) => _systemContextSequenceId = 0;
        _lstSystemLogs.MouseUp += OnSystemLogMouseUp;
        _lstSystemLogs.MouseWheel += OnSystemLogsMouseWheel;
        _lstSystemLogs.ContextMenuStrip = CreateSystemLogMenu();
    }

    private void OnSystemLogsRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var entry = GetCurrentSystemLogEntry(e.ItemIndex);
        e.Item = entry == null ? new ListViewItem() : CreateSystemLogItem(entry);
    }

    private void OnSystemLogsCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
    {
        ScheduleSystemLogPrefetch(e.StartIndex, e.EndIndex);
    }

    private ListViewItem CreateSystemLogItem(SystemLogEntry entry)
    {
        var item = new ListViewItem(entry.Timestamp.ToString("HH:mm:ss.fff"));
        item.SubItems.Add(entry.LevelShort);
        item.SubItems.Add(entry.Tag ?? string.Empty);
        item.SubItems.Add(entry.Message ?? string.Empty);
        item.ForeColor = entry.LevelColor;
        return item;
    }

    private SystemLogEntry? GetCurrentSystemLogEntry(int index)
    {
        if (index < 0 || index >= _systemLogSnapshot.Count) return null;
        return _systemLogStore.GetDisplayEntry(_systemLogSnapshot.Records[index]);
    }

    private SystemLogRecordRef? GetCurrentSystemRecord(int index)
    {
        if (index < 0 || index >= _systemLogSnapshot.Count) return null;
        return _systemLogSnapshot.Records[index];
    }

    public void RefreshSystemLogList(bool preferBackground = false)
    {
        RequestSystemSnapshotRefresh(preferBackground: preferBackground);
    }

    private void RequestSystemSnapshotRefresh(int debounceMs = 0, bool preferBackground = false)
    {
        if (!IsRuntimeReady()) return;

        var query = CaptureSystemLogQuery();
        if (!preferBackground && TryCatchUpCurrentSnapshot(query))
        {
            ApplySystemSnapshot();
            return;
        }

        _systemSnapshotCts?.Cancel();
        _systemSnapshotCts?.Dispose();
        _systemSnapshotCts = new CancellationTokenSource();
        var token = _systemSnapshotCts.Token;
        var version = ++_systemSnapshotVersion;

        _ = Task.Run(async () =>
        {
            try
            {
                if (debounceMs > 0) await Task.Delay(debounceMs, token);
                var snapshot = await BuildSystemLogSnapshotAsync(query, token).ConfigureAwait(false);
                if (token.IsCancellationRequested || IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (token.IsCancellationRequested || IsDisposed || version != _systemSnapshotVersion) return;
                    _systemLogSnapshot = snapshot;
                    ApplySystemSnapshot();
                }));
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private async Task<SystemLogSnapshot> BuildSystemLogSnapshotAsync(SystemLogQuery query, CancellationToken token)
    {
        var baseRecords = _systemLogStore.CopyActiveRecords(query.DeviceId, query.MaxSequenceInclusive);
        token.ThrowIfCancellationRequested();

        if (!query.FilterActive)
        {
            return SystemLogSnapshot.Create(
                query.DeviceId, query.Keyword, query.Level, query.Tag,
                query.StoreStructureVersion, query.MaxSequenceInclusive, false, baseRecords);
        }

        var filtered = new List<SystemLogRecordRef>(baseRecords.Count);
        if (!query.KeywordActive)
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (MatchesSystemRecord(record, query)) filtered.Add(record);
            }
        }
        else if (query.IsRegex)
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesSystemRecord(record, query, includeKeyword: false)) continue;
                if (_systemFilterPanel.CachedRegex != null &&
                    await _systemLogStore.MatchesKeywordAsync(record, query.Keyword, token, _systemFilterPanel.CachedRegex).ConfigureAwait(false))
                    filtered.Add(record);
            }
        }
        else
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesSystemRecord(record, query, includeKeyword: false)) continue;
                if (await _systemLogStore.MatchesKeywordAsync(record, query.Keyword, token).ConfigureAwait(false))
                    filtered.Add(record);
            }
        }

        return SystemLogSnapshot.Create(
            query.DeviceId, query.Keyword, query.Level, query.Tag,
            query.StoreStructureVersion, query.MaxSequenceInclusive, true, filtered);
    }

    private bool TryCatchUpCurrentSnapshot(SystemLogQuery query)
    {
        if (!_systemLogSnapshot.MatchesQuery(query.DeviceId, query.Keyword, query.Level, query.Tag) ||
            _systemLogSnapshot.StoreStructureVersion != query.StoreStructureVersion)
            return false;

        if (_systemLogSnapshot.MaxSequenceInclusive > query.MaxSequenceInclusive) return false;
        if (_systemLogSnapshot.MaxSequenceInclusive >= query.MaxSequenceInclusive) return true;
        if (query.KeywordActive) return false;

        var newRecords = _systemLogStore.CopyActiveRecordsSince(
            query.DeviceId, _systemLogSnapshot.MaxSequenceInclusive, query.MaxSequenceInclusive);

        foreach (var record in newRecords)
        {
            if (_systemLogSnapshot.FilterActive)
            {
                _systemLogSnapshot.Observe(record);
                if (MatchesSystemRecord(record, query)) _systemLogSnapshot.Append(record);
            }
            else
            {
                _systemLogSnapshot.Append(record);
            }
        }

        return true;
    }

    private void ApplySystemSnapshot()
    {
        if (!IsRuntimeReady()) return;

        var showingSystemLog = _isShowingSystemLog();
        var anchorIndex = _systemAutoScrollEnabled || !showingSystemLog
            ? -1
            : BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);

        _lstSystemLogs.VirtualListSize = _systemLogSnapshot.Count;

        if (showingSystemLog && _systemAutoScrollEnabled)
            BufferedListViewHelper.ScrollToBottom(_lstSystemLogs);
        else if (anchorIndex >= 0)
            BufferedListViewHelper.RestoreTopIndexExact(_lstSystemLogs, anchorIndex);

        _lstSystemLogs.Invalidate();
        UpdateSystemTagOptionsFromSnapshot();
        ScheduleVisibleSystemPrefetch();
        UpdateSystemLogUiState();
    }

    private void RequestSystemVisibleRefresh()
    {
        if (!IsHandleCreated || IsDisposed || _systemVisibleRefreshPending) return;
        _systemVisibleRefreshPending = true;
        BeginInvoke(new Action(() =>
        {
            _systemVisibleRefreshPending = false;
            if (IsDisposed || !IsRuntimeReady()) return;
            RefreshSystemVisibleRows();
        }));
    }

    private void RefreshSystemVisibleRows()
    {
        if (_lstSystemLogs.VirtualListSize <= 0) { _lstSystemLogs.Invalidate(); return; }
        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);
        var bottomIndex = Math.Min(_lstSystemLogs.VirtualListSize - 1,
            topIndex + BufferedListViewHelper.GetApproxVisibleRowCount(_lstSystemLogs) - 1);
        if (bottomIndex < topIndex) { _lstSystemLogs.Invalidate(); return; }
        try { _lstSystemLogs.RedrawItems(topIndex, bottomIndex, false); }
        catch { _lstSystemLogs.Invalidate(); }
    }

    private void ScheduleSystemUiRefresh(bool needSnapshot)
    {
        if (needSnapshot) _systemUiRefreshNeedsSnapshot = true;
        if (_systemUiRefreshScheduled || !IsHandleCreated || IsDisposed) return;
        _systemUiRefreshScheduled = true;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(SystemLogUiRefreshDebounceMs).ConfigureAwait(false); } catch { }
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                _systemUiRefreshScheduled = false;
                if (IsDisposed || !IsRuntimeReady()) return;
                var needRefreshSnapshot = _systemUiRefreshNeedsSnapshot;
                _systemUiRefreshNeedsSnapshot = false;
                if (_systemLogPaused) { UpdateSystemLogUiState(); return; }
                if (needRefreshSnapshot && _isShowingSystemLog())
                {
                    RequestSystemSnapshotRefresh(preferBackground: true);
                    return;
                }

                UpdateSystemLogUiState();
            }));
        });
    }

    private void OnSystemFilterChanged(object? sender, EventArgs e)
    {
        RequestSystemSnapshotRefresh(200);
        UpdateSystemLogUiState();
    }

    private void OnSystemFilter2DropDown(object? sender, EventArgs e)
    {
        RefreshSystemTagOptions();
    }

    public void RefreshSystemTagOptions()
    {
        UpdateSystemTagOptionsFromSnapshot();
    }

    private void UpdateSystemTagOptionsFromSnapshot()
    {
        if (!IsHandleCreated) return;
        var selected = _systemFilterPanel.Filter2Value ?? Language.All;
        var ordered = _systemLogStore.GetOrderedTags(_systemLogSnapshot.DeviceId, _systemLogSnapshot.MaxSequenceInclusive);
        _systemFilterPanel.UpdateFilter2Items(ordered, selected);
    }

    private void OnSystemLogMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _lstSystemLogs.HitTest(e.Location);
        if (hit.Item == null) return;
        hit.Item.Selected = true;
        var record = GetCurrentSystemRecord(hit.Item.Index);
        if (record == null) return;
        _systemContextSequenceId = record.Value.SequenceId;
        _ = WarmSystemEntryAsync(_systemContextSequenceId);
        _lstSystemLogs.ContextMenuStrip?.Show(_lstSystemLogs.PointToScreen(e.Location));
    }

    private async Task WarmSystemEntryAsync(long sequenceId)
    {
        if (sequenceId <= 0) return;
        try
        {
            var entry = await _systemLogStore.GetEntryAsync(sequenceId, CancellationToken.None).ConfigureAwait(false);
            if (entry != null) RequestSystemVisibleRefresh();
        }
        catch { }
    }

    private void OnSystemLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _systemAutoScrollEnabled = false;
        ScrollStateChanged?.Invoke();
        UpdateSystemLogUiState();
    }

    private ContextMenuStrip CreateSystemLogMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Language.CopyMessage, null,
            async (s, e) => { await CopySelectedSystemLogAsync(static entry => entry.Message).ConfigureAwait(false); });
        menu.Items.Add(Language.CopyFullLine, null, async (s, e) =>
        {
            await CopySelectedSystemLogAsync(static entry =>
                $"{entry.Timestamp:HH:mm:ss.fff} {entry.LevelShort} {entry.Tag} {entry.Message}").ConfigureAwait(false);
        });
        return menu;
    }

    private async Task CopySelectedSystemLogAsync(Func<SystemLogEntry, string?> projector)
    {
        var sequenceId = GetSelectedSystemSequenceId();
        if (sequenceId <= 0) return;
        try
        {
            var entry = await _systemLogStore.GetEntryAsync(sequenceId, CancellationToken.None).ConfigureAwait(false);
            var text = entry == null ? null : projector(entry);
            if (string.IsNullOrEmpty(text) || IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => ClipboardTextHelper.TrySetText(text)));
        }
        catch { }
    }

    private long GetSelectedSystemSequenceId()
    {
        if (_systemContextSequenceId > 0) return _systemContextSequenceId;
        if (_lstSystemLogs.SelectedIndices.Count == 0) return 0;
        var selectedIndex = _lstSystemLogs.SelectedIndices[0];
        return selectedIndex >= 0 && selectedIndex < _systemLogSnapshot.Count
            ? _systemLogSnapshot.Records[selectedIndex].SequenceId
            : 0;
    }

    private void OnSystemPauseResumeClick(object? sender, EventArgs e)
    {
        if (!IsRuntimeReady()) return;
        if (_systemLogPaused)
        {
            _systemLogPaused = false;
            _systemPausedBacklog = 0;
            _systemFreezeSequenceId = _systemLogStore.LastSequenceId;
            RequestSystemSnapshotRefresh();
        }
        else
        {
            _systemLogPaused = true;
            _systemFreezeSequenceId = _systemLogStore.LastSequenceId;
            _systemPausedBacklog = 0;
            RequestSystemSnapshotRefresh();
        }

        UpdateSystemLogUiState();
        SystemLogPausedChanged?.Invoke();
    }

    private void UpdateSystemLogUiState()
    {
        _btnSystemPauseResume.Text = _systemLogPaused ? Language.Resume : Language.Pause;
        _lblSystemBacklog.Text = _systemPausedBacklog > 0 ? Language.BufferedCount(_systemPausedBacklog) : string.Empty;
        _lblSystemBacklog.Visible = _systemPausedBacklog > 0;
        _btnSystemPauseResume.BackColor = _systemLogPaused ? Color.LightGoldenrodYellow : DefaultBackColor;
        _btnSystemScrollToBottom.BackColor = _systemAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
        LogCountChanged?.Invoke();
    }

    private SystemLogQuery CaptureSystemLogQuery()
    {
        var keyword = _systemFilterPanel.Keyword;
        var level = NormalizeSystemFilterValue(_systemFilterPanel.Filter1Value);
        var tag = NormalizeSystemFilterValue(_systemFilterPanel.Filter2Value);
        var maxSequenceInclusive = _systemLogPaused ? _systemFreezeSequenceId : _systemLogStore.LastSequenceId;
        return new SystemLogQuery(
            _getCurrentDeviceId(), keyword, level, tag,
            maxSequenceInclusive, _systemLogStore.StructureVersion, _systemFilterPanel.RegexMode);
    }

    private bool MatchesSystemRecord(SystemLogRecordRef record, SystemLogQuery query, bool includeKeyword = true)
    {
        if (!_systemLogStore.MatchesScope(record, query.DeviceId) ||
            !_systemLogStore.MatchesLevel(record, query.Level) ||
            !_systemLogStore.MatchesTag(record, query.Tag))
            return false;
        return !includeKeyword || !query.KeywordActive;
    }

    private bool MatchesIncomingSystemEntry(SystemLogEntry entry, SystemLogQuery query)
    {
        if (!string.IsNullOrEmpty(query.DeviceId) &&
            !string.Equals(entry.SourceDeviceId ?? string.Empty, query.DeviceId, StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(query.Level) &&
            !string.Equals(entry.LevelShort, query.Level, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(query.Tag) &&
            !string.Equals(entry.Tag ?? string.Empty, query.Tag, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!query.KeywordActive) return true;
        if (query.IsRegex)
        {
            if (_systemFilterPanel.CachedRegex == null) return true;
            var combined = $"{entry.LevelShort} {entry.Tag} {entry.Message} {entry.ProcessId} {entry.ThreadId}";
            return _systemFilterPanel.CachedRegex.IsMatch(combined);
        }

        return (entry.LevelShort?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true) ||
               (entry.Tag?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true) ||
               (entry.Message?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true) ||
               entry.ProcessId.ToString().Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
               entry.ThreadId.ToString().Contains(query.Keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeSystemFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, Language.All, StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private void ScheduleVisibleSystemPrefetch()
    {
        if (!_isShowingSystemLog() || _lstSystemLogs.VirtualListSize <= 0)
        {
            _systemLogStore.UpdatePinnedSequences(Array.Empty<long>());
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);
        var bottomIndex = Math.Min(_lstSystemLogs.VirtualListSize - 1,
            topIndex + BufferedListViewHelper.GetApproxVisibleRowCount(_lstSystemLogs) - 1);
        ScheduleSystemLogPrefetch(topIndex, bottomIndex);
    }

    private void ScheduleSystemLogPrefetch(int startIndex, int endIndex)
    {
        if (!IsRuntimeReady() || _systemLogSnapshot.Count == 0) return;
        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(_systemLogSnapshot.Count - 1, endIndex);
        if (endIndex < startIndex) return;

        var visibleSequenceIds = new List<long>(endIndex - startIndex + 1);
        for (var i = startIndex; i <= endIndex; i++)
            visibleSequenceIds.Add(_systemLogSnapshot.Records[i].SequenceId);

        _systemLogStore.UpdatePinnedSequences(visibleSequenceIds);

        var prefetchStart = Math.Max(0, startIndex - 120);
        var prefetchEnd = Math.Min(_systemLogSnapshot.Count - 1, endIndex + 240);
        var prefetchSequenceIds = new List<long>(prefetchEnd - prefetchStart + 1);
        for (var i = prefetchStart; i <= prefetchEnd; i++)
            prefetchSequenceIds.Add(_systemLogSnapshot.Records[i].SequenceId);

        _systemPrefetchCts?.Cancel();
        _systemPrefetchCts?.Dispose();
        _systemPrefetchCts = new CancellationTokenSource();
        var token = _systemPrefetchCts.Token;
        var snapshot = _systemLogSnapshot;

        _ = Task.Run(async () =>
        {
            try
            {
                var loaded = await _systemLogStore.PrefetchAsync(prefetchSequenceIds, token).ConfigureAwait(false);
                if (loaded > 0 && !token.IsCancellationRequested && ReferenceEquals(snapshot, _systemLogSnapshot))
                    RequestSystemVisibleRefresh();
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private void OnSystemScrollToTopClick(object? sender, EventArgs e)
    {
        _systemAutoScrollEnabled = false;
        BufferedListViewHelper.ScrollToTop(_lstSystemLogs);
        ScrollStateChanged?.Invoke();
        UpdateSystemLogUiState();
    }

    private void OnSystemScrollToBottomClick(object? sender, EventArgs e)
    {
        _systemAutoScrollEnabled = true;
        BufferedListViewHelper.ScrollToBottom(_lstSystemLogs);
        ScrollStateChanged?.Invoke();
        UpdateSystemLogUiState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _systemSnapshotCts?.Cancel(); } catch (ObjectDisposedException) { }
            _systemSnapshotCts?.Dispose();
            try { _systemPrefetchCts?.Cancel(); } catch (ObjectDisposedException) { }
            _systemPrefetchCts?.Dispose();
        }

        base.Dispose(disposing);
    }
}
