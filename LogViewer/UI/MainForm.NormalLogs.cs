using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class MainForm
{
    private bool _normalRefreshScheduled;
    private bool _normalRefreshNeedsFullFilter;
    private List<int> _filteredNormalIndices = new();

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
    }

    private RingBuffer<LogEntry> GetCurrentNormalLogBuffer()
    {
        if (_currentDeviceId == null) return _allNormalLogs;
        return _deviceNormalLogs.TryGetValue(_currentDeviceId, out var buf) ? buf : _allNormalLogs;
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
        2 => "V",
        3 => "D",
        4 => "I",
        5 => "W",
        6 => "E",
        7 => "F",
        _ => "?"
    };

    private static int LevelFromDisplayText(string text) => text switch
    {
        "V" => 2,
        "D" => 3,
        "I" => 4,
        "W" => 5,
        "E" => 6,
        "F" => 7,
        _ => 0
    };

    private ListViewItem CreateNormalLogItem(LogEntry entry)
    {
        var timeStr = entry.SendTime > 0
            ? entry.SendTimeDt.ToString("HH:mm:ss.fff")
            : "";
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
        2 => Color.Gray,
        3 => DefaultForeColor,
        4 => Color.Green,
        5 => Color.Orange,
        6 => Color.Red,
        7 => Color.Red,
        _ => DefaultForeColor
    };

    private void OnNormalLogsDoubleClick(object? sender, EventArgs e)
    {
        var entry = GetSelectedNormalEntry();
        if (entry == null) return;

        var msgPreview = (entry.Message ?? "").Length > 20 ? (entry.Message ?? "")[..20] + "..." : entry.Message ?? "";
        var form = new Form
        {
            Text = $"[{entry.Method ?? ""}] {msgPreview}",
            Size = new Size(800, 500),
            StartPosition = FormStartPosition.CenterParent
        };
        var txt = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Text = entry.Message ?? "",
            Font = new Font("Consolas", _settings.FontSize),
            BackColor = Color.White
        };
        var btnCopy = new Button
        {
            Dock = DockStyle.Bottom,
            Text = Language.CopyNormalLogMessage,
            Height = 30
        };
        btnCopy.Click += (_, _) => ClipboardTextHelper.TrySetText(entry.Message);
        form.Controls.Add(txt);
        form.Controls.Add(btnCopy);
        form.Show(this);
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
            ScrollToBottom(_lstNormalLogs);
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

    private void OnNormalFilterChanged(object? sender, EventArgs e)
    {
        RefreshNormalFilter();
    }

    private void OnNormalLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _normalAutoScrollEnabled = false;
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
            topIndex + GetApproxVisibleRowCount(_lstNormalLogs) - 1);
        if (bottomIndex < topIndex)
        {
            _lstNormalLogs.Invalidate();
            return;
        }

        try { _lstNormalLogs.RedrawItems(topIndex, bottomIndex, false); }
        catch { _lstNormalLogs.Invalidate(); }
    }
}
