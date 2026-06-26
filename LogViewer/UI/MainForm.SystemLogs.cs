using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// MainForm 的系统日志部分，包含系统日志的显示、过滤和管理逻辑。
/// </summary>
public partial class MainForm
{
    /// <summary>系统日志批量刷新大小。</summary>
    private const int SystemLogFlushBatchSize = 200;

    /// <summary>系统日志 UI 刷新防抖间隔（毫秒）。</summary>
    private const int SystemLogUiRefreshDebounceMs = 80;

    /// <summary>
    /// 系统日志查询参数，封装当前 scope 和过滤条件用于快照构建。
    /// </summary>
    private readonly record struct SystemLogQuery(
        /// <summary>设备 ID 过滤，null 表示全部设备。</summary>
        string? DeviceId,
        /// <summary>关键字过滤。</summary>
        string Keyword,
        /// <summary>日志级别过滤，null 表示全部级别。</summary>
        string? Level,
        /// <summary>标签过滤，null 表示全部标签。</summary>
        string? Tag,
        /// <summary>最大包含的序列号，Pause 时冻结到此值。</summary>
        long MaxSequenceInclusive,
        /// <summary>存储结构版本号，用于检测存储是否发生重建。</summary>
        long StoreStructureVersion,
        /// <summary>是否启用正则模式匹配关键字。</summary>
        bool IsRegex)
    {
        /// <summary>是否有非关键字过滤条件（级别或标签）激活。</summary>
        public bool FilterActive =>
            !string.IsNullOrEmpty(Keyword) ||
            !string.IsNullOrEmpty(Level) ||
            !string.IsNullOrEmpty(Tag);

        /// <summary>关键字过滤是否激活。</summary>
        public bool KeywordActive => !string.IsNullOrEmpty(Keyword);
    }

    /// <summary>系统日志会话存储，负责 jsonl 追加写入、索引和热缓存。</summary>
    private SystemLogSessionStore _systemLogStore = null!;

    /// <summary>系统日志当前快照，驱动 VirtualMode ListView 渲染。</summary>
    private SystemLogSnapshot _systemLogSnapshot = SystemLogSnapshot.Empty;

    /// <summary>快照构建的取消令牌源，用于取消进行中的快照构建。</summary>
    private CancellationTokenSource? _systemSnapshotCts;

    /// <summary>预取操作的取消令牌源，用于取消进行中的预取。</summary>
    private CancellationTokenSource? _systemPrefetchCts;

    /// <summary>快照版本号，用于检测快照是否过期。</summary>
    private int _systemSnapshotVersion;

    /// <summary>系统日志是否暂停，暂停后继续采集但冻结视图。</summary>
    private bool _systemLogPaused;

    /// <summary>暂停时冻结的序列号，视图只显示到此序列号为止。</summary>
    private long _systemFreezeSequenceId;

    /// <summary>暂停期间累计的待处理日志数量。</summary>
    private long _systemPausedBacklog;

    /// <summary>是否有待处理的可见行刷新请求。</summary>
    private bool _systemVisibleRefreshPending;

    /// <summary>右键菜单关联的序列号，优先于 SelectedIndices 获取选中项。</summary>
    private long _systemContextSequenceId;

    /// <summary>系统日志 UI 刷新是否已调度。</summary>
    private bool _systemUiRefreshScheduled;

    /// <summary>系统日志 UI 刷新是否需要重建快照。</summary>
    private bool _systemUiRefreshNeedsSnapshot;

    /// <summary>
    /// 初始化系统日志运行时，创建会话存储。
    /// </summary>
    private void InitializeSystemLogRuntime()
    {
        _systemLogStore = new SystemLogSessionStore(_settings.MaxSystemLogEntries);
        _systemFreezeSequenceId = _systemLogStore.LastSequenceId;
    }

