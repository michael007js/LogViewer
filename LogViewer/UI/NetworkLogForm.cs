using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class NetworkLogForm : Form
{
    private bool _networkRefreshScheduled;
    private bool _networkRefreshNeedsFullFilter;
    private List<int> _filteredNetworkIndices = new();
    private bool _networkAutoScrollEnabled = true;

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
        _filteredNetworkIndices.Clear();
        RefreshNetworkLogList();
        UpdateLogCount();
    }

    public void RebuildFilter()
    {
        RefreshNetworkFilter();
    }

    public void OnLogAdded(LogEntry entry, bool isActiveView, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (!isActiveView) return;

        var incrementalUpdated = TryAppendNetworkLogIncrementally(entry, bufferCountBeforeAdd, bufferWasFull);
        if (_networkAutoScrollEnabled)
        {
            var wasAtBottom = BufferedListViewHelper.IsAtBottom(_lstNetworkLogs);
            if (!incrementalUpdated)
            {
                RefreshNetworkFilter();
            }
            else
            {
                RefreshNetworkLogList();
                UpdateLogCount();
            }

            if (wasAtBottom) BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        }
        else
        {
            _networkRefreshNeedsFullFilter |= !incrementalUpdated;
            ScheduleNetworkRefresh();
        }
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
        var buf = GetCurrentLogBuffer();
        return (_filteredNetworkIndices.Count, buf.Count);
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
        if (index < 0 || index >= _filteredNetworkIndices.Count) return null;
        var buf = GetCurrentLogBuffer();
        return buf.Get(_filteredNetworkIndices[index]);
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
        menu.Items.Add(Language.CopyUrl, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Url);
        });
        menu.Items.Add(Language.CopyMethodUrl, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : $"{entry.Method} {entry.Url}".Trim());
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Language.CopyRequestBody, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Send);
        });
        menu.Items.Add(Language.CopyUrlRequestBody, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : FormatUrlWithBody(entry.Url, entry.Send));
        });
        menu.Items.Add(Language.CopyResponseBody, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Content);
        });
        menu.Items.Add(Language.CopyUrlResponseBody, null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : FormatUrlWithBody(entry.Url, entry.Content));
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

    private void RefreshNetworkLogList()
    {
        var anchorIndex = _networkAutoScrollEnabled ? -1 : BufferedListViewHelper.GetTopIndexExact(_lstNetworkLogs);
        _lstNetworkLogs.VirtualListSize = _filteredNetworkIndices.Count;
        if (_networkAutoScrollEnabled)
        {
            BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        }
        else
        {
            BufferedListViewHelper.RestoreTopIndexExact(_lstNetworkLogs, anchorIndex);
            RefreshNetworkVisibleRows();
            return;
        }

        _lstNetworkLogs.Invalidate();
    }

    private void RefreshNetworkFilter()
    {
        var buf = GetCurrentLogBuffer();
        _filteredNetworkIndices.Clear();
        for (int i = 0; i < buf.Count; i++)
        {
            if (MatchesNetworkFilter(buf.Get(i)))
                _filteredNetworkIndices.Add(i);
        }

        RefreshNetworkLogList();
        UpdateLogCount();
    }

    private bool TryAppendNetworkLogIncrementally(LogEntry entry, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (bufferWasFull) return false;
        if (MatchesNetworkFilter(entry))
            _filteredNetworkIndices.Add(bufferCountBeforeAdd);
        return true;
    }

    private void ScheduleNetworkRefresh(int debounceMs = 80)
    {
        if (_networkRefreshScheduled || !IsHandleCreated || IsDisposed) return;
        _networkRefreshScheduled = true;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(debounceMs).ConfigureAwait(false); } catch { }
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                _networkRefreshScheduled = false;
                if (IsDisposed) return;
                if (_networkRefreshNeedsFullFilter)
                {
                    _networkRefreshNeedsFullFilter = false;
                    RefreshNetworkFilter();
                    return;
                }

                RefreshNetworkLogList();
                UpdateLogCount();
            }));
        });
    }

    private bool MatchesNetworkFilter(LogEntry entry)
    {
        var kw = _networkFilterPanel.Keyword;
        if (!string.IsNullOrEmpty(kw))
        {
            if (_networkFilterPanel.RegexMode && _networkFilterPanel.CachedRegex != null)
            {
                if (!(_networkFilterPanel.CachedRegex.IsMatch(entry.Url ?? "") ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Method ?? "") ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Code.ToString()) ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Duration.ToString()) ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Headers ?? "") ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Send ?? "") ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Content ?? "") ||
                      _networkFilterPanel.CachedRegex.IsMatch(entry.Message ?? "")))
                    return false;
            }
            else
            {
                if (!(entry.Url?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Method?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Code.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                      entry.Duration.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                      entry.Headers?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Send?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Content?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Message?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                    return false;
            }
        }

        var method = _networkFilterPanel.Filter1Value;
        if (method != Language.All && !string.IsNullOrEmpty(method) &&
            !string.Equals(entry.Method, method, StringComparison.OrdinalIgnoreCase))
            return false;

        var statusFilter = _networkFilterPanel.Filter2Value;
        if (statusFilter != Language.All && !string.IsNullOrEmpty(statusFilter))
        {
            if (statusFilter == "0" && entry.Code != 0) return false;
            else if (statusFilter != "0")
            {
                var range = statusFilter[0];
                var codeStr = entry.Code.ToString();
                if (codeStr.Length == 0 || codeStr[0] != range) return false;
            }
        }

        return true;
    }

    private void OnNetworkFilterChanged(object? sender, EventArgs e)
    {
        RefreshNetworkFilter();
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
        var buf = GetCurrentLogBuffer();
        var total = buf.Count;
        var filtered = _filteredNetworkIndices.Count;
        var max = _getCurrentDeviceId() == null ? _settings.MaxLogEntriesAll : _settings.MaxLogEntriesPerDevice;
        var pct = (double)total / max;
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
        BufferedListViewHelper.ScrollToBottom(_lstNetworkLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }
}
