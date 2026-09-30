using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Models;
using LogViewer.Network;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class MainForm : Form
{
    private readonly LogServer _server = new();
    private readonly AdbHelper _adbHelper = new();
    private readonly ScrcpyManager _scrcpyManager = new();
    private readonly Dictionary<string, LogcatReader> _logcatReaders = new();
    private AppSettings _settings;
    private Font? _uiFont;
    private CancellationTokenSource? _adbScanCts;
    private readonly SemaphoreSlim _adbScanGate = new(1, 1);
    private int _adbScanVersion;
    private CancellationTokenSource? _scrcpyStartCts;
    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceLogs = new();
    private readonly RingBuffer<LogEntry> _allLogs;
    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceNormalLogs = new();
    private readonly RingBuffer<LogEntry> _allNormalLogs;
    private readonly Dictionary<string, string> _adbSerialToDeviceId = new();
    private readonly Queue<SystemLogEntry> _pendingSystemLogs = new();
    private readonly object _pendingSystemLogsLock = new();
    private bool _systemLogFlushScheduled;
    private bool _adbValidated;
    private string? _currentDeviceId;
    private bool _showingSystemLog;
    private bool _showingNormalLog;
    private LogEntry? _selectedLogEntry;
    private SystemLogSessionStore _systemLogStore = null!;
    private bool _allowFinalClose;
    private Task? _shutdownTask;

    private NetworkLogForm _networkLogForm = null!;
    private NormalLogForm _normalLogForm = null!;
    private SystemLogForm _systemLogForm = null!;

    private System.Windows.Forms.SplitContainer _outerSplit;
    private System.Windows.Forms.SplitContainer _innerSplit;
    private LogViewer.UI.DevicePanel _devicePanel;
    private System.Windows.Forms.TabControl _tabLogType;
    private TabPage _tabNetwork;
    private TabPage _tabNormal;
    private TabPage _tabSystem;
    private JsonDetailToolbar _jsonDetailToolbar;
    private System.Windows.Forms.Panel _jsonHeaders;
    private Panel _jsonRequestBody;
    private Panel _jsonResponseBody;
    private JsonTreeView? _jsonHeadersView;
    private JsonTreeView? _jsonRequestBodyView;
    private JsonTreeView? _jsonResponseBodyView;
    private PagedTextView? _pagedHeadersView;
    private PagedTextView? _pagedRequestBodyView;
    private PagedTextView? _pagedResponseBodyView;
    private System.Windows.Forms.TextBox _rawHeaders;
    private TextBox _rawRequestBody;
    private TextBox _rawResponseBody;
    private System.Windows.Forms.TabControl _tabDetail;
    private TabPage _tabHeaders;
    private TabPage _tabRequestBody;
    private TabPage _tabResponseBody;
    private ToolStrip _toolStrip;
    private ToolStripDropDownButton _btnAdbReverse;
    private ToolStripLabel _lblStatus;
    private MenuStrip _menuStrip;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _lblServerStatus;
    private ToolStripStatusLabel _lblDeviceCountStatus;
    private ToolStripStatusLabel _lblAdbStatus;
    private ToolStripStatusLabel _lblLogcatStatus;
    private System.Windows.Forms.FlowLayoutPanel _pnlBottomBar;
    private Button _btnClear;
    private Button _btnExportJson;
    private Button _btnExportTxt;

    private static void BootLog(string msg)
    {
        var line = $"[BOOT] {msg}";
        Debug.WriteLine(line);
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "boot.log"), $"{DateTime.Now:HH:mm:ss.fff} {line}\n"); } catch { }
    }

    public MainForm()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _settings = AppSettings.Load();
        _allLogs = new RingBuffer<LogEntry>(_settings.MaxLogEntriesAll);
        _allNormalLogs = new RingBuffer<LogEntry>(_settings.MaxNormalLogEntries);
        BootLog($"Settings+Buffers: {sw.ElapsedMilliseconds}");
        InitializeComponent();
        BootLog($"InitializeComponent: {sw.ElapsedMilliseconds}");
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icon.ico"));

        if (IsDesignTimeMode()) return;

        CreateAndEmbedLogForms();
        BootLog($"CreateAndEmbedLogForms: {sw.ElapsedMilliseconds}");
        ApplyLanguage();
        InitializeJsonTreeViewsRuntime();
        BootLog($"Language+JsonViews: {sw.ElapsedMilliseconds}");
        WireComponentEvents();
        ApplySettings();
        BootLog($"Wire+Settings: {sw.ElapsedMilliseconds}");

        if (_adbHelper.IsAdbAvailable())
        {
            Task.Run(() =>
            {
                BootLog("EnsureServerStarted begin");
                _adbHelper.EnsureServerStarted();
                BootLog("EnsureServerStarted done");
                if (IsHandleCreated) BeginInvoke(StartAdbScanLoop);
            });
        }
        else
        {
            Shown += OnMissingAdbPromptShown;
            StartAdbScanLoop();
        }

        AutoStartServer();
        BootLog($"Total: {sw.ElapsedMilliseconds}");

        StartBootDiag();
    }

    private System.Windows.Forms.Timer? _bootDiagTimer;
    private int _bootDiagCount;
    private long _uiBusyMs;
    private System.Diagnostics.Stopwatch _uiBusySw = System.Diagnostics.Stopwatch.StartNew();

    private void StartBootDiag()
    {
        Application.Idle += OnBootUiIdle;
        _bootDiagTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _bootDiagTimer.Tick += (s, e) =>
        {
            _bootDiagCount++;
            var busy = _uiBusySw.ElapsedMilliseconds;
            if (busy > 100) BootLog($"UI-BUSY {busy}ms (tick #{_bootDiagCount})");
            _uiBusySw.Restart();
            if (_bootDiagCount >= 80) { _bootDiagTimer.Stop(); _bootDiagTimer.Dispose(); Application.Idle -= OnBootUiIdle; }
        };
        _bootDiagTimer.Start();
    }

    private void OnBootUiIdle(object? s, EventArgs e)
    {
        _uiBusySw.Restart();
    }

    private void CreateAndEmbedLogForms()
    {
        InitializeSystemLogRuntime();

        _networkLogForm = new NetworkLogForm(_deviceLogs, _allLogs, _settings, () => _currentDeviceId);
        _normalLogForm = new NormalLogForm(_deviceNormalLogs, _allNormalLogs, _settings, () => _currentDeviceId);
        _systemLogForm = new SystemLogForm(_systemLogStore, _settings, () => _currentDeviceId, _adbSerialToDeviceId, () => _showingSystemLog);

        EmbedFormInTab(_networkLogForm, _tabNetwork);
        EmbedFormInTabLazy(_normalLogForm, _tabNormal);
        EmbedFormInTabLazy(_systemLogForm, _tabSystem);
        _networkLogForm.SetActive(_tabLogType.SelectedTab == _tabNetwork);
        _normalLogForm.SetActive(_tabLogType.SelectedTab == _tabNormal);
    }

    /// <summary>
    /// 立即嵌入 Form 到 TabPage（用于默认选中的 Network Tab），Show() 触发 Handle 创建和首布局。
    /// </summary>
    private void EmbedFormInTab(Form form, TabPage tab)
    {
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        tab.Controls.Clear();
        tab.Controls.Add(form);
        form.Show();
    }

    /// <summary>
    /// 延迟嵌入 Form 到 TabPage：仅添加为子控件，不调用 Show()（不创建 Handle）。
    /// 只有当前选中的 Tab 才 Show，其余 Tab 的 Form 在首次切换时由 EnsureFormVisible 触发 Show，
    /// 减少启动时同时创建多个 Form Handle 导致的 UI 线程阻塞。
    /// </summary>
    private void EmbedFormInTabLazy(Form form, TabPage tab)
    {
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        tab.Controls.Clear();
        tab.Controls.Add(form);
        if (tab == _tabLogType.SelectedTab)
            form.Show();
    }

    /// <summary>
    /// 确保 Tab 切换时目标 Form 已创建 Handle（首次切换时触发 Show）。
    /// Show() 只执行一次（IsHandleCreated 检查），后续切换无需重复。
    /// </summary>
    private void EnsureFormVisible(Form? form)
    {
        if (form != null && !form.IsHandleCreated)
            form.Show();
    }

    private void ApplyLanguage()
    {
        Text = Language.AppTitle;
        _toolsMenuItem.Text = Language.ToolsMenu;
        _settingsMenuItem.Text = Language.SettingsMenu;
        _btnAdbReverse.Text = Language.AdbReverse;
        _tabNetwork.Text = Language.NetworkLogs;
        _tabNormal.Text = Language.NormalLogs;
        _tabSystem.Text = Language.SystemLogs;
        _tabHeaders.Text = Language.Headers;
        _tabRequestBody.Text = Language.RequestBody;
        _tabResponseBody.Text = Language.ResponseBody;
        _jsonDetailToolbar.ApplyLanguage();
        _btnClear.Text = Language.Clear;
        _btnExportJson.Text = Language.ExportJson;
        _btnExportTxt.Text = Language.ExportTxt;
        _lblStatus.Text = $"\u25CF {Language.Running}";
        _lblServerStatus.Text = Language.ServerStopped;
        _lblDeviceCountStatus.Text = Language.DevicesCount(0);
        _lblAdbStatus.Text = Language.AdbNotDetected;
        _lblLogcatStatus.Text = Language.LogcatCount(0);

        _networkLogForm.ApplyLanguage();
        _normalLogForm.ApplyLanguage();
        _systemLogForm.ApplyLanguage();
    }

    private void WireComponentEvents()
    {
        _settingsMenuItem.Click += OnSettingsClick;
        _server.DeviceConnected += OnDeviceConnected;
        _server.DeviceDisconnected += OnDeviceDisconnected;
        _server.LogReceived += OnLogReceived;
        _server.NormalLogReceived += OnNormalLogReceived;
        _btnAdbReverse.DropDownOpening += OnAdbReverseOpening;
        _devicePanel.DeviceSelected += OnDeviceSelected;
        _devicePanel.RefreshAdbRequested += OnRefreshAdbDevices;
        _devicePanel.MirrorStartRequested += OnMirrorStartRequested;
        _devicePanel.MirrorStopRequested += OnMirrorStopRequested;
        _devicePanel.MirrorReconnectRequested += OnMirrorReconnectRequested;
        _devicePanel.MirrorRotateRequested += OnMirrorRotateRequested;
        _devicePanel.MirrorScreenshotRequested += OnMirrorScreenshotRequested;
        _devicePanel.MirrorPopoutRequested += OnMirrorPopoutRequested;
        _devicePanel.MirrorLayoutChanged += OnMirrorLayoutChanged;

        // Tab 切换：延迟 Show 非 Active Tab 的 Form + 仅对 SystemLog 做后台刷新。
        // Network/Normal 的过滤索引在日志添加时已增量维护，切回时无需全量 RebuildFilter。
        _tabLogType.SelectedIndexChanged += (s, e) =>
        {
            _showingNormalLog = _tabLogType.SelectedTab == _tabNormal;
            _showingSystemLog = _tabLogType.SelectedTab == _tabSystem;
            EnsureFormVisible(_showingNormalLog ? _normalLogForm : _showingSystemLog ? _systemLogForm : null);
            _networkLogForm.SetActive(!_showingNormalLog && !_showingSystemLog);
            _normalLogForm.SetActive(_showingNormalLog);
            if (_showingSystemLog)
                _systemLogForm.RefreshSystemLogList(preferBackground: true);
        };

        _networkLogForm.LogEntrySelected += entry =>
        {
            if (ReferenceEquals(_selectedLogEntry, entry)) return;
            _selectedLogEntry = entry;
            ShowLogDetail(entry);
        };
        _networkLogForm.LogEntryDoubleClicked += entry =>
        {
            if (entry != null) new JsonDetailForm(entry, _networkLogForm.Font).Show(this);
        };
        _networkLogForm.ScrollStateChanged += UpdateLogCount;
        _networkLogForm.LogCountChanged += UpdateLogCount;

        _normalLogForm.NormalLogEntryDoubleClicked += entry =>
        {
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
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
                ReadOnly = true, Text = entry.Message ?? "", Font = new Font("Consolas", _settings.FontSize),
                BackColor = Color.White
            };
            var btnCopy = new Button { Dock = DockStyle.Bottom, Text = Language.CopyNormalLogMessage, Height = 30 };
            btnCopy.Click += (_, _) => ClipboardTextHelper.TrySetText(entry.Message);
            form.Controls.Add(txt);
            form.Controls.Add(btnCopy);
            form.Show(this);
        };
        _normalLogForm.ScrollStateChanged += UpdateLogCount;
        _normalLogForm.LogCountChanged += UpdateLogCount;

        _systemLogForm.ScrollStateChanged += UpdateLogCount;
        _systemLogForm.SystemLogPausedChanged += UpdateLogCount;
        _systemLogForm.LogCountChanged += UpdateLogCount;

        _networkLogForm.RebuildFilter();

        _jsonDetailToolbar.SearchClicked += (s, e) => GetActiveJsonView()?.SearchAndHighlight(_jsonDetailToolbar.SearchText);
        _jsonDetailToolbar.ExpandAllClicked += (s, e) => GetActiveJsonView()?.ExpandAll();
        _jsonDetailToolbar.CollapseAllClicked += (s, e) => GetActiveJsonView()?.CollapseAll();
        _jsonDetailToolbar.CollapseTo2Clicked += (s, e) => GetActiveJsonView()?.CollapseToLevel(2);
        _jsonDetailToolbar.ViewToggled += (s, e) => SyncDetailViewVisibility();
        _tabDetail.SelectedIndexChanged += (s, e) => SyncDetailViewVisibility();
        _btnClear.Click += OnClear;
        _btnExportJson.Click += OnExportJson;
        _btnExportTxt.Click += OnExportTxt;
        _outerSplit.SplitterMoved += (_, _) =>
        {
            if (_scrcpySession?.IsRunning != true) return;
            _userResizedMirror = true;
        };
        Load += OnMainFormLoad;
        Shown += async (s, e) => await OnMainFormShownAsync();
        ResizeEnd += (_, _) =>
        {
            if (_scrcpySession?.IsRunning != true || !_userResizedMirror) return;
            _userResizedMirror = false;
            ScheduleEmbeddedMirrorRestart();
        };
    }

    private void OnMissingAdbPromptShown(object? sender, EventArgs e)
    {
        Shown -= OnMissingAdbPromptShown;
        var result = MessageBox.Show(Language.MissingAdbMessage, Language.MissingAdbTitle,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes) OnSettingsClick(this, EventArgs.Empty);
    }

    private static bool IsDesignTimeMode()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;
        var processName = Process.GetCurrentProcess().ProcessName;
        var commandLine = Environment.CommandLine;
        return processName.Contains("devenv", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("rider", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("jetbrains", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("JetBrains.ReSharper.Features.WinForms.Designer.External.Core",
                   StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("WinFormsDesigner", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateAdbStatus()
    {
        var adbPath = _adbHelper.GetAdbPath();
        var scrcpyPath = _scrcpyManager.GetScrcpyPath();
        var adbText = adbPath != null
            ? _adbValidated ? Language.AdbStatusReady(Path.GetFileName(adbPath)) : Language.AdbCheckingStatus
            : Language.AdbNotFoundStatus;
        var scrcpyText = _scrcpyPreparing
            ? Language.ScrcpyPreparing
            : scrcpyPath != null && _scrcpyValidated
                ? Language.ScrcpyStatusReady(Path.GetFileName(scrcpyPath))
                : scrcpyPath != null
                    ? Language.ScrcpyCheckingStatus
                    : !string.IsNullOrEmpty(_scrcpyDeployError)
                        ? Language.ScrcpyDeployFailed
                        : Language.ScrcpyNotReady;
        _lblAdbStatus.Text = $"{adbText} | {scrcpyText}";
        _lblAdbStatus.ForeColor = adbPath == null
            ? Color.Red
            : string.IsNullOrEmpty(_scrcpyDeployError) ? Color.Green : Color.DarkOrange;
    }

    /// <summary>
    /// Shown 事件：并行启动三个异步初始化（工具验证/ADB扫描/scrcpy准备），不再串行 await，
    /// 避免阻塞窗口首次渲染。
    /// </summary>
    private async Task OnMainFormShownAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = ValidateBundledToolsAsync();
        BootLog($"ValidateBundledToolsAsync dispatched: {sw.ElapsedMilliseconds}");
        _ = PrimeAdbDeviceListAsync();
        BootLog($"PrimeAdbDeviceListAsync dispatched: {sw.ElapsedMilliseconds}");
        _ = EnsureScrcpyReadyAsync(forceDeploy: false, reportToMirrorPanel: false);
        BootLog($"EnsureScrcpyReadyAsync dispatched: {sw.ElapsedMilliseconds}");
    }

    private async Task ValidateBundledToolsAsync()
    {
        var adbPath = _adbHelper.GetAdbPath();
        var scrcpyPath = _scrcpyManager.GetScrcpyPath();
        var adbValid = !string.IsNullOrEmpty(adbPath) && await Task.Run(() => _adbHelper.ValidateAdb(adbPath));
        var scrcpyValid = !string.IsNullOrEmpty(scrcpyPath) &&
                          await Task.Run(() => _scrcpyManager.ValidateScrcpy(scrcpyPath));
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() =>
        {
            _adbValidated = adbValid;
            _scrcpyValidated = scrcpyValid;
            UpdateAdbStatus();
            RefreshMirrorPanelState();
        }));
    }

    private void ApplySettings()
    {
        if (_uiFont == null || !_uiFont.Name.Equals("Consolas", StringComparison.OrdinalIgnoreCase) ||
            Math.Abs(_uiFont.Size - _settings.FontSize) > 0.01f)
        {
            var previousFont = _uiFont;
            var font = new Font("Consolas", _settings.FontSize);
            _uiFont = font;
            _networkLogForm.ApplyFont(font);
            _normalLogForm.ApplyFont(font);
            _systemLogForm.ApplyFont(font);
            _jsonHeadersView?.SetFont(font);
            _jsonRequestBodyView?.SetFont(font);
            _jsonResponseBodyView?.SetFont(font);
            _pagedHeadersView?.SetDisplayFont(font);
            _pagedRequestBodyView?.SetDisplayFont(font);
            _pagedResponseBodyView?.SetDisplayFont(font);
            previousFont?.Dispose();
        }
        _jsonHeadersView?.SetImageUrlPattern(_settings.ImageUrlPattern);
        _jsonRequestBodyView?.SetImageUrlPattern(_settings.ImageUrlPattern);
        _jsonResponseBodyView?.SetImageUrlPattern(_settings.ImageUrlPattern);
        _networkLogForm.ApplySettings(_settings);
        _normalLogForm.ApplySettings(_settings);
        _systemLogForm.ApplySettings(_settings);
        UpdateLogCount();
        RefreshMirrorPanelState();
    }

    private RingBuffer<LogEntry> GetCurrentLogBuffer()
    {
        if (_currentDeviceId == null) return _allLogs;
        return _deviceLogs.TryGetValue(_currentDeviceId, out var buf) ? buf : _allLogs;
    }

    #region Server Events

    /// <summary>
    /// TCP 设备连接回调：在 UI 线程初始化缓冲区和面板，然后用 fire-and-forget 启动异步 ADB 匹配。
    /// TryMatchAdbSerialAsync 在后台线程调 GetDevices()（adb devices 进程），避免阻塞 UI。
    /// </summary>
    private void OnDeviceConnected(object? sender, DeviceInfo info)
    {
        if (_isClosing) return;
        this.BeginInvoke(new Action(() =>
        {
            if (_isClosing) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var id = info.DeviceId ?? "";
            if (!_deviceLogs.ContainsKey(id))
                _deviceLogs[id] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);
            if (!_deviceNormalLogs.ContainsKey(id))
                _deviceNormalLogs[id] = new RingBuffer<LogEntry>(_settings.MaxNormalLogEntriesPerDevice);
            BootLog($"  OnDeviceConnected.Buffers: {sw.ElapsedMilliseconds}");
            _devicePanel.AddOrUpdateDevice(info, 0);
            BootLog($"  OnDeviceConnected.AddOrUpdate: {sw.ElapsedMilliseconds}");
            UpdateDeviceCountStatus();
            RefreshMirrorPanelState();
            BootLog($"  OnDeviceConnected.RefreshMirror: {sw.ElapsedMilliseconds}");
            if (_settings.AutoStartLogcat && _adbHelper.IsAdbAvailable())
            {
                var adbPath = _adbHelper.GetAdbPath();
                if (adbPath != null && !string.IsNullOrEmpty(info.AdbSerial))
                    StartLogcat(adbPath, info.AdbSerial, id, _settings.LogcatFilter);
            }
            BootLog($"  OnDeviceConnected.Logcat: {sw.ElapsedMilliseconds}");

            _ = TryMatchAdbSerialAsync(info);
            BootLog($"  OnDeviceConnected.Total: {sw.ElapsedMilliseconds}");
        }));
    }

    /// <summary>
    /// 异步匹配 TCP 设备的 ADB Serial：在后台线程调 GetDevices()（adb devices 进程，最多 3s），
    /// 匹配结果通过 BeginInvoke 回 UI 线程做 MergeTcpDevice + RebindAdbBackedState。
    /// 之前的同步版本 TryMatchAdbSerial 在 UI 线程直接跑 adb 进程，每次阻塞 1-3 秒。
    /// </summary>
    private async Task TryMatchAdbSerialAsync(DeviceInfo info)
    {
        if (!_adbHelper.IsAdbAvailable()) return;
        var scan = await ScanAdbDevicesAsync(CancellationToken.None).ConfigureAwait(false);
        if (!scan.succeeded || IsDisposed || !IsHandleCreated) return;
        var adbDevices = scan.devices;
        BeginInvoke(new Action(() =>
        {
            if (_isClosing) return;
            var model = info.DeviceModel ?? "";
            foreach (var dev in adbDevices)
            {
                if (!string.IsNullOrEmpty(dev.Model) && dev.Model.Equals(model, StringComparison.OrdinalIgnoreCase))
                {
                    info.AdbSerial = dev.Serial;
                    _adbSerialToDeviceId[dev.Serial] = info.DeviceId ?? "";
                    RebindAdbBackedState(dev.Serial, info.DeviceId ?? "");
                    _devicePanel.MergeTcpDevice(dev.Serial, info, 0);
                    break;
                }
            }
        }));
    }

    private void RebindAdbBackedState(string adbSerial, string deviceId)
    {
        if (string.IsNullOrEmpty(adbSerial) || string.IsNullOrEmpty(deviceId) || adbSerial == deviceId) return;
        if (_deviceLogs.TryGetValue(adbSerial, out var serialLogs))
        {
            if (!_deviceLogs.ContainsKey(deviceId)) _deviceLogs[deviceId] = serialLogs;
            _deviceLogs.Remove(adbSerial);
        }

        if (_deviceNormalLogs.TryGetValue(adbSerial, out var serialNormalLogs))
        {
            if (!_deviceNormalLogs.ContainsKey(deviceId)) _deviceNormalLogs[deviceId] = serialNormalLogs;
            _deviceNormalLogs.Remove(adbSerial);
        }

        if (_systemLogForm.IsRuntimeReady())
        {
            _systemLogStore.RemapDevice(adbSerial, deviceId);
            _systemLogForm.RefreshSystemLogList();
        }

        if (_logcatReaders.TryGetValue(adbSerial, out var reader))
        {
            _logcatReaders.Remove(adbSerial);
            _logcatReaders[deviceId] = reader;
        }

        if (_currentDeviceId == adbSerial)
        {
            _currentDeviceId = deviceId;
            _networkLogForm.InvalidateData(clearView: true);
            _normalLogForm.InvalidateData(clearView: true);
        }
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        if (_isClosing) return;
        this.BeginInvoke(new Action(() =>
        {
            if (_isClosing) return;
            _devicePanel.SetDeviceConnected(deviceId, false);
            UpdateDeviceCountStatus();
            RefreshMirrorPanelState();
            RequestAdbScan();
        }));
    }

    private void OnLogReceived(object? sender, (string deviceId, LogEntry entry) args)
    {
        EnqueueUiLog(PendingLogKind.Network, args.deviceId, args.entry);
    }

    private void OnNormalLogReceived(object? sender, (string deviceId, LogEntry entry) args)
    {
        EnqueueUiLog(PendingLogKind.Normal, args.deviceId, args.entry);
    }

    private void OnSystemLogReceived(object? sender, SystemLogEntry entry)
    {
        if (_isClosing) return;
        lock (_pendingSystemLogsLock)
        {
            _pendingSystemLogs.Enqueue(entry);
            if (_systemLogFlushScheduled) return;
            _systemLogFlushScheduled = true;
        }

        _ = Task.Run(FlushPendingSystemLogsAsync);
    }

    /// <summary>
    /// 系统日志批量刷入：在后台线程（FlushPendingSystemLogsAsync 的 Task.Run 上下文）
    /// 执行 Append（JSON 序列化 + 文件写入），完成后只通过 BeginInvoke 通知 UI 线程
    /// 做 OnStoreAppended（轻量遍历 + ScheduleSystemUiRefresh 去抖）。
    /// 之前的 ProcessPendingLogs 在 UI 线程里做 Append，每条日志一次 JSON 序列化+文件I/O，
    /// 200 条一批可阻塞 UI 500-1500ms。
    /// </summary>
    private async Task FlushPendingSystemLogsAsync()
    {
        try
        {
            while (!_isClosing)
            {
                var entries = new List<SystemLogEntry>();
                lock (_pendingSystemLogsLock)
                {
                    while (_pendingSystemLogs.Count > 0 && entries.Count < 200)
                        entries.Add(_pendingSystemLogs.Dequeue());
                    if (entries.Count == 0) return;
                }

                if (!_systemLogForm.IsRuntimeReady() || IsDisposed) return;
                foreach (var entry in entries) _systemLogStore.Append(entry);

                if (!_isClosing && IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!_isClosing && !IsDisposed) _systemLogForm.OnStoreAppended(entries);
                        }));
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                }

                await Task.Yield();
            }
        }
        catch (ObjectDisposedException) when (_isClosing)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            var restart = false;
            lock (_pendingSystemLogsLock)
            {
                _systemLogFlushScheduled = false;
                if (!_isClosing && _pendingSystemLogs.Count > 0)
                {
                    _systemLogFlushScheduled = true;
                    restart = true;
                }
            }

            if (restart) _ = Task.Run(FlushPendingSystemLogsAsync);
        }
    }

    #endregion

    #region Toolbar Actions

    private void AutoStartServer()
    {
        BootLog("AutoStartServer start");
        var port = _settings.ServerPort;
        Task.Run(() =>
        {
            try
            {
                _server.Start(port);
                BootLog("Server.Start done");
                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        BootLog("Server UI update");
                        _lblStatus.Text = $"\u25CF {Language.Running}";
                        _lblStatus.ForeColor = Color.Green;
                        _lblServerStatus.Text = Language.ServerPort(port);
                    });
            }
            catch (Exception ex)
            {
                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        _lblStatus.Text = $"\u25CB {Language.Error}";
                        _lblStatus.ForeColor = Color.Red;
                        _lblServerStatus.Text = Language.ServerError(ex.Message);
                        MessageBox.Show(Language.FailedToStartServer(ex.Message), Language.Error,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
            }
        });
    }

    private void OnAdbReverseOpening(object? sender, EventArgs e)
    {
        _btnAdbReverse.DropDownItems.Clear();
        var adbPath = _adbHelper.GetAdbPath();
        if (adbPath == null)
        {
            _btnAdbReverse.DropDownItems.Add(Language.AdbNotFound).Enabled = false;
            return;
        }

        var devices = _adbHelper.GetDevices();
        if (devices.Count == 0)
        {
            _btnAdbReverse.DropDownItems.Add(Language.NoDevicesConnected).Enabled = false;
            return;
        }

        foreach (var dev in devices)
        {
            var item = new ToolStripMenuItem(dev.DisplayName, null, (s, ev) =>
            {
                var (ok, output) = _adbHelper.ReversePort(adbPath, dev, _settings.ServerPort);
                MessageBox.Show(Language.AdbReverseResult(dev.Serial, ok, output),
                    Language.AdbReverse, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            });
            _btnAdbReverse.DropDownItems.Add(item);
        }

        _btnAdbReverse.DropDownItems.Add(new ToolStripSeparator());
        var allItem = new ToolStripMenuItem(Language.AdbReverseAll, null, (s, ev) =>
        {
            var results = new List<string>();
            foreach (var dev in devices)
            {
                var (ok, output) = _adbHelper.ReversePort(adbPath, dev, _settings.ServerPort);
                results.Add(Language.ReverseAllDeviceResult(dev.DisplayName, ok, output));
            }

            MessageBox.Show(string.Join("\n", results), Language.AdbReverse, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
        _btnAdbReverse.DropDownItems.Add(allItem);
    }

    #endregion

    #region Device Panel Events

    private void OnRefreshAdbDevices(object? sender, EventArgs e) => RequestAdbScan();

    private void RequestAdbScan()
    {
        if (!_adbHelper.IsAdbAvailable() || _isClosing) return;
        var scanVersion = Interlocked.Increment(ref _adbScanVersion);
        _ = RunRequestedAdbScanAsync(scanVersion);
    }

    private async Task RunRequestedAdbScanAsync(int scanVersion)
    {
        var scan = await ScanAdbDevicesAsync(CancellationToken.None).ConfigureAwait(false);
        if (!scan.succeeded || _isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (!_isClosing && scanVersion == Volatile.Read(ref _adbScanVersion))
                    ApplyAdbDevices(scan.devices);
            });
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task<(bool succeeded, List<AdbDevice> devices)> ScanAdbDevicesAsync(
        CancellationToken cancellationToken)
    {
        await _adbScanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                var succeeded = _adbHelper.TryGetDevices(out var devices);
                return (succeeded, devices);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _adbScanGate.Release();
        }
    }

    private async Task PrimeAdbDeviceListAsync()
    {
        if (!_adbHelper.IsAdbAvailable()) return;
        for (int attempt = 0; attempt < 4 && !IsDisposed && !_isClosing; attempt++)
        {
            var scanVersion = Interlocked.Increment(ref _adbScanVersion);
            var scan = await ScanAdbDevicesAsync(CancellationToken.None);
            if (IsDisposed || _isClosing || scanVersion != Volatile.Read(ref _adbScanVersion)) return;
            if (scan.succeeded) ApplyAdbDevices(scan.devices);
            if (scan.succeeded && scan.devices.Count > 0) return;
            await Task.Delay(1000);
        }
    }

    private void ApplyAdbDevices(List<AdbDevice> adbDevices)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var adbPath = _adbHelper.GetAdbPath();
        var newAdbSerials = new List<string>();

        _devicePanel.BeginDeviceUpdate();
        try
        {
            var currentSerials = new HashSet<string>(adbDevices.Select(d => d.Serial), StringComparer.Ordinal);
            var removedDeviceIds = _devicePanel.RemoveMissingAdbDevices(currentSerials);
            RemoveUnavailableDeviceState(removedDeviceIds);
            BootLog($"  ApplyAdbDevices.RemoveMissing: {sw.ElapsedMilliseconds}");

            foreach (var dev in adbDevices)
            {
                var mappedDeviceId = _adbSerialToDeviceId.TryGetValue(dev.Serial, out var existingDeviceId) &&
                                     !string.IsNullOrEmpty(existingDeviceId)
                    ? existingDeviceId
                    : dev.Serial;

                var isNew = _devicePanel.AddAdbDevice(dev.Serial, dev.Model);
                if (isNew) newAdbSerials.Add(dev.Serial);
                if (!_deviceLogs.ContainsKey(mappedDeviceId))
                    _deviceLogs[mappedDeviceId] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);
                if (!_deviceNormalLogs.ContainsKey(mappedDeviceId))
                    _deviceNormalLogs[mappedDeviceId] =
                        new RingBuffer<LogEntry>(_settings.MaxNormalLogEntriesPerDevice);

                if (!_logcatReaders.ContainsKey(mappedDeviceId) && !_logcatReaders.ContainsKey(dev.Serial) &&
                    _settings.AutoStartLogcat && adbPath != null)
                    StartLogcat(adbPath, dev.Serial, mappedDeviceId, _settings.LogcatFilter);
            }
        }
        finally
        {
            _devicePanel.EndDeviceUpdate();
        }

        BootLog($"  ApplyAdbDevices.Loop: {sw.ElapsedMilliseconds}");

        if (newAdbSerials.Count > 0 && adbPath != null && _settings.AutoAdbReverse)
        {
            Task.Run(() =>
            {
                foreach (var serial in newAdbSerials)
                    _adbHelper.ReversePort(adbPath, new AdbDevice { Serial = serial }, _settings.ServerPort);
            });
        }

        UpdateDeviceCountStatus();
        BootLog($"  ApplyAdbDevices.Total: {sw.ElapsedMilliseconds}");
    }

    private void StartAdbScanLoop()
    {
        BootLog("StartAdbScanLoop");
        StopAdbScanLoop();
        _adbScanCts = new CancellationTokenSource();
        _ = ScanLoopAsync(_adbScanCts.Token);
    }

    private async Task ScanLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_adbHelper.IsAdbAvailable())
                {
                    var scanVersion = Interlocked.Increment(ref _adbScanVersion);
                    var scan = await ScanAdbDevicesAsync(token);
                    if (scan.succeeded && IsHandleCreated && !token.IsCancellationRequested)
                        BeginInvoke(() =>
                        {
                            if (!_isClosing && scanVersion == Volatile.Read(ref _adbScanVersion))
                                ApplyAdbDevices(scan.devices);
                        });
                }

                await Task.Delay(_settings.AdbScanIntervalMs, token);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(10000, token); }
        }
    }

    private void StopAdbScanLoop()
    {
        _adbScanCts?.Cancel();
        _adbScanCts?.Dispose();
        _adbScanCts = null;
    }

    private void OnDeviceSelected(object? sender, string? deviceId)
    {
        _currentDeviceId = deviceId;
        _networkLogForm.InvalidateData(clearView: true);
        _normalLogForm.InvalidateData(clearView: true);
        _systemLogForm.RefreshSystemLogList();
        _selectedLogEntry = null;
        ShowLogDetail(null);
        _scrcpyRotationIndex = 0;
        RefreshMirrorPanelState();
        if (!string.IsNullOrEmpty(deviceId) &&
            _settings.AutoStartScrcpyForSelectedDevice &&
            _scrcpySession?.IsRunning != true)
        {
            _ = StartMirrorForCurrentDeviceAsync(restart: true);
        }
    }

    private void RemoveUnavailableDeviceState(IReadOnlyCollection<string> deviceIds)
    {
        if (deviceIds.Count == 0) return;

        var removedIds = deviceIds.ToHashSet(StringComparer.Ordinal);
        foreach (var deviceId in removedIds)
        {
            if (_logcatReaders.Remove(deviceId, out var reader))
            {
                var serial = reader.DeviceSerial;
                if (serial != null && _adbSerialToDeviceId.TryGetValue(serial, out var mappedId) &&
                    mappedId == deviceId)
                    _adbSerialToDeviceId.Remove(serial);
                _ = Task.Run(reader.Stop);
            }

            _deviceLogs.Remove(deviceId);
            _deviceNormalLogs.Remove(deviceId);
        }

        foreach (var serial in _adbSerialToDeviceId
                     .Where(pair => removedIds.Contains(pair.Key) || removedIds.Contains(pair.Value))
                     .Select(pair => pair.Key)
                     .ToList())
            _adbSerialToDeviceId.Remove(serial);

        if (_currentDeviceId != null && removedIds.Contains(_currentDeviceId))
        {
            _currentDeviceId = null;
            _selectedLogEntry = null;
            ShowLogDetail(null);
            StopMirror(clearStatusOnly: true);
            _networkLogForm.InvalidateData(clearView: true);
            _normalLogForm.InvalidateData(clearView: true);
            _systemLogForm.RefreshSystemLogList();
        }

        UpdateDeviceCountStatus();
        UpdateLogcatStatus();
        RefreshMirrorPanelState();
    }

    private void OnAdbReverseForDevice(object? sender, string deviceId)
    {
        var adbPath = _adbHelper.GetAdbPath();
        if (adbPath == null) return;
        int port = _settings.ServerPort;
        var info = _server.GetDeviceInfo(deviceId);
        var serial = info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId) ?? deviceId;
        var dev = new AdbDevice { Serial = serial };
        var (ok, output) = _adbHelper.ReversePort(adbPath, dev, port);
        MessageBox.Show(ok ? $"ADB Reverse OK for {serial}" : $"Failed: {output}", "ADB Reverse",
            MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void OnLogcatToggle(object? sender, string deviceId)
    {
        if (_logcatReaders.TryGetValue(deviceId, out var reader))
        {
            var serial = reader.DeviceSerial;
            _logcatReaders.Remove(deviceId);
            if (serial != null && _adbSerialToDeviceId.TryGetValue(serial, out var mappedId) &&
                mappedId == deviceId)
                _adbSerialToDeviceId.Remove(serial);
            _ = Task.Run(reader.Stop);
        }
        else
        {
            var adbPath = _adbHelper.GetAdbPath();
            var info = _server.GetDeviceInfo(deviceId);
            var serial = info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId) ?? deviceId;
            if (adbPath != null && !string.IsNullOrEmpty(serial))
                StartLogcat(adbPath, serial, deviceId, _settings.LogcatFilter);
            else
                MessageBox.Show(Language.CannotStartLogcat, Language.LogcatTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        UpdateLogcatStatus();
    }

    #endregion

    #region Logcat

    private void StartLogcat(string adbPath, string serial, string deviceId, string filter)
    {
        if (_isClosing || _logcatReaders.ContainsKey(deviceId)) return;
        var reader = new LogcatReader();
        reader.SystemLogReceived += OnSystemLogReceived;
        reader.ProcessExited += (s, args) => PostLogcatExitToUi(deviceId, serial, reader);
        _logcatReaders[deviceId] = reader;
        _adbSerialToDeviceId[serial] = deviceId;
        UpdateLogcatStatus();
        _ = RunLogcatReaderAsync(reader, adbPath, serial, deviceId, filter);
    }

    private async Task RunLogcatReaderAsync(LogcatReader reader, string adbPath, string serial,
        string deviceId, string filter)
    {
        try
        {
            await reader.StartAsync(adbPath, serial, filter).ConfigureAwait(false);
            if (!_isClosing && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (_logcatReaders.TryGetValue(deviceId, out var currentReader) &&
                        ReferenceEquals(currentReader, reader))
                        UpdateLogcatStatus();
                }));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            PostLogcatExitToUi(deviceId, serial, reader);
        }
    }

    private void PostLogcatExitToUi(string deviceId, string serial, LogcatReader reader)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (_logcatReaders.TryGetValue(deviceId, out var currentReader) &&
                    ReferenceEquals(currentReader, reader))
                {
                    _logcatReaders.Remove(deviceId);
                    if (_adbSerialToDeviceId.TryGetValue(serial, out var mappedId) && mappedId == deviceId)
                        _adbSerialToDeviceId.Remove(serial);
                }

                UpdateLogcatStatus();
            }));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    #endregion

    #region Scroll & Count

    private void UpdateLogCount()
    {
        if (_showingNormalLog)
        {
            // Count handled by NormalLogForm internally
        }
        else if (_showingSystemLog)
        {
            // Count handled by SystemLogForm internally
        }
        else
        {
            // Count handled by NetworkLogForm internally
        }
    }

    private void UpdateDeviceCountStatus()
    {
        _lblDeviceCountStatus.Text = Language.DevicesCount(_deviceLogs.Count);
    }

    private void UpdateLogcatStatus()
    {
        var running = _logcatReaders.Values.Count(r => r.IsRunning);
        _lblLogcatStatus.Text = Language.LogcatCount(running);
    }

    #endregion

    #region Bottom Bar Actions

    private void OnClear(object? sender, EventArgs e)
    {
        if (_currentDeviceId != null)
        {
            if (_deviceLogs.TryGetValue(_currentDeviceId, out var buf)) buf.Clear();
            if (_deviceNormalLogs.TryGetValue(_currentDeviceId, out var nBuf)) nBuf.Clear();
            if (_systemLogForm.IsRuntimeReady()) _systemLogStore.ClearDevice(_currentDeviceId);
        }
        else
        {
            foreach (var buf in _deviceLogs.Values) buf.Clear();
            _allLogs.Clear();
            foreach (var nBuf in _deviceNormalLogs.Values) nBuf.Clear();
            _allNormalLogs.Clear();
            if (_systemLogForm.IsRuntimeReady()) _systemLogStore.RotateSession();
        }

        _selectedLogEntry = null;
        ShowLogDetail(null);
        _networkLogForm.ClearFilterAndRefresh();
        _normalLogForm.ClearFilterAndRefresh();
        _systemLogForm.RefreshSystemLogList();
        RefreshMirrorPanelState();
    }

    private void OnExportJson(object? sender, EventArgs e)
    {
        if (_showingNormalLog)
        {
            using var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "normal_logs.json" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var buf = _normalLogForm.GetCurrentNormalLogBuffer();
            var entries = new List<LogEntry>();
            for (int i = 0; i < buf.Count; i++) entries.Add(buf.Get(i));
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            return;
        }

        using var dlg2 = new SaveFileDialog { Filter = "JSON|*.json", FileName = "network_logs.json" };
        if (dlg2.ShowDialog() != DialogResult.OK) return;
        var buf2 = _networkLogForm.GetCurrentLogBuffer();
        var entries2 = new List<LogEntry>();
        for (int i = 0; i < buf2.Count; i++) entries2.Add(buf2.Get(i));
        var json2 = JsonSerializer.Serialize(entries2, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dlg2.FileName, json2);
    }

    private void OnExportTxt(object? sender, EventArgs e)
    {
        if (_showingNormalLog)
        {
            using var dlg = new SaveFileDialog { Filter = "Text|*.txt", FileName = "normal_logs.txt" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var buf = _normalLogForm.GetCurrentNormalLogBuffer();
            using var writer = new StreamWriter(dlg.FileName);
            for (int i = 0; i < buf.Count; i++)
            {
                var entry = buf.Get(i);
                var timeStr = entry.SendTime > 0 ? entry.SendTimeDt.ToString("HH:mm:ss.fff") : "";
                writer.WriteLine($"[{timeStr}] [{LevelToDisplayText(entry.Level)}] [{entry.Method}] {entry.Message}");
            }

            return;
        }

        using var dlg2 = new SaveFileDialog { Filter = "Text|*.txt", FileName = "network_logs.txt" };
        if (dlg2.ShowDialog() != DialogResult.OK) return;
        var buf2 = _networkLogForm.GetCurrentLogBuffer();
        using var writer2 = new StreamWriter(dlg2.FileName);
        for (int i = 0; i < buf2.Count; i++)
        {
            var entry = buf2.Get(i);
            writer2.WriteLine($"--- Log #{i + 1} ---");
            writer2.WriteLine($"Method: {entry.Method}");
            writer2.WriteLine($"URL: {entry.Url}");
            writer2.WriteLine($"Code: {entry.Code}");
            writer2.WriteLine($"Duration: {entry.Duration}ms");
            writer2.WriteLine($"Successful: {entry.IsSuccessStatusCode}");
            if (!string.IsNullOrEmpty(entry.Send)) writer2.WriteLine($"Request Body: {entry.Send}");
            if (!string.IsNullOrEmpty(entry.Content)) writer2.WriteLine($"Response: {entry.Content}");
            writer2.WriteLine();
        }
    }

    private static string LevelToDisplayText(int level) => level switch
    {
        2 => "V", 3 => "D", 4 => "I", 5 => "W", 6 => "E", 7 => "F", _ => "?"
    };

    #endregion

    #region Settings

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings, _adbHelper);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings = AppSettings.Load();
            ApplySettings();
            _scrcpyDeployError = null;
            _scrcpyDeployStatus = null;
            foreach (var kvp in _deviceLogs) kvp.Value.Resize(_settings.MaxLogEntriesPerDevice);
            foreach (var kvp in _deviceNormalLogs) kvp.Value.Resize(_settings.MaxNormalLogEntriesPerDevice);
            _allLogs.Resize(_settings.MaxLogEntriesAll);
            _allNormalLogs.Resize(_settings.MaxNormalLogEntries);
            _networkLogForm.InvalidateData(clearView: true);
            _normalLogForm.InvalidateData(clearView: true);
            if (_systemLogForm.IsRuntimeReady())
            {
                _systemLogStore.UpdateHotCapacity(_settings.MaxSystemLogEntries);
                _systemLogForm.RefreshSystemLogList();
            }

            UpdateAdbStatus();
            RefreshMirrorPanelState();
        }
    }

    #endregion

    private void InitializeSystemLogRuntime()
    {
        _systemLogStore = new SystemLogSessionStore(_settings.MaxSystemLogEntries);
    }

    private bool IsSystemLogRuntimeReady() => _systemLogStore != null;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowFinalClose && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            _shutdownTask ??= PrepareForFinalCloseAsync();
            return;
        }

        _isClosing = true;
        StopAdbScanLoop();
        CancelUiLogPipeline();
        CancelPreviewAsyncOperations();
        _networkLogForm.CancelAsyncOperations();
        _normalLogForm.CancelAsyncOperations();
        _systemLogForm.CancelAsyncOperations();
        _server.DeviceConnected -= OnDeviceConnected;
        _server.DeviceDisconnected -= OnDeviceDisconnected;
        _server.LogReceived -= OnLogReceived;
        _server.NormalLogReceived -= OnNormalLogReceived;

        if (e.CloseReason == CloseReason.WindowsShutDown && !_allowFinalClose)
        {
            DetachAllScrcpySessionsForShutdown();
            foreach (var reader in _logcatReaders.Values.ToArray()) _ = Task.Run(reader.Stop);
            _ = Task.Run(_server.Stop);
        }

        if (_systemLogStore is not null) _systemLogStore.Dispose();
        _networkLogForm.Dispose();
        _normalLogForm.Dispose();
        _systemLogForm.Dispose();
        _uiFont?.Dispose();
        _uiFont = null;
        base.OnFormClosing(e);
    }

    private async Task PrepareForFinalCloseAsync()
    {
        _isClosing = true;
        Hide();
        Interlocked.Increment(ref _adbScanVersion);
        StopAdbScanLoop();
        CancelUiLogPipeline();
        CancelPreviewAsyncOperations();
        _networkLogForm.CancelAsyncOperations();
        _normalLogForm.CancelAsyncOperations();
        _systemLogForm.CancelAsyncOperations();
        _scrcpyStartCts?.Cancel();
        _mirrorRestartTimer?.Stop();
        _mirrorRestartTimer?.Dispose();
        _mirrorRestartTimer = null;

        _server.DeviceConnected -= OnDeviceConnected;
        _server.DeviceDisconnected -= OnDeviceDisconnected;
        _server.LogReceived -= OnLogReceived;
        _server.NormalLogReceived -= OnNormalLogReceived;

        var cleanupTasks = new List<Task>(DetachAllScrcpySessionsForShutdown());
        foreach (var reader in _logcatReaders.Values.Distinct().ToArray())
            cleanupTasks.Add(Task.Run(reader.Stop));
        _logcatReaders.Clear();
        _adbSerialToDeviceId.Clear();
        cleanupTasks.Add(Task.Run(_server.Stop));

        try
        {
            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
            await WaitForScrcpyCleanupAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                _allowFinalClose = true;
                Close();
            }));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.End)
        {
            if (_showingSystemLog) _systemLogForm.HandleEndKey();
            else if (_showingNormalLog) _normalLogForm.HandleEndKey();
            else _networkLogForm.HandleEndKey();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnMainFormLoad(object? sender, EventArgs e)
    {
        BootLog("OnMainFormLoad start");
        RefreshMirrorPanelState();
        BootLog("OnMainFormLoad end");
    }
}
