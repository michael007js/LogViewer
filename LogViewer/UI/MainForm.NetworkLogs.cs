using System.Text.Json;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// MainForm 的网络日志部分，包含网络日志的配置、过滤、显示和交互逻辑。
/// </summary>
public partial class MainForm
{
    /// <summary>网络日志刷新是否已调度。</summary>
    private bool _networkRefreshScheduled;
    /// <summary>网络日志刷新是否需要全量过滤。</summary>
    private bool _networkRefreshNeedsFullFilter;
    /// <summary>网络日志过滤后的索引列表。</summary>
    private List<int> _filteredNetworkIndices = new();
    /// <summary>网络日志自动滚动是否启用。</summary>
    private bool _networkAutoScrollEnabled = true;

    /// <summary>
    /// 配置网络日志列表视图，设置列和虚拟模式。
    /// </summary>
    private void ConfigureLogLists()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstNetworkLogs);
        _lstNetworkLogs.Columns.Add("Method", 60);
        _lstNetworkLogs.Columns.Add("URL", 300);
        _lstNetworkLogs.Columns.Add("Status", 55);
        _lstNetworkLogs.Columns.Add("Dur", 55);
        _lstNetworkLogs.Columns.Add("Request", 200);
        _lstNetworkLogs.Columns.Add("Response", 200);
        _lstNetworkLogs.RetrieveVirtualItem += OnNetworkLogsRetrieveVirtualItem;
    }

    /// <summary>
    /// 根据视图索引获取网络日志条目。
    /// </summary>
    private LogEntry? GetNetworkLogEntryByViewIndex(int index)
    {
        if (index < 0 || index >= _filteredNetworkIndices.Count) return null;

        var buf = GetCurrentLogBuffer();
        return buf.Get(_filteredNetworkIndices[index]);
    }

    /// <summary>
    /// 虚拟模式下获取列表项的事件处理。
    /// </summary>
    private void OnNetworkLogsRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var entry = GetNetworkLogEntryByViewIndex(e.ItemIndex);
        e.Item = entry == null ? new ListViewItem() : CreateNetworkLogItem(entry);
    }

    /// <summary>
    /// 创建网络日志列表项，设置列值和颜色。
    /// </summary>
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
        ShowNetworkLogMenu(_lstNetworkLogs.PointToScreen(e.Location));
    }

    private void OnNetworkLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _networkAutoScrollEnabled = false;
        UpdateLogCount();
    }

    private void RefreshNetworkLogList()
    {
        var anchorIndex = _networkAutoScrollEnabled ? -1 : BufferedListViewHelper.GetTopIndexExact(_lstNetworkLogs);
        _lstNetworkLogs.VirtualListSize = _filteredNetworkIndices.Count;
        if (_networkAutoScrollEnabled)
        {
            ScrollToBottom(_lstNetworkLogs);
        }
        else
        {
            BufferedListViewHelper.RestoreTopIndexExact(_lstNetworkLogs, anchorIndex);
            RefreshNetworkVisibleRows();
            return;
        }
        _lstNetworkLogs.Invalidate();
    }

    private void OnNetworkLogSelected(object? sender, EventArgs e)
    {
        var entry = GetSelectedNetworkEntry();
        if (ReferenceEquals(_selectedLogEntry, entry)) return;
        _selectedLogEntry = entry;
        ShowLogDetail(entry);
    }

    private void OnNetworkLogDoubleClick(object? sender, EventArgs e)
    {
        var entry = GetSelectedNetworkEntry();
        if (entry != null)
        {
            new JsonDetailForm(entry, _lstNetworkLogs.Font).Show(this);
        }
    }

    private ContextMenuStrip CreateNetworkLogMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy URL", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Url);
        });
        menu.Items.Add("Copy Method + URL", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : $"{entry.Method} {entry.Url}".Trim());
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copy Request Body", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Send);
        });
        menu.Items.Add("Copy URL + Request Body", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : FormatUrlWithBody(entry.Url, entry.Send));
        });
        menu.Items.Add("Copy Response Body", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry?.Content);
        });
        menu.Items.Add("Copy URL + Response Body", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            ClipboardTextHelper.TrySetText(entry == null ? null : FormatUrlWithBody(entry.Url, entry.Content));
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("View Detail", null, (s, e) =>
        {
            var entry = GetSelectedNetworkEntry();
            if (entry != null) new JsonDetailForm(entry, _lstNetworkLogs.Font).Show(this);
        });
        return menu;
    }

    private void ShowNetworkLogMenu(Point screenLocation)
    {
        _lstNetworkLogs.ContextMenuStrip?.Show(screenLocation);
    }

    private static string FormatUrlWithBody(string? url, string? body)
    {
        var formattedBody = JsonFormatter.FormatJson(body) ?? body ?? "";
        return string.IsNullOrEmpty(formattedBody)
            ? url ?? ""
            : $"{url ?? ""}{Environment.NewLine}{formattedBody}";
    }

    private LogEntry? GetSelectedNetworkEntry()
    {
        return _lstNetworkLogs.SelectedIndices.Count > 0
            ? GetNetworkLogEntryByViewIndex(_lstNetworkLogs.SelectedIndices[0])
            : null;
    }

    /// <summary>
    /// 显示日志详情到预览面板。
    /// </summary>
    private void ShowLogDetail(LogEntry? entry)
    {
        if (entry == null)
        {
            _jsonHeadersView?.DisplayPlainText("");
            _jsonRequestBodyView?.DisplayPlainText("");
            _jsonResponseBodyView?.DisplayPlainText("");
            _rawHeaders.Text = "";
            _rawRequestBody.Text = "";
            _rawResponseBody.Text = "";
            return;
        }

        if (_settings.AutoFormatJson)
        {
            _jsonHeadersView?.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBodyView?.DisplayJson(entry.Send ?? "");
            _jsonResponseBodyView?.DisplayJson(entry.Content ?? "");
        }
        else
        {
            _jsonHeadersView?.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBodyView?.DisplayPlainText(entry.Send ?? "");
            _jsonResponseBodyView?.DisplayPlainText(entry.Content ?? "");
        }

        _rawHeaders.Text = entry.Headers ?? "";
        _rawRequestBody.Text = JsonFormatter.FormatJson(entry.Send) ?? entry.Send ?? "";
        _rawResponseBody.Text = JsonFormatter.FormatJson(entry.Content) ?? entry.Content ?? "";
    }

    /// <summary>
    /// 刷新网络日志过滤，根据关键字、方法和状态码筛选日志。
    /// </summary>
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
        if (bufferWasFull)
        {
            return false;
        }

        if (MatchesNetworkFilter(entry))
        {
            _filteredNetworkIndices.Add(bufferCountBeforeAdd);
        }
        return true;
    }

    private void ScheduleNetworkRefresh(int debounceMs = 80)
    {
        if (_networkRefreshScheduled || !IsHandleCreated || IsDisposed)
        {
            return;
        }

        _networkRefreshScheduled = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(debounceMs).ConfigureAwait(false);
            }
            catch
            {
            }

            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(() =>
            {
                _networkRefreshScheduled = false;
                if (IsDisposed)
                {
                    return;
                }

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
        var kw = _txtNetworkKeyword.Text.Trim();
        if (!string.IsNullOrEmpty(kw) &&
            !(entry.Url?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
              entry.Method?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
              entry.Code.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase) ||
              entry.Duration.ToString().Contains(kw, StringComparison.OrdinalIgnoreCase) ||
              entry.Headers?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
              entry.Send?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
              entry.Content?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true ||
              entry.Message?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true))
            return false;

        var method = _cmbMethod.SelectedItem as string;
        if (method != Language.All && !string.IsNullOrEmpty(method) && !string.Equals(entry.Method, method, StringComparison.OrdinalIgnoreCase))
            return false;

        var statusFilter = _cmbStatusCode.SelectedItem as string;
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

    /// <summary>
    /// 刷新网络日志可见行，优化性能只重绘可见区域。
    /// </summary>
    private void RefreshNetworkVisibleRows()
    {
        if (_lstNetworkLogs.VirtualListSize <= 0)
        {
            _lstNetworkLogs.Invalidate();
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstNetworkLogs);
        var bottomIndex = Math.Min(_lstNetworkLogs.VirtualListSize - 1, topIndex + GetApproxVisibleRowCount(_lstNetworkLogs) - 1);
        if (bottomIndex < topIndex)
        {
            _lstNetworkLogs.Invalidate();
            return;
        }

        try { _lstNetworkLogs.RedrawItems(topIndex, bottomIndex, false); } catch { _lstNetworkLogs.Invalidate(); }
    }

    private void OnExportJson(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "network_logs.json" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var buf = GetCurrentLogBuffer();
        var entries = new List<LogEntry>();
        for (int i = 0; i < buf.Count; i++) entries.Add(buf.Get(i));
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dlg.FileName, json);
    }

    private void OnExportTxt(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "Text|*.txt", FileName = "network_logs.txt" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var buf = GetCurrentLogBuffer();
        using var writer = new StreamWriter(dlg.FileName);
        for (int i = 0; i < buf.Count; i++)
        {
            var entry = buf.Get(i);
            writer.WriteLine($"--- Log #{i + 1} ---");
            writer.WriteLine($"Method: {entry.Method}");
            writer.WriteLine($"URL: {entry.Url}");
            writer.WriteLine($"Code: {entry.Code}");
            writer.WriteLine($"Duration: {entry.Duration}ms");
            writer.WriteLine($"Successful: {entry.IsSuccessStatusCode}");
            if (!string.IsNullOrEmpty(entry.Send)) writer.WriteLine($"Request Body: {entry.Send}");
            if (!string.IsNullOrEmpty(entry.Content)) writer.WriteLine($"Response: {entry.Content}");
            writer.WriteLine();
        }
    }
}