    /// <summary>
    /// 配置系统日志列表视图，设置列和虚拟模式。
    /// </summary>
    private void ConfigureSystemLogList()
    {
        BufferedListViewHelper.EnableDoubleBuffer(_lstSystemLogs);
        _lstSystemLogs.Columns.Add(Language.TimeColumn, 90);
        _lstSystemLogs.Columns.Add(Language.LevelColumn, 35);
        _lstSystemLogs.Columns.Add(Language.TagColumn, 120);
        _lstSystemLogs.Columns.Add(Language.MessageColumn, 500);
        _lstSystemLogs.RetrieveVirtualItem += OnSystemLogsRetrieveVirtualItem;
        _lstSystemLogs.CacheVirtualItems += OnSystemLogsCacheVirtualItems;
        _lstSystemLogs.SelectedIndexChanged += OnSystemLogSelectedIndexChanged;
        _lstSystemLogs.MouseUp += OnSystemLogMouseUp;
    }

    /// <summary>
    /// 虚拟模式下获取系统日志列表项的事件处理，通过快照索引获取条目并创建 ListViewItem。
    /// </summary>
    private void OnSystemLogsRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var entry = GetCurrentSystemLogEntry(e.ItemIndex);
        e.Item = entry == null ? new ListViewItem() : CreateSystemLogItem(entry);
    }

    /// <summary>
    /// 虚拟项缓存请求事件处理，调度可见区域预取以优化渲染性能。
    /// </summary>
    private void OnSystemLogsCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
    {
        ScheduleSystemLogPrefetch(e.StartIndex, e.EndIndex);
    }

    /// <summary>
    /// 系统日志选中项变化事件处理，重置右键菜单关联的序列号。
    /// </summary>
    private void OnSystemLogSelectedIndexChanged(object? sender, EventArgs e)
    {
        _systemContextSequenceId = 0;
    }

    /// <summary>
    /// 创建系统日志 ListViewItem，设置时间、级别、标签、消息列值和级别着色。
    /// </summary>
    /// <param name="entry">系统日志条目。</param>
    /// <returns>配置好的 ListViewItem。</returns>
    private ListViewItem CreateSystemLogItem(SystemLogEntry entry)
    {
        var item = new ListViewItem(entry.Timestamp.ToString("HH:mm:ss.fff"));
        item.SubItems.Add(entry.LevelShort);
        item.SubItems.Add(entry.Tag ?? string.Empty);
        item.SubItems.Add(entry.Message ?? string.Empty);
        item.ForeColor = entry.LevelColor;
        return item;
    }

    /// <summary>
    /// 刷新系统日志列表，请求快照刷新。可选择后台线程构建快照。
    /// </summary>
    /// <param name="preferBackground">是否优先在后台线程构建快照。</param>
    private void RefreshSystemLogList(bool preferBackground = false)
    {
        RequestSystemSnapshotRefresh(preferBackground: preferBackground);
    }

    /// <summary>
    /// 请求系统日志快照刷新，先尝试增量追赶，失败则后台构建新快照。支持防抖延迟。
    /// </summary>
    /// <param name="debounceMs">防抖延迟毫秒数，0 表示立即执行。</param>
    /// <param name="preferBackground">是否优先在后台线程构建快照。</param>
    private void RequestSystemSnapshotRefresh(int debounceMs = 0, bool preferBackground = false)
    {
        if (!IsSystemLogRuntimeReady())
        {
            return;
        }

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
                if (debounceMs > 0)
                {
                    await Task.Delay(debounceMs, token);
                }

                var snapshot = await BuildSystemLogSnapshotAsync(query, token).ConfigureAwait(false);
                if (token.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    if (token.IsCancellationRequested || IsDisposed || version != _systemSnapshotVersion)
                    {
                        return;
                    }

                    _systemLogSnapshot = snapshot;
                    ApplySystemSnapshot();
                }));
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    /// <summary>
    /// 异步构建系统日志快照，根据查询条件从会话存储获取记录并过滤。
    /// </summary>
    /// <param name="query">查询参数。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>构建完成的快照。</returns>
    private async Task<SystemLogSnapshot> BuildSystemLogSnapshotAsync(SystemLogQuery query, CancellationToken token)
    {
        var baseRecords = _systemLogStore.CopyActiveRecords(query.DeviceId, query.MaxSequenceInclusive);
        token.ThrowIfCancellationRequested();

        if (!query.FilterActive)
        {
            return SystemLogSnapshot.Create(
                query.DeviceId,
                query.Keyword,
                query.Level,
                query.Tag,
                query.StoreStructureVersion,
                query.MaxSequenceInclusive,
                false,
                baseRecords);
        }

        var filtered = new List<SystemLogRecordRef>(baseRecords.Count);
        if (!query.KeywordActive)
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (MatchesSystemRecord(record, query))
                {
                    filtered.Add(record);
                }
            }
        }
        else if (query.IsRegex)
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesSystemRecord(record, query, includeKeyword: false))
                {
                    continue;
                }

                if (_systemFilterPanel.CachedRegex != null && await _systemLogStore.MatchesKeywordAsync(record, query.Keyword, token, _systemFilterPanel.CachedRegex).ConfigureAwait(false))
                {
                    filtered.Add(record);
                }
            }
        }
        else
        {
            foreach (var record in baseRecords)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesSystemRecord(record, query, includeKeyword: false))
                {
                    continue;
                }

                if (await _systemLogStore.MatchesKeywordAsync(record, query.Keyword, token).ConfigureAwait(false))
                {
                    filtered.Add(record);
                }
            }
        }

        return SystemLogSnapshot.Create(
            query.DeviceId,
            query.Keyword,
            query.Level,
            query.Tag,
            query.StoreStructureVersion,
            query.MaxSequenceInclusive,
            true,
            filtered);
    }

    /// <summary>
    /// 尝试增量追赶当前快照，避免全量重建。仅当查询条件和存储版本匹配且无关键字过滤时有效。
    /// </summary>
    /// <param name="query">当前查询参数。</param>
    /// <returns>增量追赶成功返回 true；需要全量重建返回 false。</returns>
    private bool TryCatchUpCurrentSnapshot(SystemLogQuery query)
    {
        if (!_systemLogSnapshot.MatchesQuery(query.DeviceId, query.Keyword, query.Level, query.Tag) ||
            _systemLogSnapshot.StoreStructureVersion != query.StoreStructureVersion)
        {
            return false;
        }

        if (_systemLogSnapshot.MaxSequenceInclusive > query.MaxSequenceInclusive)
        {
            return false;
        }

        if (_systemLogSnapshot.MaxSequenceInclusive >= query.MaxSequenceInclusive)
        {
            return true;
        }

        if (query.KeywordActive)
        {
            return false;
        }

        var newRecords = _systemLogStore.CopyActiveRecordsSince(
            query.DeviceId,
            _systemLogSnapshot.MaxSequenceInclusive,
            query.MaxSequenceInclusive);

        foreach (var record in newRecords)
        {
            if (_systemLogSnapshot.FilterActive)
            {
                _systemLogSnapshot.Observe(record);
                if (MatchesSystemRecord(record, query))
                {
                    _systemLogSnapshot.Append(record);
                }
            }
            else
            {
                _systemLogSnapshot.Append(record);
            }
        }

        return true;
    }

    /// <summary>
    /// 将当前快照应用到 ListView：设置 VirtualListSize、恢复锚点或滚动到底部、刷新可见区域。
    /// </summary>
    private void ApplySystemSnapshot()
    {
        if (!IsSystemLogRuntimeReady())
        {
            return;
        }

        var anchorIndex = _systemAutoScrollEnabled || !_showingSystemLog
            ? -1
            : BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);

        _lstSystemLogs.VirtualListSize = _systemLogSnapshot.Count;

        if (_showingSystemLog && _systemAutoScrollEnabled)
        {
            ScrollToBottom(_lstSystemLogs);
        }
        else if (anchorIndex >= 0)
        {
            BufferedListViewHelper.RestoreTopIndexExact(_lstSystemLogs, anchorIndex);
        }

        _lstSystemLogs.Invalidate();
        UpdateSystemTagOptionsFromSnapshot();
        ScheduleVisibleSystemPrefetch();
        UpdateSystemLogUiState();
    }

    /// <summary>
    /// 请求系统日志可见行刷新，通过 BeginInvoke 调度到 UI 线程。
    /// </summary>
    private void RequestSystemVisibleRefresh()
    {
        if (!IsHandleCreated || IsDisposed || _systemVisibleRefreshPending)
        {
            return;
        }

        _systemVisibleRefreshPending = true;
        BeginInvoke(new Action(() =>
        {
            _systemVisibleRefreshPending = false;
            if (IsDisposed || !IsSystemLogRuntimeReady())
            {
                return;
            }

            RefreshSystemVisibleRows();
        }));
    }

    /// <summary>
    /// 刷新系统日志可见行，只重绘当前可见区域以优化性能。
    /// </summary>
    private void RefreshSystemVisibleRows()
    {
        if (_lstSystemLogs.VirtualListSize <= 0)
        {
            _lstSystemLogs.Invalidate();
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);
        var visibleCount = GetApproxVisibleRowCount(_lstSystemLogs);
        var bottomIndex = Math.Min(_lstSystemLogs.VirtualListSize - 1, topIndex + visibleCount - 1);
        if (bottomIndex < topIndex)
        {
            _lstSystemLogs.Invalidate();
            return;
        }

        try
        {
            _lstSystemLogs.RedrawItems(topIndex, bottomIndex, false);
        }
        catch
        {
            _lstSystemLogs.Invalidate();
        }
    }

    /// <summary>
    /// 系统日志接收事件处理，将条目加入待处理队列并调度批量刷新。
    /// </summary>
    private void OnSystemLogReceived(object? sender, SystemLogEntry entry)
    {
        lock (_pendingSystemLogsLock)
        {
            _pendingSystemLogs.Enqueue(entry);
            if (_systemLogFlushScheduled)
            {
                return;
            }

            _systemLogFlushScheduled = true;
        }

        _ = Task.Run(FlushPendingSystemLogsAsync);
    }

    /// <summary>
    /// 异步批量刷新待处理系统日志，分批从队列取出条目追加到存储，并调度 UI 刷新。
    /// Pause 时累计 backlog 而不更新视图。
    /// </summary>
    private async Task FlushPendingSystemLogsAsync()
    {
        while (true)
        {
            var entries = new List<SystemLogEntry>();
            lock (_pendingSystemLogsLock)
            {
                while (_pendingSystemLogs.Count > 0 && entries.Count < SystemLogFlushBatchSize)
                {
                    entries.Add(_pendingSystemLogs.Dequeue());
                }

                if (entries.Count == 0)
                {
                    _systemLogFlushScheduled = false;
                    return;
                }
            }

            if (!IsSystemLogRuntimeReady())
            {
                continue;
            }

            var selectedDeviceId = _currentDeviceId;
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
                {
                    continue;
                }

                scopeChanged = true;
                if (_systemLogPaused)
                {
                    Interlocked.Increment(ref _systemPausedBacklog);
                }
            }

            if (scopeChanged || _systemLogPaused)
            {
                ScheduleSystemUiRefresh(scopeChanged && !_systemLogPaused);
            }
        }
    }

    /// <summary>
    /// 调度系统日志 UI 刷新，防抖 80ms。根据需要决定是重建快照还是仅更新 UI 状态。
    /// </summary>
    /// <param name="needSnapshot">是否需要重建快照。</param>
    private void ScheduleSystemUiRefresh(bool needSnapshot)
    {
        if (needSnapshot)
        {
            _systemUiRefreshNeedsSnapshot = true;
        }

        if (_systemUiRefreshScheduled || !IsHandleCreated || IsDisposed)
        {
            return;
        }

        _systemUiRefreshScheduled = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SystemLogUiRefreshDebounceMs).ConfigureAwait(false);
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
                _systemUiRefreshScheduled = false;
                if (IsDisposed || !IsSystemLogRuntimeReady())
                {
                    return;
                }

                var needRefreshSnapshot = _systemUiRefreshNeedsSnapshot;
                _systemUiRefreshNeedsSnapshot = false;

                if (_systemLogPaused)
                {
                    UpdateSystemLogUiState();
                    return;
                }

                if (needRefreshSnapshot && _showingSystemLog)
                {
                    RequestSystemSnapshotRefresh(preferBackground: true);
                    return;
                }

                UpdateSystemLogUiState();
            }));
        });
    }

    /// <summary>
    /// 系统日志过滤条件变化事件处理，延迟 200ms 后重建快照并更新 UI。
    /// </summary>
    private void OnSystemFilterChanged(object? sender, EventArgs e)
    {
        RequestSystemSnapshotRefresh(200);
        UpdateSystemLogUiState();
    }

    /// <summary>
    /// 刷新系统日志标签选项，从当前快照更新标签下拉列表。
    /// </summary>
    private void RefreshSystemTagOptions()
    {
        UpdateSystemTagOptionsFromSnapshot();
    }

    /// <summary>
    /// 从当前快照更新标签下拉列表选项，仅在列表内容变化时重建。
    /// </summary>
    private void UpdateSystemTagOptionsFromSnapshot()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var selected = _systemFilterPanel.Filter2Value ?? Language.All;
        var ordered =
            _systemLogStore.GetOrderedTags(_systemLogSnapshot.DeviceId, _systemLogSnapshot.MaxSequenceInclusive);
        _systemFilterPanel.UpdateFilter2Items(ordered, selected);
    }

    /// <summary>
    /// 获取当前系统日志快照中的条目数量。
    /// </summary>
    /// <returns>当前快照条目数。</returns>
    private int GetCurrentSystemLogCount()
    {
        return _systemLogSnapshot.Count;
    }

    /// <summary>
    /// 根据视图索引获取系统日志条目，通过快照索引从会话存储读取完整数据。
    /// </summary>
    /// <param name="index">视图索引。</param>
    /// <returns>日志条目；索引越界时返回 null。</returns>
    private SystemLogEntry? GetCurrentSystemLogEntry(int index)
    {
        if (index < 0 || index >= _systemLogSnapshot.Count)
        {
            return null;
        }

        return _systemLogStore.GetDisplayEntry(_systemLogSnapshot.Records[index]);
    }

    /// <summary>
    /// 根据视图索引获取系统日志记录引用，直接从快照读取无需访问存储。
    /// </summary>
    /// <param name="index">视图索引。</param>
    /// <returns>记录引用；索引越界时返回 null。</returns>
    private SystemLogRecordRef? GetCurrentSystemRecord(int index)
    {
        if (index < 0 || index >= _systemLogSnapshot.Count)
        {
            return null;
        }

        return _systemLogSnapshot.Records[index];
    }

    /// <summary>
    /// 系统日志列表鼠标抬起事件处理，右键时选中条目、预热缓存并显示上下文菜单。
    /// </summary>
    private void OnSystemLogMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _lstSystemLogs.HitTest(e.Location);
        if (hit.Item == null)
        {
            return;
        }

        hit.Item.Selected = true;

        var record = GetCurrentSystemRecord(hit.Item.Index);
        if (record == null)
        {
            return;
        }

        _systemContextSequenceId = record.Value.SequenceId;
        _ = WarmSystemEntryAsync(_systemContextSequenceId);
        ShowSystemLogMenu(_lstSystemLogs.PointToScreen(e.Location));
    }

    /// <summary>
    /// 异步预热指定序列号的系统日志条目缓存，加载完成后刷新可见行。
    /// </summary>
    /// <param name="sequenceId">待预热的序列号。</param>
    private async Task WarmSystemEntryAsync(long sequenceId)
    {
        if (sequenceId <= 0)
        {
            return;
        }

        try
        {
            var entry = await _systemLogStore.GetEntryAsync(sequenceId, CancellationToken.None).ConfigureAwait(false);
            if (entry != null)
            {
                RequestSystemVisibleRefresh();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 系统日志列表鼠标滚轮事件处理，禁用自动滚动并更新 UI 状态。
    /// </summary>
    private void OnSystemLogsMouseWheel(object? sender, MouseEventArgs e)
    {
        _systemAutoScrollEnabled = false;
        UpdateSystemLogUiState();
    }

    /// <summary>
    /// 创建系统日志右键上下文菜单，包含复制消息和复制完整行选项。
    /// </summary>
    /// <returns>配置好的上下文菜单。</returns>
    private ContextMenuStrip CreateSystemLogMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Language.CopyMessage, null,
            async (s, e) => { await CopySelectedSystemLogAsync(static entry => entry.Message).ConfigureAwait(false); });
        menu.Items.Add(Language.CopyFullLine, null, async (s, e) =>
        {
            await CopySelectedSystemLogAsync(static entry =>
                    $"{entry.Timestamp:HH:mm:ss.fff} {entry.LevelShort} {entry.Tag} {entry.Message}")
                .ConfigureAwait(false);
        });
        return menu;
    }

    /// <summary>
    /// 异步复制选中系统日志的指定字段到剪贴板，通过序列号从存储异步获取完整条目。
    /// </summary>
    /// <param name="projector">从条目提取待复制文本的投影函数。</param>
    private async Task CopySelectedSystemLogAsync(Func<SystemLogEntry, string?> projector)
    {
        var sequenceId = GetSelectedSystemSequenceId();
        if (sequenceId <= 0)
        {
            return;
        }

        try
        {
            var entry = await _systemLogStore.GetEntryAsync(sequenceId, CancellationToken.None).ConfigureAwait(false);
            var text = entry == null ? null : projector(entry);
            if (string.IsNullOrEmpty(text) || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new Action(() => ClipboardTextHelper.TrySetText(text)));
        }
        catch
        {
        }
    }

    /// <summary>
    /// 在指定屏幕位置显示系统日志上下文菜单。
    /// </summary>
    /// <param name="screenLocation">屏幕坐标位置。</param>
    private void ShowSystemLogMenu(Point screenLocation)
    {
        _lstSystemLogs.ContextMenuStrip?.Show(screenLocation);
    }

    /// <summary>
    /// 获取当前选中的系统日志序列号，优先使用右键菜单关联序列号，否则从 SelectedIndices 读取。
    /// </summary>
    /// <returns>选中条目的序列号；无选中时返回 0。</returns>
    private long GetSelectedSystemSequenceId()
    {
        if (_systemContextSequenceId > 0)
        {
            return _systemContextSequenceId;
        }

        if (_lstSystemLogs.SelectedIndices.Count == 0)
        {
            return 0;
        }

        var selectedIndex = _lstSystemLogs.SelectedIndices[0];
        return selectedIndex >= 0 && selectedIndex < _systemLogSnapshot.Count
            ? _systemLogSnapshot.Records[selectedIndex].SequenceId
            : 0;
    }

    /// <summary>
    /// 系统日志暂停/恢复按钮点击事件处理。暂停时冻结视图序列号，恢复时追平到最新并重建快照。
    /// </summary>
    private void OnSystemPauseResumeClick(object? sender, EventArgs e)
    {
        if (!IsSystemLogRuntimeReady())
        {
            return;
        }

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
    }

    /// <summary>
    /// 更新系统日志 UI 状态：暂停按钮文本、backlog 标签、按钮背景色。
    /// </summary>
    private void UpdateSystemLogUiState()
    {
        _btnSystemPauseResume.Text = _systemLogPaused ? Language.Resume : Language.Pause;
        _lblSystemBacklog.Text = _systemPausedBacklog > 0 ? Language.BufferedCount(_systemPausedBacklog) : string.Empty;
        _lblSystemBacklog.Visible = _systemPausedBacklog > 0;
        _btnSystemPauseResume.BackColor = _systemLogPaused ? Color.LightGoldenrodYellow : DefaultBackColor;
        _btnSystemScrollToBottom.BackColor = _systemAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
    }

    /// <summary>
    /// 捕获当前系统日志查询参数，包含设备、关键字、级别、标签和序列号范围。
    /// </summary>
    /// <returns>当前查询参数结构体。</returns>
    private SystemLogQuery CaptureSystemLogQuery()
    {
        var keyword = _systemFilterPanel.Keyword;
        var level = NormalizeSystemFilterValue(_systemFilterPanel.Filter1Value);
        var tag = NormalizeSystemFilterValue(_systemFilterPanel.Filter2Value);
        var maxSequenceInclusive = _systemLogPaused ? _systemFreezeSequenceId : _systemLogStore.LastSequenceId;

        return new SystemLogQuery(
            _currentDeviceId,
            keyword,
            level,
            tag,
            maxSequenceInclusive,
            _systemLogStore.StructureVersion,
            _systemFilterPanel.RegexMode);
    }

    /// <summary>
    /// 判断系统日志记录是否匹配查询条件（设备、级别、标签），可选跳过关键字匹配。
    /// </summary>
    /// <param name="record">待匹配的记录引用。</param>
    /// <param name="query">查询参数。</param>
    /// <param name="includeKeyword">是否包含关键字匹配，默认 true。</param>
    /// <returns>匹配返回 true。</returns>
    private bool MatchesSystemRecord(SystemLogRecordRef record, SystemLogQuery query, bool includeKeyword = true)
    {
        if (!_systemLogStore.MatchesScope(record, query.DeviceId) ||
            !_systemLogStore.MatchesLevel(record, query.Level) ||
            !_systemLogStore.MatchesTag(record, query.Tag))
        {
            return false;
        }

        return !includeKeyword || !query.KeywordActive;
    }

    /// <summary>
    /// 判断新到达的系统日志条目是否匹配查询条件，包含关键字内容匹配。
    /// </summary>
    /// <param name="entry">待匹配的日志条目。</param>
    /// <param name="query">查询参数。</param>
    /// <returns>匹配返回 true。</returns>
    private bool MatchesIncomingSystemEntry(SystemLogEntry entry, SystemLogQuery query)
    {
        if (!string.IsNullOrEmpty(query.DeviceId) &&
            !string.Equals(entry.SourceDeviceId ?? string.Empty, query.DeviceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.Level) &&
            !string.Equals(entry.LevelShort, query.Level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.Tag) &&
            !string.Equals(entry.Tag ?? string.Empty, query.Tag, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!query.KeywordActive)
        {
            return true;
        }

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

    /// <summary>
    /// 规范化过滤值，将空白或"全部"转为 null 表示不过滤。
    /// </summary>
    /// <param name="value">原始过滤值。</param>
    /// <returns>规范化后的过滤值；不过滤时返回 null。</returns>
    private static string? NormalizeSystemFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, Language.All, StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    /// <summary>
    /// 检查系统日志运行时是否就绪（非设计期且存储已初始化）。
    /// </summary>
    /// <returns>就绪返回 true。</returns>
    private bool IsSystemLogRuntimeReady()
    {
        return !IsDesignTimeMode() && _systemLogStore != null;
    }

    /// <summary>
    /// 估算 ListView 可见行数，用于 VirtualMode 局部刷新和预取范围计算。
    /// </summary>
    /// <param name="listView">目标 ListView。</param>
    /// <returns>近似可见行数，至少为 1。</returns>
    private static int GetApproxVisibleRowCount(ListView listView)
    {
        return Math.Max(1, listView.ClientSize.Height / Math.Max(1, listView.Font.Height + 6));
    }

    /// <summary>
    /// 调度可见区域的系统日志预取，通知存储固定可见序列号并预取前后扩展区域。
    /// </summary>
    private void ScheduleVisibleSystemPrefetch()
    {
        if (!_showingSystemLog || _lstSystemLogs.VirtualListSize <= 0)
        {
            _systemLogStore.UpdatePinnedSequences(Array.Empty<long>());
            return;
        }

        var topIndex = BufferedListViewHelper.GetTopIndexExact(_lstSystemLogs);
        var bottomIndex = Math.Min(_lstSystemLogs.VirtualListSize - 1,
            topIndex + GetApproxVisibleRowCount(_lstSystemLogs) - 1);
        ScheduleSystemLogPrefetch(topIndex, bottomIndex);
    }

    /// <summary>
    /// 调度指定索引范围的系统日志预取，固定可见序列号并异步预取前后扩展区域（前120行后240行）。
    /// 预取完成后刷新可见行以显示加载的数据。
    /// </summary>
    /// <param name="startIndex">起始索引。</param>
    /// <param name="endIndex">结束索引。</param>
    private void ScheduleSystemLogPrefetch(int startIndex, int endIndex)
    {
        if (!IsSystemLogRuntimeReady() || _systemLogSnapshot.Count == 0)
        {
            return;
        }

        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(_systemLogSnapshot.Count - 1, endIndex);
        if (endIndex < startIndex)
        {
            return;
        }

        var visibleSequenceIds = new List<long>(endIndex - startIndex + 1);
        for (var i = startIndex; i <= endIndex; i++)
        {
            visibleSequenceIds.Add(_systemLogSnapshot.Records[i].SequenceId);
        }

        _systemLogStore.UpdatePinnedSequences(visibleSequenceIds);

        var prefetchStart = Math.Max(0, startIndex - 120);
        var prefetchEnd = Math.Min(_systemLogSnapshot.Count - 1, endIndex + 240);
        var prefetchSequenceIds = new List<long>(prefetchEnd - prefetchStart + 1);
        for (var i = prefetchStart; i <= prefetchEnd; i++)
        {
            prefetchSequenceIds.Add(_systemLogSnapshot.Records[i].SequenceId);
        }

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
                {
                    RequestSystemVisibleRefresh();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }
}