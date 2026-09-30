using System.Text.RegularExpressions;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class NormalLogForm : Form
{
    private const int DataRefreshDebounceMs = 80;
    private const int FilterInputDebounceMs = 200;

    private readonly record struct NormalLogQuery(string Keyword, Regex? Regex, int Level)
    {
        public bool FilterActive => !string.IsNullOrEmpty(Keyword) || Level > 0;
    }

    private LogEntry[] _normalView = Array.Empty<LogEntry>();
    private int _normalViewTotalCount;
    private bool _normalAutoScrollEnabled = true;
    private bool _isActive;
    private bool _isDirty = true;
    private int _queryVersion;
    private int _dataVersion;
    private CancellationTokenSource? _filterCts;
    private CancellationTokenSource? _refreshDebounceCts;
    private volatile bool _isClosing;

    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceNormalLogs;
    private readonly RingBuffer<LogEntry> _allNormalLogs;
    private readonly AppSettings _settings;
    private readonly Func<string?> _getCurrentDeviceId;

    public event Action<LogEntry?>? NormalLogEntryDoubleClicked;
    public event Action? ScrollStateChanged;
    public event Action? LogCountChanged;

    public NormalLogForm(
        Dictionary<string, RingBuffer<LogEntry>> deviceNormalLogs,
        RingBuffer<LogEntry> allNormalLogs,
        AppSettings settings,
        Func<string?> getCurrentDeviceId)
    {
        _deviceNormalLogs = deviceNormalLogs;
        _allNormalLogs = allNormalLogs;
        _settings = settings;
        _getCurrentDeviceId = getCurrentDeviceId;
        InitializeComponent();
        ConfigureNormalLogList();
    }

    public bool IsAutoScrollEnabled => _normalAutoScrollEnabled;

    public RingBuffer<LogEntry> GetCurrentNormalLogBuffer()
    {
        var id = _getCurrentDeviceId();
        if (id == null) return _allNormalLogs;
        return _deviceNormalLogs.TryGetValue(id, out var buf) ? buf : _allNormalLogs;
    }

    public void ApplyLanguage()
    {
        _btnNormalScrollToTop.Text = Language.ScrollToTop;
        _btnNormalScrollToBottom.Text = Language.ScrollToBottom;
        _normalFilterPanel.ApplyLanguage(Language.KeywordPlaceholder, Language.RegexMode);
        _normalFilterPanel.SetFilter1Items([Language.All, "V", "D", "I", "W", "E", "F"]);
        _normalFilterPanel.SetFilter2Items([Language.All]);
    }

    public void ApplyFont(Font font)
    {
        _lstNormalLogs.Font = font;
    }

    public void ApplySettings(AppSettings settings)
    {
        _normalFilterPanel.NotifyRegexError = settings.NotifyRegexError;
        _normalFilterPanel.EnsureDefaultSelections();
    }

    public void ClearFilterAndRefresh()
    {
        InvalidateData(clearView: true);
    }

    public void RebuildFilter()
    {
        RequestQueryRefresh(0);
    }

    public void SetActive(bool active)
    {
        if (_isClosing || IsDisposed) return;
        if (_isActive == active)
        {
            if (active && _isDirty) ScheduleNormalRefresh(0);
            return;
        }

        _isActive = active;
        if (!active)
        {
            _isDirty = true;
            CancelNormalRefreshDelay();
            CancelNormalFilter();
            return;
        }

        ScheduleNormalRefresh(0);
    }

    public void NotifyDataChanged()
    {
        if (_isClosing || IsDisposed) return;
        _dataVersion++;
        _isDirty = true;
        if (_isActive) ScheduleNormalRefresh(DataRefreshDebounceMs);
    }

    public void InvalidateData(bool clearView = false)
    {
        if (_isClosing || IsDisposed) return;
        _queryVersion++;
        _dataVersion++;
        _isDirty = true;
        CancelNormalRefreshDelay();
        CancelNormalFilter();

        if (clearView) ClearNormalView();
        if (_isActive) ScheduleNormalRefresh(0);
    }

    public void CancelAsyncOperations()
    {
        _queryVersion++;
        _isDirty = true;
        CancelNormalRefreshDelay();
        CancelNormalFilter();
    }

    public void OnNormalLogAdded(LogEntry entry, bool isActiveView, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (isActiveView) NotifyDataChanged();
    }

    public void HandleEndKey()
    {
        _normalAutoScrollEnabled = true;
        BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    public (int filtered, int total) GetFilterCounts()
    {
        return (_normalView.Length, _normalViewTotalCount);
    }

    private void ConfigureNormalLogList()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstNormalLogs);
        _lstNormalLogs.Columns.Add(Language.TimeColumn, 100);
        _lstNormalLogs.Columns.Add(Language.LevelColumn, 50);
        _lstNormalLogs.Columns.Add(Language.TagColumn, 100);
        _lstNormalLogs.Columns.Add(Language.NormalLogMessageColumn, 500);
        _lstNormalLogs.RetrieveVirtualItem += OnNormalLogsRetrieveVirtualItem;
        _lstNormalLogs.SelectedIndexChanged += (_, _) =>
        {
            if (_lstNormalLogs.SelectedIndices.Count > 0)
            {
                _normalAutoScrollEnabled = false;
                ScrollStateChanged?.Invoke();
                UpdateLogCount();
            }
        };
        _lstNormalLogs.DoubleClick += OnNormalLogsDoubleClick;
        _lstNormalLogs.MouseWheel += OnNormalLogsMouseWheel;
        _lstNormalLogs.ContextMenuStrip = CreateNormalLogMenu();
        _lstNormalLogs.KeyUp += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                var entry = GetSelectedNormalEntry();
                ClipboardTextHelper.TrySetText(entry?.Message);
            }
        };
    }

    private LogEntry? GetNormalLogEntryByViewIndex(int index)
    {
        return index >= 0 && index < _normalView.Length ? _normalView[index] : null;
    }

    private void OnNormalLogsRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var entry = GetNormalLogEntryByViewIndex(e.ItemIndex);
        e.Item = entry == null ? new ListViewItem() : CreateNormalLogItem(entry);
    }

    private static string LevelToDisplayText(int level) => level switch
    {
        2 => "V", 3 => "D", 4 => "I", 5 => "W", 6 => "E", 7 => "F", _ => "?"
    };

    private static int LevelFromDisplayText(string text) => text switch
    {
        "V" => 2, "D" => 3, "I" => 4, "W" => 5, "E" => 6, "F" => 7, _ => 0
    };

    private ListViewItem CreateNormalLogItem(LogEntry entry)
    {
        var timeStr = entry.SendTime > 0 ? entry.SendTimeDt.ToString("HH:mm:ss.fff") : "";
        var levelStr = LevelToDisplayText(entry.EffectiveLevel);
        var location = ExtractLocation(entry.FileHead, entry.Method);
        var msg = TruncateNormalPreview(entry.Message);
        var item = new ListViewItem(timeStr);
        item.SubItems.Add(levelStr);
        item.SubItems.Add(location);
        item.SubItems.Add(msg);
        item.ForeColor = LevelToColor(entry.EffectiveLevel);
        return item;
    }

    private static string ExtractLocation(string? fileHead, string? fallback)
    {
        if (!string.IsNullOrEmpty(fileHead))
        {
            var trimmed = fileHead.Trim().TrimStart('[');
            var closeIdx = trimmed.IndexOf(']');
            if (closeIdx > 0) trimmed = trimmed[..closeIdx].Trim();
            var lastComma = trimmed.LastIndexOf(", ");
            if (lastComma >= 0) trimmed = trimmed[(lastComma + 2)..];
            return trimmed;
        }
        return fallback ?? "";
    }

    private static string TruncateNormalPreview(string? text, int maxLen = 120)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Replace("\r", "").Replace("\n", " ").Trim();
        return s.Length <= maxLen ? s : s[..maxLen] + "...";
    }

    private static Color LevelToColor(int level) => level switch
    {
        2 => Color.Gray, 3 => DefaultForeColor, 4 => Color.Green,
        5 => Color.Orange, 6 => Color.Red, 7 => Color.Red, _ => DefaultForeColor
    };

    private void OnNormalLogsDoubleClick(object? sender, EventArgs e)
    {
        var entry = GetSelectedNormalEntry();
        if (entry != null)
            NormalLogEntryDoubleClicked?.Invoke(entry);
    }

    private LogEntry? GetSelectedNormalEntry()
    {
        return _lstNormalLogs.SelectedIndices.Count > 0
            ? GetNormalLogEntryByViewIndex(_lstNormalLogs.SelectedIndices[0])
            : null;
    }

    private ContextMenuStrip CreateNormalLogMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Language.CopyNormalLogMessage, null, (s, e) =>
        {
            var entry = GetSelectedNormalEntry();
            ClipboardTextHelper.TrySetText(entry?.Message);
        });
        return menu;
    }

    private void RequestQueryRefresh(int debounceMs)
    {
        if (_isClosing || IsDisposed) return;
        _queryVersion++;
        _isDirty = true;
        CancelNormalRefreshDelay();
        CancelNormalFilter();
        if (_isActive) ScheduleNormalRefresh(debounceMs);
    }

    private void ScheduleNormalRefresh(int debounceMs)
    {
        if (!_isActive || !_isDirty || _isClosing || IsDisposed || !IsHandleCreated) return;
        if (_filterCts != null || _refreshDebounceCts != null) return;
        if (debounceMs <= 0)
        {
            StartNormalSnapshotRefresh();
            return;
        }

        var cts = new CancellationTokenSource();
        _refreshDebounceCts = cts;
        _ = DelayNormalRefreshAsync(debounceMs, cts);
    }

    private async Task DelayNormalRefreshAsync(int debounceMs, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(debounceMs, cts.Token).ConfigureAwait(false);
            await PostToUiWithRetryAsync(() =>
            {
                if (!ReferenceEquals(_refreshDebounceCts, cts)) return;
                _refreshDebounceCts.Dispose();
                _refreshDebounceCts = null;
                StartNormalSnapshotRefresh();
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartNormalSnapshotRefresh()
    {
        if (!_isActive || !_isDirty || _isClosing || IsDisposed || !IsHandleCreated || _filterCts != null)
            return;

        var source = CaptureCurrentNormalLogSnapshot();
        var query = CaptureNormalLogQuery();
        var queryVersion = _queryVersion;
        var dataVersion = _dataVersion;
        _isDirty = false;

        var cts = new CancellationTokenSource();
        _filterCts = cts;
        _ = FilterNormalSnapshotAsync(source, query, queryVersion, dataVersion, cts);
    }

    private LogEntry[] CaptureCurrentNormalLogSnapshot()
    {
        var buffer = GetCurrentNormalLogBuffer();
        var snapshot = new LogEntry[buffer.Count];
        for (var i = 0; i < snapshot.Length; i++) snapshot[i] = buffer.Get(i);
        return snapshot;
    }

    private NormalLogQuery CaptureNormalLogQuery()
    {
        var keyword = _normalFilterPanel.Keyword;
        var regex = _normalFilterPanel.RegexMode ? _normalFilterPanel.CachedRegex : null;
        return new NormalLogQuery(
            keyword,
            regex,
            LevelFromDisplayText(NormalizeNormalFilterValue(_normalFilterPanel.Filter1Value) ?? string.Empty));
    }

    private async Task FilterNormalSnapshotAsync(
        LogEntry[] source,
        NormalLogQuery query,
        int queryVersion,
        int dataVersion,
        CancellationTokenSource cts)
    {
        try
        {
            var token = cts.Token;
            var view = await Task.Run(
                () => BuildNormalView(source, query, token), token).ConfigureAwait(false);
            await PostToUiWithRetryAsync(
                () => CompleteNormalSnapshotRefresh(source.Length, view, queryVersion, dataVersion, cts),
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await PostToUiWithRetryAsync(() => CompleteNormalSnapshotFailure(cts), CancellationToken.None);
        }
    }

    private static LogEntry[] BuildNormalView(
        LogEntry[] source,
        NormalLogQuery query,
        CancellationToken token)
    {
        if (!query.FilterActive) return source;

        var filtered = new List<LogEntry>(source.Length);
        foreach (var entry in source)
        {
            token.ThrowIfCancellationRequested();
            if (MatchesNormalFilter(entry, query)) filtered.Add(entry);
        }

        return filtered.ToArray();
    }

    private void CompleteNormalSnapshotRefresh(
        int totalCount,
        LogEntry[] view,
        int queryVersion,
        int dataVersion,
        CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_filterCts, cts)) return;
        _filterCts.Dispose();
        _filterCts = null;

        if (_isClosing || IsDisposed || !_isActive || queryVersion != _queryVersion)
        {
            _isDirty = true;
            return;
        }

        ApplyNormalSnapshot(view, totalCount);
        if (dataVersion != _dataVersion) _isDirty = true;
        if (_isDirty) ScheduleNormalRefresh(DataRefreshDebounceMs);
    }

    private void CompleteNormalSnapshotFailure(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_filterCts, cts)) return;
        _filterCts.Dispose();
        _filterCts = null;
        _isDirty = true;
        ScheduleNormalRefresh(DataRefreshDebounceMs);
    }

    private void ApplyNormalSnapshot(LogEntry[] view, int totalCount)
    {
        var hadItems = _lstNormalLogs.VirtualListSize > 0;
        var wasAtBottom = BufferedListViewHelper.IsAtBottom(_lstNormalLogs);
        var followBottom = _normalAutoScrollEnabled && wasAtBottom;
        var anchorIndex = followBottom ? -1 : BufferedListViewHelper.GetTopIndexExact(_lstNormalLogs);
        var anchorEntry = anchorIndex >= 0 && anchorIndex < _normalView.Length
            ? _normalView[anchorIndex]
            : null;
        if (hadItems && !wasAtBottom) _normalAutoScrollEnabled = false;

        if (followBottom)
        {
            _lstNormalLogs.SelectedIndices.Clear();
            _lstNormalLogs.FocusedItem = null;
        }

        _normalView = view;
        _normalViewTotalCount = totalCount;
        _lstNormalLogs.VirtualListSize = view.Length;

        if (followBottom)
            BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        else
            BufferedListViewHelper.RestoreTopIndexExact(
                _lstNormalLogs, ResolveNormalAnchorIndex(view, anchorEntry, anchorIndex));

        _lstNormalLogs.Invalidate();
        UpdateLogCount();
    }

    private static int ResolveNormalAnchorIndex(
        LogEntry[] view,
        LogEntry? anchorEntry,
        int fallbackIndex)
    {
        if (view.Length == 0) return -1;
        if (anchorEntry != null)
        {
            for (var i = 0; i < view.Length; i++)
            {
                if (ReferenceEquals(view[i], anchorEntry)) return i;
            }
        }

        return Math.Clamp(fallbackIndex, 0, view.Length - 1);
    }

    private void ClearNormalView()
    {
        _normalView = Array.Empty<LogEntry>();
        _normalViewTotalCount = 0;
        if (!IsHandleCreated) return;
        _lstNormalLogs.SelectedIndices.Clear();
        _lstNormalLogs.FocusedItem = null;
        _lstNormalLogs.VirtualListSize = 0;
        _lstNormalLogs.Invalidate();
        UpdateLogCount();
    }

    private bool PostToUi(Action action)
    {
        if (_isClosing || IsDisposed || Disposing || !IsHandleCreated) return false;
        try { BeginInvoke(action); return true; }
        catch (ObjectDisposedException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>
    /// 句柄重建窗口内投递可能失败，短延迟重试；窗体已废弃时放弃（状态不再有意义）。
    /// </summary>
    private async Task PostToUiWithRetryAsync(Action action, CancellationToken token)
    {
        while (!PostToUi(action))
        {
            if (_isClosing || IsDisposed || Disposing) return;
            try { await Task.Delay(50, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void CancelNormalRefreshDelay()
    {
        var cts = _refreshDebounceCts;
        _refreshDebounceCts = null;
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
    }

    private void CancelNormalFilter()
    {
        var cts = _filterCts;
        _filterCts = null;
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
    }

    private static bool MatchesNormalFilter(LogEntry entry, NormalLogQuery query)
    {
        if (query.Level > 0 && entry.EffectiveLevel != query.Level) return false;
        if (string.IsNullOrEmpty(query.Keyword)) return true;

        if (query.Regex != null)
        {
            try
            {
                return query.Regex.IsMatch(entry.Message ?? "") ||
                       query.Regex.IsMatch(entry.Method ?? "");
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return entry.Message?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
               entry.Method?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? NormalizeNormalFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, Language.All, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private void OnNormalFilterChanged(object? sender, EventArgs e)
    {
        RequestQueryRefresh(FilterInputDebounceMs);
    }

    private void OnNormalLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _normalAutoScrollEnabled = false;
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    private void RefreshNormalVisibleRows()
    {
        if (_lstNormalLogs.VirtualListSize <= 0)
        {
            _lstNormalLogs.Invalidate();
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstNormalLogs);
        var bottomIndex = Math.Min(_lstNormalLogs.VirtualListSize - 1,
            topIndex + BufferedListViewHelper.GetApproxVisibleRowCount(_lstNormalLogs) - 1);
        if (bottomIndex < topIndex)
        {
            _lstNormalLogs.Invalidate();
            return;
        }

        try { _lstNormalLogs.RedrawItems(topIndex, bottomIndex, false); }
        catch { _lstNormalLogs.Invalidate(); }
    }

    private void UpdateLogCount()
    {
        var total = _normalViewTotalCount;
        var filtered = _normalView.Length;
        var max = _getCurrentDeviceId() == null ? _settings.MaxNormalLogEntries : _settings.MaxNormalLogEntriesPerDevice;
        var pct = max > 0 ? (double)total / max : 0;
        var countText = Language.LogsCount(filtered, total);
        var isPaused = !(_normalAutoScrollEnabled && BufferedListViewHelper.IsAtBottom(_lstNormalLogs));
        _lblNormalLogCount.Text = Language.LogsCountWithMax(countText, max, isPaused);
        _lblNormalLogCount.ForeColor = pct >= 1.0 ? Color.Red : pct >= 0.8 ? Color.Orange : DefaultForeColor;
        _btnNormalScrollToBottom.BackColor = _normalAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
        LogCountChanged?.Invoke();
    }

    private void OnNormalScrollToTopClick(object? sender, EventArgs e)
    {
        _normalAutoScrollEnabled = false;
        BufferedListViewHelper.ScrollToTop(_lstNormalLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    private void OnNormalScrollToBottomClick(object? sender, EventArgs e)
    {
        _normalAutoScrollEnabled = true;
        _lstNormalLogs.SelectedIndices.Clear();
        BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_isActive && _isDirty) ScheduleNormalRefresh(0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isClosing = true;
            CancelNormalRefreshDelay();
            CancelNormalFilter();
        }

        base.Dispose(disposing);
    }
}
