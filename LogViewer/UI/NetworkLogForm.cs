using System.Text.RegularExpressions;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class NetworkLogForm : Form
{
    private const int DataRefreshDebounceMs = 80;
    private const int FilterInputDebounceMs = 200;

    private readonly record struct NetworkLogQuery(
        string Keyword,
        Regex? Regex,
        string? Method,
        string? StatusFilter)
    {
        public bool FilterActive =>
            !string.IsNullOrEmpty(Keyword) || Method != null || StatusFilter != null;
    }

    private LogEntry[] _networkView = Array.Empty<LogEntry>();
    private int _networkViewTotalCount;
    private bool _networkAutoScrollEnabled = true;
    private bool _isActive;
    private bool _isDirty = true;
    private int _queryVersion;
    private int _dataVersion;
    private CancellationTokenSource? _filterCts;
    private CancellationTokenSource? _refreshDebounceCts;
    private volatile bool _isClosing;

    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceLogs;
    private readonly RingBuffer<LogEntry> _allLogs;
    private readonly AppSettings _settings;
    private readonly Func<string?> _getCurrentDeviceId;

    public event Action<LogEntry?>? LogEntrySelected;
    public event Action<LogEntry?>? LogEntryDoubleClicked;
    public event Action? ScrollStateChanged;
    public event Action? LogCountChanged;

    public NetworkLogForm(
        Dictionary<string, RingBuffer<LogEntry>> deviceLogs,
        RingBuffer<LogEntry> allLogs,
        AppSettings settings,
        Func<string?> getCurrentDeviceId)
    {
        _deviceLogs = deviceLogs;
        _allLogs = allLogs;
        _settings = settings;
        _getCurrentDeviceId = getCurrentDeviceId;
        InitializeComponent();
        ConfigureLogLists();
    }

    public bool IsAutoScrollEnabled => _networkAutoScrollEnabled;

    public RingBuffer<LogEntry> GetCurrentLogBuffer()
    {
        var id = _getCurrentDeviceId();
        if (id == null) return _allLogs;
        return _deviceLogs.TryGetValue(id, out var buf) ? buf : _allLogs;
    }

    public void ApplyLanguage()
    {
        _btnScrollToTop.Text = Language.ScrollToTop;
        _btnScrollToBottom.Text = Language.ScrollToBottom;
        _networkFilterPanel.ApplyLanguage(Language.KeywordPlaceholder, Language.RegexMode);
        _networkFilterPanel.SetFilter1Items([Language.All, "GET", "POST", "PUT", "DELETE", "PATCH"]);
        _networkFilterPanel.SetFilter2Items([Language.All, "2xx", "3xx", "4xx", "5xx", "0"]);
    }

    public void ApplyFont(Font font)
    {
        _lstNetworkLogs.Font = font;
    }

    public void ApplySettings(AppSettings settings)
    {
        _networkFilterPanel.NotifyRegexError = settings.NotifyRegexError;
        _networkFilterPanel.EnsureDefaultSelections();
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
            if (active && _isDirty) ScheduleNetworkRefresh(0);
            return;
        }

        _isActive = active;
        if (!active)
        {
            _isDirty = true;
            CancelNetworkRefreshDelay();
            CancelNetworkFilter();
            return;
        }

        ScheduleNetworkRefresh(0);
    }

    public void NotifyDataChanged()
    {
        if (_isClosing || IsDisposed) return;
        _dataVersion++;
        _isDirty = true;
        if (_isActive) ScheduleNetworkRefresh(DataRefreshDebounceMs);
    }

    public void InvalidateData(bool clearView = false)
    {
        if (_isClosing || IsDisposed) return;
        _queryVersion++;
        _dataVersion++;
        _isDirty = true;
        CancelNetworkRefreshDelay();
        CancelNetworkFilter();

        if (clearView) ClearNetworkView();
        if (_isActive) ScheduleNetworkRefresh(0);
    }

    public void CancelAsyncOperations()
    {
        _queryVersion++;
        _isDirty = true;
        CancelNetworkRefreshDelay();
        CancelNetworkFilter();
    }

    public void OnLogAdded(LogEntry entry, bool isActiveView, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (isActiveView) NotifyDataChanged();
    }

    public void HandleEndKey()
    {
        _networkAutoScrollEnabled = true;
        BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    public (int filtered, int total) GetFilterCounts()
    {
        return (_networkView.Length, _networkViewTotalCount);
    }

    private void ConfigureLogLists()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstNetworkLogs);
        _lstNetworkLogs.Columns.Add(Language.MethodColumn, 60);
        _lstNetworkLogs.Columns.Add(Language.UrlColumn, 500);
        _lstNetworkLogs.Columns.Add(Language.StatusColumn, 55);
        _lstNetworkLogs.Columns.Add(Language.DurationColumn, 55);
        _lstNetworkLogs.Columns.Add(Language.RequestColumn, 200);
        _lstNetworkLogs.Columns.Add(Language.ResponseColumn, 200);
        _lstNetworkLogs.RetrieveVirtualItem += OnNetworkLogsRetrieveVirtualItem;
        _lstNetworkLogs.SelectedIndexChanged += OnNetworkLogSelected;
        _lstNetworkLogs.DoubleClick += OnNetworkLogDoubleClick;
        _lstNetworkLogs.MouseUp += OnNetworkLogMouseUp;
        _lstNetworkLogs.MouseWheel += OnNetworkLogsMouseWheel;
        _lstNetworkLogs.ContextMenuStrip = CreateNetworkLogMenu();
    }

    private LogEntry? GetNetworkLogEntryByViewIndex(int index)
    {
        return index >= 0 && index < _networkView.Length ? _networkView[index] : null;
    }

    private void OnNetworkLogsRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var entry = GetNetworkLogEntryByViewIndex(e.ItemIndex);
        e.Item = entry == null ? new ListViewItem() : CreateNetworkLogItem(entry);
    }

    private ListViewItem CreateNetworkLogItem(LogEntry entry)
    {
        var item = new ListViewItem(entry.Method ?? string.Empty);
        item.SubItems.Add(entry.UrlPath);
        item.SubItems.Add(entry.Code.ToString());
        item.SubItems.Add(entry.Duration + "ms");
        item.SubItems.Add(entry.SendPreview);
        item.SubItems.Add(entry.ContentPreview);
        item.ForeColor = entry.IsSuccessStatusCode ? Color.Green : Color.Red;
        return item;
    }

    private void OnNetworkLogMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _lstNetworkLogs.HitTest(e.Location);
        if (hit.Item == null) return;
        hit.Item.Selected = true;
        _lstNetworkLogs.ContextMenuStrip?.Show(_lstNetworkLogs.PointToScreen(e.Location));
    }

    private void OnNetworkLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _networkAutoScrollEnabled = false;
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    private void OnNetworkLogSelected(object? sender, EventArgs e)
    {
        if (_lstNetworkLogs.SelectedIndices.Count > 0)
        {
            _networkAutoScrollEnabled = false;
            ScrollStateChanged?.Invoke();
            UpdateLogCount();
        }
        var entry = GetSelectedNetworkEntry();
        LogEntrySelected?.Invoke(entry);
    }

    private void OnNetworkLogDoubleClick(object? sender, EventArgs e)
    {
        var entry = GetSelectedNetworkEntry();
        if (entry != null)
            LogEntryDoubleClicked?.Invoke(entry);
    }

    private LogEntry? GetSelectedNetworkEntry()
    {
        return _lstNetworkLogs.SelectedIndices.Count > 0
            ? GetNetworkLogEntryByViewIndex(_lstNetworkLogs.SelectedIndices[0])
            : null;
    }

    private ContextMenuStrip CreateNetworkLogMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Language.CopyUrl, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            await ClipboardTextHelper.TrySetTextAsync(entry?.Url);
        });
        menu.Items.Add(Language.CopyUrlWithoutDomain, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            await ClipboardTextHelper.TrySetTextAsync(GetUrlWithoutDomain(entry?.Url));
        });
        menu.Items.Add(Language.CopyMethodUrl, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            await ClipboardTextHelper.TrySetTextAsync(entry == null ? null : $"{entry.Method} {entry.Url}".Trim());
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Language.CopyRequestBody, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            await ClipboardTextHelper.TrySetTextAsync(entry?.Send);
        });
        menu.Items.Add(Language.CopyUrlRequestBody, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            string? text = entry == null ? null : await Task.Run(() => FormatUrlWithBody(entry.Url, entry.Send));
            await ClipboardTextHelper.TrySetTextAsync(text);
        });
        menu.Items.Add(Language.CopyResponseBody, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            await ClipboardTextHelper.TrySetTextAsync(entry?.Content);
        });
        menu.Items.Add(Language.CopyUrlResponseBody, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            string? text = entry == null ? null : await Task.Run(() => FormatUrlWithBody(entry.Url, entry.Content));
            await ClipboardTextHelper.TrySetTextAsync(text);
        });
        menu.Items.Add(Language.CopyUrlRequestResponse, null, async (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            string? text = entry == null
                ? null
                : await Task.Run(() => FormatUrlRequestResponse(entry.Url, entry.Send, entry.Content));
            await ClipboardTextHelper.TrySetTextAsync(text);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Language.ViewDetail, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            if (entry != null) LogEntryDoubleClicked?.Invoke(entry);
        });
        return menu;
    }

    private static string FormatUrlWithBody(string? url, string? body)
    {
        var formattedBody = JsonFormatter.FormatJson(body) ?? body ?? "";
        return string.IsNullOrEmpty(formattedBody)
            ? url ?? ""
            : $"{url ?? ""}{Environment.NewLine}{formattedBody}";
    }

    private static string GetUrlWithoutDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.PathAndQuery + uri.Fragment
            : url;
    }

    private static string FormatUrlRequestResponse(string? url, string? requestBody, string? responseBody)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(url)) parts.Add(url);

        var formattedRequest = JsonFormatter.FormatJson(requestBody) ?? requestBody ?? string.Empty;
        if (!string.IsNullOrEmpty(formattedRequest)) parts.Add(formattedRequest);

        var formattedResponse = JsonFormatter.FormatJson(responseBody) ?? responseBody ?? string.Empty;
        if (!string.IsNullOrEmpty(formattedResponse)) parts.Add(formattedResponse);
        return string.Join(Environment.NewLine, parts);
    }

    private void RequestQueryRefresh(int debounceMs)
    {
        if (_isClosing || IsDisposed) return;
        _queryVersion++;
        _isDirty = true;
        CancelNetworkRefreshDelay();
        CancelNetworkFilter();
        if (_isActive) ScheduleNetworkRefresh(debounceMs);
    }

    private void ScheduleNetworkRefresh(int debounceMs)
    {
        if (!_isActive || !_isDirty || _isClosing || IsDisposed || !IsHandleCreated) return;
        if (_filterCts != null || _refreshDebounceCts != null) return;
        if (debounceMs <= 0)
        {
            StartNetworkSnapshotRefresh();
            return;
        }

        var cts = new CancellationTokenSource();
        _refreshDebounceCts = cts;
        _ = DelayNetworkRefreshAsync(debounceMs, cts);
    }

    private async Task DelayNetworkRefreshAsync(int debounceMs, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(debounceMs, cts.Token).ConfigureAwait(false);
            await PostToUiWithRetryAsync(() =>
            {
                if (!ReferenceEquals(_refreshDebounceCts, cts)) return;
                _refreshDebounceCts.Dispose();
                _refreshDebounceCts = null;
                StartNetworkSnapshotRefresh();
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartNetworkSnapshotRefresh()
    {
        if (!_isActive || !_isDirty || _isClosing || IsDisposed || !IsHandleCreated || _filterCts != null)
            return;

        var source = CaptureCurrentLogSnapshot();
        var query = CaptureNetworkQuery();
        var queryVersion = _queryVersion;
        var dataVersion = _dataVersion;
        _isDirty = false;

        var cts = new CancellationTokenSource();
        _filterCts = cts;
        _ = FilterNetworkSnapshotAsync(source, query, queryVersion, dataVersion, cts);
    }

    private LogEntry[] CaptureCurrentLogSnapshot()
    {
        var buffer = GetCurrentLogBuffer();
        var snapshot = new LogEntry[buffer.Count];
        for (var i = 0; i < snapshot.Length; i++) snapshot[i] = buffer.Get(i);
        return snapshot;
    }

    private NetworkLogQuery CaptureNetworkQuery()
    {
        var keyword = _networkFilterPanel.Keyword;
        var regex = _networkFilterPanel.RegexMode ? _networkFilterPanel.CachedRegex : null;
        return new NetworkLogQuery(
            keyword,
            regex,
            NormalizeNetworkFilterValue(_networkFilterPanel.Filter1Value),
            NormalizeNetworkFilterValue(_networkFilterPanel.Filter2Value));
    }

    private async Task FilterNetworkSnapshotAsync(
        LogEntry[] source,
        NetworkLogQuery query,
        int queryVersion,
        int dataVersion,
        CancellationTokenSource cts)
    {
        try
        {
            var token = cts.Token;
            var view = await Task.Run(
                () => BuildNetworkView(source, query, token), token).ConfigureAwait(false);
            await PostToUiWithRetryAsync(
                () => CompleteNetworkSnapshotRefresh(source.Length, view, queryVersion, dataVersion, cts),
                token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await PostToUiWithRetryAsync(() => CompleteNetworkSnapshotFailure(cts), CancellationToken.None);
        }
    }

    private static LogEntry[] BuildNetworkView(
        LogEntry[] source,
        NetworkLogQuery query,
        CancellationToken token)
    {
        if (!query.FilterActive) return source;

        var filtered = new List<LogEntry>(source.Length);
        foreach (var entry in source)
        {
            token.ThrowIfCancellationRequested();
            if (MatchesNetworkFilter(entry, query)) filtered.Add(entry);
        }

        return filtered.ToArray();
    }

    private void CompleteNetworkSnapshotRefresh(
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

        ApplyNetworkSnapshot(view, totalCount);
        if (dataVersion != _dataVersion) _isDirty = true;
        if (_isDirty) ScheduleNetworkRefresh(DataRefreshDebounceMs);
    }

    private void CompleteNetworkSnapshotFailure(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_filterCts, cts)) return;
        _filterCts.Dispose();
        _filterCts = null;
        _isDirty = true;
        ScheduleNetworkRefresh(DataRefreshDebounceMs);
    }

    private void ApplyNetworkSnapshot(LogEntry[] view, int totalCount)
    {
        var hadItems = _lstNetworkLogs.VirtualListSize > 0;
        var wasAtBottom = BufferedListViewHelper.IsAtBottom(_lstNetworkLogs);
        var followBottom = _networkAutoScrollEnabled && wasAtBottom;
        var anchorIndex = followBottom ? -1 : BufferedListViewHelper.GetTopIndexExact(_lstNetworkLogs);
        var anchorEntry = anchorIndex >= 0 && anchorIndex < _networkView.Length
            ? _networkView[anchorIndex]
            : null;
        if (hadItems && !wasAtBottom) _networkAutoScrollEnabled = false;

        if (followBottom)
        {
            _lstNetworkLogs.SelectedIndices.Clear();
            _lstNetworkLogs.FocusedItem = null;
        }

        _networkView = view;
        _networkViewTotalCount = totalCount;
        _lstNetworkLogs.VirtualListSize = view.Length;

        if (followBottom)
            BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        else
            BufferedListViewHelper.RestoreTopIndexExact(
                _lstNetworkLogs, ResolveNetworkAnchorIndex(view, anchorEntry, anchorIndex));

        _lstNetworkLogs.Invalidate();
        UpdateLogCount();
    }

    private static int ResolveNetworkAnchorIndex(
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

    private void ClearNetworkView()
    {
        _networkView = Array.Empty<LogEntry>();
        _networkViewTotalCount = 0;
        if (!IsHandleCreated) return;
        _lstNetworkLogs.SelectedIndices.Clear();
        _lstNetworkLogs.FocusedItem = null;
        _lstNetworkLogs.VirtualListSize = 0;
        _lstNetworkLogs.Invalidate();
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

    private void CancelNetworkRefreshDelay()
    {
        var cts = _refreshDebounceCts;
        _refreshDebounceCts = null;
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
    }

    private void CancelNetworkFilter()
    {
        var cts = _filterCts;
        _filterCts = null;
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
    }

    private static bool MatchesNetworkFilter(LogEntry entry, NetworkLogQuery query)
    {
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            if (query.Regex != null)
            {
                try
                {
                    if (!(query.Regex.IsMatch(entry.Url ?? "") ||
                          query.Regex.IsMatch(entry.Method ?? "") ||
                          query.Regex.IsMatch(entry.Code.ToString()) ||
                          query.Regex.IsMatch(entry.Duration.ToString()) ||
                          query.Regex.IsMatch(entry.Headers ?? "") ||
                          query.Regex.IsMatch(entry.Send ?? "") ||
                          query.Regex.IsMatch(entry.Content ?? "") ||
                          query.Regex.IsMatch(entry.Message ?? "")))
                        return false;
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }
            else if (!(entry.Url?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
                       entry.Method?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
                       entry.Code.ToString().Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                       entry.Duration.ToString().Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                       entry.Headers?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
                       entry.Send?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
                       entry.Content?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true ||
                       entry.Message?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) == true))
            {
                return false;
            }
        }

        if (query.Method != null &&
            !string.Equals(entry.Method, query.Method, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.StatusFilter == "0") return entry.Code == 0;
        if (query.StatusFilter != null)
        {
            var codeText = entry.Code.ToString();
            return codeText.Length > 0 && codeText[0] == query.StatusFilter[0];
        }

        return true;
    }

    private static string? NormalizeNetworkFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, Language.All, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private void OnNetworkFilterChanged(object? sender, EventArgs e)
    {
        RequestQueryRefresh(FilterInputDebounceMs);
    }

    private void RefreshNetworkVisibleRows()
    {
        if (_lstNetworkLogs.VirtualListSize <= 0)
        {
            _lstNetworkLogs.Invalidate();
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstNetworkLogs);
        var bottomIndex = Math.Min(_lstNetworkLogs.VirtualListSize - 1,
            topIndex + BufferedListViewHelper.GetApproxVisibleRowCount(_lstNetworkLogs) - 1);
        if (bottomIndex < topIndex)
        {
            _lstNetworkLogs.Invalidate();
            return;
        }

        try { _lstNetworkLogs.RedrawItems(topIndex, bottomIndex, false); }
        catch { _lstNetworkLogs.Invalidate(); }
    }

    private void UpdateLogCount()
    {
        var total = _networkViewTotalCount;
        var filtered = _networkView.Length;
        var max = _getCurrentDeviceId() == null ? _settings.MaxLogEntriesAll : _settings.MaxLogEntriesPerDevice;
        var pct = max > 0 ? (double)total / max : 0;
        var countText = Language.LogsCount(filtered, total);
        var isPaused = !(_networkAutoScrollEnabled && BufferedListViewHelper.IsAtBottom(_lstNetworkLogs));
        _lblLogCount.Text = Language.LogsCountWithMax(countText, max, isPaused);
        _lblLogCount.ForeColor = pct >= 1.0 ? Color.Red : pct >= 0.8 ? Color.Orange : DefaultForeColor;
        _btnScrollToBottom.BackColor = _networkAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
        LogCountChanged?.Invoke();
    }

    private void OnScrollToTopClick(object? sender, EventArgs e)
    {
        _networkAutoScrollEnabled = false;
        BufferedListViewHelper.ScrollToTop(_lstNetworkLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    private void OnScrollToBottomClick(object? sender, EventArgs e)
    {
        _networkAutoScrollEnabled = true;
        _lstNetworkLogs.SelectedIndices.Clear();
        BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_isActive && _isDirty) ScheduleNetworkRefresh(0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isClosing = true;
            CancelNetworkRefreshDelay();
            CancelNetworkFilter();
        }

        base.Dispose(disposing);
    }
}
