using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class NormalLogForm : Form
{
    private bool _normalRefreshScheduled;
    private bool _normalRefreshNeedsFullFilter;
    private List<int> _filteredNormalIndices = new();
    private bool _normalAutoScrollEnabled = true;

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
        _filteredNormalIndices.Clear();
        RefreshNormalLogList();
        UpdateLogCount();
    }

    public void RebuildFilter()
    {
        RefreshNormalFilter();
    }

    public void OnNormalLogAdded(LogEntry entry, bool isActiveView, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (!isActiveView) return;

        var incrementalUpdated = TryAppendNormalLogIncrementally(entry, bufferCountBeforeAdd, bufferWasFull);
        if (_normalAutoScrollEnabled)
        {
            var wasAtBottom = BufferedListViewHelper.IsAtBottom(_lstNormalLogs);
            if (!incrementalUpdated)
            {
                RefreshNormalFilter();
            }
            else
            {
                RefreshNormalLogList();
                UpdateLogCount();
            }

            if (wasAtBottom) BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        }
        else
        {
            _normalRefreshNeedsFullFilter |= !incrementalUpdated;
            ScheduleNormalRefresh();
        }
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
        var buf = GetCurrentNormalLogBuffer();
        return (_filteredNormalIndices.Count, buf.Count);
    }

    private void ConfigureNormalLogList()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstNormalLogs);
        _lstNormalLogs.Columns.Add(Language.TimeColumn, 100);
        _lstNormalLogs.Columns.Add(Language.LevelColumn, 50);
        _lstNormalLogs.Columns.Add(Language.TagColumn, 100);
        _lstNormalLogs.Columns.Add(Language.NormalLogMessageColumn, 500);
        _lstNormalLogs.RetrieveVirtualItem += OnNormalLogsRetrieveVirtualItem;
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
        if (index < 0 || index >= _filteredNormalIndices.Count) return null;
        var buf = GetCurrentNormalLogBuffer();
        return buf.Get(_filteredNormalIndices[index]);
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

    private void RefreshNormalLogList()
    {
        var anchorIndex = _normalAutoScrollEnabled ? -1 : BufferedListViewHelper.GetTopIndexExact(_lstNormalLogs);
        _lstNormalLogs.VirtualListSize = _filteredNormalIndices.Count;
        if (_normalAutoScrollEnabled)
        {
            BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        }
        else
        {
            BufferedListViewHelper.RestoreTopIndexExact(_lstNormalLogs, anchorIndex);
            RefreshNormalVisibleRows();
            return;
        }

        _lstNormalLogs.Invalidate();
    }

    private void RefreshNormalFilter()
    {
        var buf = GetCurrentNormalLogBuffer();
        _filteredNormalIndices.Clear();
        for (int i = 0; i < buf.Count; i++)
        {
            if (MatchesNormalFilter(buf.Get(i)))
                _filteredNormalIndices.Add(i);
        }

        RefreshNormalLogList();
        UpdateLogCount();
    }

    private bool TryAppendNormalLogIncrementally(LogEntry entry, int bufferCountBeforeAdd, bool bufferWasFull)
    {
        if (bufferWasFull) return false;
        if (MatchesNormalFilter(entry))
            _filteredNormalIndices.Add(bufferCountBeforeAdd);
        return true;
    }

    private void ScheduleNormalRefresh(int debounceMs = 80)
    {
        if (_normalRefreshScheduled || !IsHandleCreated || IsDisposed) return;
        _normalRefreshScheduled = true;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(debounceMs).ConfigureAwait(false); } catch { }
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                _normalRefreshScheduled = false;
                if (IsDisposed) return;
                if (_normalRefreshNeedsFullFilter)
                {
                    _normalRefreshNeedsFullFilter = false;
                    RefreshNormalFilter();
                    return;
                }

                RefreshNormalLogList();
                UpdateLogCount();
            }));
        });
    }

    private bool MatchesNormalFilter(LogEntry entry)
    {
        var levelFilter = _normalFilterPanel.Filter1Value;
        if (levelFilter != Language.All && !string.IsNullOrEmpty(levelFilter))
        {
            var filterLevel = LevelFromDisplayText(levelFilter);
            if (filterLevel > 0 && entry.EffectiveLevel != filterLevel)
                return false;
        }

        var kw = _normalFilterPanel.Keyword;
        if (!string.IsNullOrEmpty(kw))
        {
            if (_normalFilterPanel.RegexMode && _normalFilterPanel.CachedRegex != null)
            {
                if (!(_normalFilterPanel.CachedRegex.IsMatch(entry.Message ?? "") ||
                      _normalFilterPanel.CachedRegex.IsMatch(entry.Method ?? "")))
                    return false;
            }
            else
            {
                if (!(entry.Message?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
                      entry.Method?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
                    return false;
            }
        }

        return true;
    }

    private void OnNormalFilterChanged(object? sender, EventArgs e)
    {
        RefreshNormalFilter();
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
        var buf = GetCurrentNormalLogBuffer();
        var total = buf.Count;
        var filtered = _filteredNormalIndices.Count;
        var max = _getCurrentDeviceId() == null ? _settings.MaxNormalLogEntries : _settings.MaxNormalLogEntriesPerDevice;
        var pct = (double)total / max;
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
        BufferedListViewHelper.ScrollToBottom(_lstNormalLogs);
        ScrollStateChanged?.Invoke();
        UpdateLogCount();
    }
}
