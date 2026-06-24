using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Models;
using LogViewer.Network;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// 主窗口，负责日志列表展示、详情查看、设备管理和设置配置。
/// 支持网络日志和系统日志两种类型，通过 TabControl 切换显示。
/// </summary>
public partial class MainForm : Form
{
    /// <summary>TCP 服务器实例，用于接收 Android 设备的网络日志。</summary>
    private readonly LogServer _server = new();
    /// <summary>ADB 辅助类，用于设备检测、验证和端口映射。</summary>
    private readonly AdbHelper _adbHelper = new();
    /// <summary>scrcpy 管理器，用于投屏功能。</summary>
    private readonly ScrcpyManager _scrcpyManager = new();
    /// <summary>每台设备的 logcat 读取器，key 为 deviceId。</summary>
    private readonly Dictionary<string, LogcatReader> _logcatReaders = new();
    /// <summary>应用设置实例。</summary>
    private AppSettings _settings;
    /// <summary>ADB 设备扫描的取消令牌源。</summary>
    private CancellationTokenSource? _adbScanCts;
    /// <summary>scrcpy 启动的取消令牌源。</summary>
    private CancellationTokenSource? _scrcpyStartCts;

    /// <summary>每台设备的网络日志缓冲区，key 为 deviceId。</summary>
    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceLogs = new();
    /// <summary>所有设备合并的网络日志缓冲区。</summary>
    private readonly RingBuffer<LogEntry> _allLogs;
    /// <summary>ADB 序列号到 deviceId 的映射表。</summary>
    private readonly Dictionary<string, string> _adbSerialToDeviceId = new();
    /// <summary>待处理的系统日志队列（线程安全）。</summary>
    private readonly Queue<SystemLogEntry> _pendingSystemLogs = new();
    /// <summary>待处理系统日志队列的锁对象。</summary>
    private readonly object _pendingSystemLogsLock = new();
    /// <summary>系统日志刷新是否已调度。</summary>
    private bool _systemLogFlushScheduled;

    /// <summary>当前 scrcpy 会话。</summary>
    private ScrcpySession? _scrcpySession;
    /// <summary>外部启动的 scrcpy 会话列表。</summary>
    private readonly List<ScrcpySession> _externalScrcpySessions = new();
    /// <summary>scrcpy 旋转角度索引。</summary>
    private int _scrcpyRotationIndex;
    /// <summary>scrcpy 是否正在准备中。</summary>
    private bool _scrcpyPreparing;
    /// <summary>scrcpy 部署状态信息。</summary>
    private string? _scrcpyDeployStatus;
    /// <summary>scrcpy 部署错误信息。</summary>
    private string? _scrcpyDeployError;
    /// <summary>ADB 是否已验证可用。</summary>
    private bool _adbValidated;
    /// <summary>scrcpy 是否已验证可用。</summary>
    private bool _scrcpyValidated;
    /// <summary>正在启动镜像的设备序列号。</summary>
    private string? _mirrorStartingSerial;
    /// <summary>镜像重启定时器。</summary>
    private System.Windows.Forms.Timer? _mirrorRestartTimer;
    /// <summary>是否有待处理的镜像重启。</summary>
    private bool _mirrorRestartPending;
    /// <summary>镜像是否正在重启中。</summary>
    private bool _mirrorRestartInProgress;

    /// <summary>当前选中的设备 deviceId。</summary>
    private string? _currentDeviceId;
    /// <summary>当前是否显示系统日志（true=系统日志，false=网络日志）。</summary>
    private bool _showingSystemLog;

    /// <summary>系统日志自动滚动是否启用。</summary>
    private bool _systemAutoScrollEnabled = true;

    private System.Windows.Forms.SplitContainer _outerSplit;
    private System.Windows.Forms.SplitContainer _innerSplit;
    private LogViewer.UI.DevicePanel _devicePanel;

    private System.Windows.Forms.TabControl _tabLogType;
    private System.Windows.Forms.TabPage _tabNetwork;
    private System.Windows.Forms.TabPage _tabSystem;

    private System.Windows.Forms.Panel _pnlNetworkFilter;
    private System.Windows.Forms.TextBox _txtNetworkKeyword;
    private System.Windows.Forms.ComboBox _cmbMethod;
    private System.Windows.Forms.ComboBox _cmbStatusCode;
    private System.Windows.Forms.Button _btnScrollToTop;
    private System.Windows.Forms.Button _btnScrollToBottom;
    private System.Windows.Forms.Label _lblLogCount;

    private System.Windows.Forms.ListView _lstNetworkLogs;
    private System.Windows.Forms.ListView _lstSystemLogs;

    private System.Windows.Forms.Panel _pnlSystemFilter;
    private System.Windows.Forms.Panel _systemActionBar;
    private System.Windows.Forms.TextBox _txtSystemKeyword;
    private System.Windows.Forms.ComboBox _cmbLogLevel;
    private System.Windows.Forms.ComboBox _cmbLogTag;
    private System.Windows.Forms.Button _btnSystemScrollToTop;
    private System.Windows.Forms.Button _btnSystemScrollToBottom;
    private System.Windows.Forms.Button _btnSystemPauseResume;
    private System.Windows.Forms.Label _lblSystemBacklog;

    private System.Windows.Forms.TabControl _tabDetail;
    private System.Windows.Forms.TabPage _tabHeaders;
    private System.Windows.Forms.TabPage _tabRequestBody;
    private System.Windows.Forms.TabPage _tabResponseBody;

    private System.Windows.Forms.Panel _jsonHeaders;
    private System.Windows.Forms.Panel _jsonRequestBody;
    private System.Windows.Forms.Panel _jsonResponseBody;
    private JsonTreeView? _jsonHeadersView;
    private JsonTreeView? _jsonRequestBodyView;
    private JsonTreeView? _jsonResponseBodyView;

    private System.Windows.Forms.TextBox _rawHeaders;
    private System.Windows.Forms.TextBox _rawRequestBody;
    private System.Windows.Forms.TextBox _rawResponseBody;

    private System.Windows.Forms.Panel _pnlJsonToolbar;
    private System.Windows.Forms.TextBox _txtJsonSearch;
    private System.Windows.Forms.Button _btnJsonSearch;
    private System.Windows.Forms.Button _btnExpandAll;
    private System.Windows.Forms.Button _btnCollapseAll;
    private System.Windows.Forms.Button _btnCollapseTo2;
    private System.Windows.Forms.Button _btnToggleView;
    private bool _detailViewIsRaw;

    private System.Windows.Forms.ToolStrip _toolStrip;
    private System.Windows.Forms.ToolStripDropDownButton _btnAdbReverse;
    private System.Windows.Forms.ToolStripLabel _lblStatus;

    private System.Windows.Forms.MenuStrip _menuStrip;
    private System.Windows.Forms.StatusStrip _statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel _lblServerStatus;
    private System.Windows.Forms.ToolStripStatusLabel _lblDeviceCountStatus;
    private System.Windows.Forms.ToolStripStatusLabel _lblAdbStatus;
    private System.Windows.Forms.ToolStripStatusLabel _lblLogcatStatus;

    private System.Windows.Forms.FlowLayoutPanel _pnlBottomBar;
    private System.Windows.Forms.Button _btnClear;
    private System.Windows.Forms.Button _btnExportJson;
    private System.Windows.Forms.Button _btnExportTxt;

    private LogEntry? _selectedLogEntry;

    /// <summary>
    /// 初始化主窗口，加载设置并配置组件。
    /// </summary>
    public MainForm()
    {
        _settings = AppSettings.Load();
        _allLogs = new RingBuffer<LogEntry>(_settings.MaxLogEntriesAll);
        InitializeComponent();
        ApplyLanguage();

        if (IsDesignTimeMode())
        {
            return;
        }

        InitializeJsonTreeViewsRuntime();
        WireComponentEvents();
        ApplySettings();

        if (_adbHelper.IsAdbAvailable())
        {
            _adbHelper.EnsureServerStarted();
        }

        if (!_adbHelper.IsAdbAvailable())
        {
            Shown += OnMissingAdbPromptShown;
        }

        StartAdbScanLoop();
        AutoStartServer();
    }

    /// <summary>
    /// 应用界面语言，从 Language 类加载所有 UI 文本。
    /// </summary>
    private void ApplyLanguage()
    {
        Text = Language.AppTitle;
        _toolsMenuItem.Text = Language.ToolsMenu;
        _settingsMenuItem.Text = Language.SettingsMenu;
        _btnAdbReverse.Text = Language.AdbReverse;
        _tabNetwork.Text = Language.NetworkLogs;
        _tabSystem.Text = Language.SystemLogs;
        _btnScrollToTop.Text = Language.ScrollToTop;
        _btnScrollToBottom.Text = Language.ScrollToBottom;
        _btnSystemScrollToTop.Text = Language.ScrollToTop;
        _btnSystemScrollToBottom.Text = Language.ScrollToBottom;
        _btnSystemPauseResume.Text = Language.Pause;
        _txtNetworkKeyword.PlaceholderText = Language.KeywordPlaceholder;
        _txtSystemKeyword.PlaceholderText = Language.KeywordPlaceholder;
        _tabHeaders.Text = Language.Headers;
        _tabRequestBody.Text = Language.RequestBody;
        _tabResponseBody.Text = Language.ResponseBody;
        _txtJsonSearch.PlaceholderText = Language.SearchJsonPlaceholder;
        _btnExpandAll.Text = Language.Expand;
        _btnCollapseAll.Text = Language.Collapse;
        _btnCollapseTo2.Text = Language.CollapseLevel2;
        _btnToggleView.Text = Language.Raw;
        _btnClear.Text = Language.Clear;
        _btnExportJson.Text = Language.ExportJson;
        _btnExportTxt.Text = Language.ExportTxt;
        _lblStatus.Text = $"\u25CF {Language.Running}";
        _lblServerStatus.Text = Language.ServerStopped;
        _lblDeviceCountStatus.Text = Language.DevicesCount(0);
        _lblAdbStatus.Text = Language.AdbNotDetected;
        _lblLogcatStatus.Text = Language.LogcatCount(0);

        _cmbMethod.Items.Clear();
        _cmbMethod.Items.AddRange([Language.All, "GET", "POST", "PUT", "DELETE", "PATCH"]);
        _cmbStatusCode.Items.Clear();
        _cmbStatusCode.Items.AddRange([Language.All, "2xx", "3xx", "4xx", "5xx", "0"]);
        _cmbLogLevel.Items.Clear();
        _cmbLogLevel.Items.AddRange([Language.All, "V", "D", "I", "W", "E", "F"]);
        _cmbLogTag.Items.Clear();
        _cmbLogTag.Items.Add(Language.All);
    }

    /// <summary>
    /// 连接所有组件事件，绑定 UI 交互逻辑。
    /// </summary>
    private void WireComponentEvents()
    {
        InitializeSystemLogRuntime();
        _settingsMenuItem.Click += OnSettingsClick;
        _server.DeviceConnected += OnDeviceConnected;
        _server.DeviceDisconnected += OnDeviceDisconnected;
        _server.LogReceived += OnLogReceived;
        _btnAdbReverse.DropDownOpening += OnAdbReverseOpening;
        _devicePanel.DeviceSelected += OnDeviceSelected;
        _devicePanel.RefreshAdbRequested += OnRefreshAdbDevices;
        _devicePanel.MirrorStartRequested += OnMirrorStartRequested;
        _devicePanel.MirrorStopRequested += OnMirrorStopRequested;
        _devicePanel.MirrorReconnectRequested += OnMirrorReconnectRequested;
        _devicePanel.MirrorRotateRequested += OnMirrorRotateRequested;
        _devicePanel.MirrorScreenshotRequested += OnMirrorScreenshotRequested;
        _devicePanel.MirrorPopoutRequested += OnMirrorPopoutRequested;
        _tabLogType.SelectedIndexChanged += (s, e) =>
        {
            _showingSystemLog = _tabLogType.SelectedIndex == 1;
            if (_showingSystemLog)
            {
                RefreshSystemLogList(preferBackground: true);
            }
            else
            {
                RefreshNetworkFilter();
            }

            UpdateLogCount();
        };
        _txtNetworkKeyword.TextChanged += OnNetworkFilterChanged;
        _cmbMethod.SelectedIndexChanged += OnNetworkFilterChanged;
        _cmbStatusCode.SelectedIndexChanged += OnNetworkFilterChanged;
        _txtSystemKeyword.TextChanged += OnSystemFilterChanged;
        _cmbLogLevel.SelectedIndexChanged += OnSystemFilterChanged;
        _cmbLogTag.SelectedIndexChanged += OnSystemFilterChanged;
        _cmbLogTag.DropDown += (s, e) => RefreshSystemTagOptions();
        _btnScrollToTop.Click += (s, e) =>
        {
            _networkAutoScrollEnabled = false;
            ScrollToTop(_lstNetworkLogs);
            UpdateLogCount();
        };
        _btnScrollToBottom.Click += (s, e) =>
        {
            _networkAutoScrollEnabled = true;
            ScrollToBottom(_lstNetworkLogs);
            UpdateLogCount();
        };
        _btnSystemScrollToTop.Click += (s, e) =>
        {
            _systemAutoScrollEnabled = false;
            ScrollToTop(_lstSystemLogs);
            UpdateLogCount();
        };
        _btnSystemScrollToBottom.Click += (s, e) =>
        {
            _systemAutoScrollEnabled = true;
            ScrollToBottom(_lstSystemLogs);
            UpdateLogCount();
        };
        _btnSystemPauseResume.Click += OnSystemPauseResumeClick;
        ConfigureLogLists();
        ConfigureSystemLogList();
        RefreshNetworkFilter();
        RefreshSystemLogList();
        UpdateSystemLogUiState();
        _lstNetworkLogs.SelectedIndexChanged += OnNetworkLogSelected;
        _lstNetworkLogs.DoubleClick += OnNetworkLogDoubleClick;
        _lstNetworkLogs.MouseUp += OnNetworkLogMouseUp;
        _lstNetworkLogs.MouseWheel += OnNetworkLogsMouseWheel;
        _lstNetworkLogs.ContextMenuStrip = CreateNetworkLogMenu();
        _lstSystemLogs.MouseWheel += OnSystemLogsMouseWheel;
        _lstSystemLogs.ContextMenuStrip = CreateSystemLogMenu();
        _btnJsonSearch.Click += (s, e) => GetActiveJsonView()?.SearchAndHighlight(_txtJsonSearch.Text);
        _btnExpandAll.Click += (s, e) => GetActiveJsonView()?.ExpandAll();
        _btnCollapseAll.Click += (s, e) => GetActiveJsonView()?.CollapseAll();
        _btnCollapseTo2.Click += (s, e) => GetActiveJsonView()?.CollapseToLevel(2);
        _btnToggleView.Click += OnToggleDetailView;
        _tabDetail.SelectedIndexChanged += (s, e) => SyncDetailViewVisibility();
        _btnClear.Click += OnClear;
        _btnExportJson.Click += OnExportJson;
        _btnExportTxt.Click += OnExportTxt;
        _outerSplit.SplitterMoved += (_, _) =>
        {
            ScheduleEmbeddedMirrorRestart();
        };
        Load += OnMainFormLoad;
        Shown += async (s, e) => await OnMainFormShownAsync();
        Resize += (_, _) => ScheduleEmbeddedMirrorRestart();
        ResizeEnd += (_, _) => ScheduleEmbeddedMirrorRestart();
    }

    private void OnMissingAdbPromptShown(object? sender, EventArgs e)
    {
        Shown -= OnMissingAdbPromptShown;

        var result = MessageBox.Show(
            Language.MissingAdbMessage,
            Language.MissingAdbTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            OnSettingsClick(this, EventArgs.Empty);
        }
    }

    private static bool IsDesignTimeMode()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return true;
        }

        var processName = Process.GetCurrentProcess().ProcessName;
        var commandLine = Environment.CommandLine;
        return processName.Contains("devenv", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("rider", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("jetbrains", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("JetBrains.ReSharper.Features.WinForms.Designer.External.Core", StringComparison.OrdinalIgnoreCase) ||
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

    private async Task OnMainFormShownAsync()
    {
        _ = ValidateBundledToolsAsync();
        await PrimeAdbDeviceListAsync();
        await EnsureScrcpyReadyAsync(forceDeploy: false, reportToMirrorPanel: false);
    }

    private async Task ValidateBundledToolsAsync()
    {
        var adbPath = _adbHelper.GetAdbPath();
        var scrcpyPath = _scrcpyManager.GetScrcpyPath();

        var adbValid = !string.IsNullOrEmpty(adbPath) && await Task.Run(() => _adbHelper.ValidateAdb(adbPath));
        var scrcpyValid = !string.IsNullOrEmpty(scrcpyPath) && await Task.Run(() => _scrcpyManager.ValidateScrcpy(scrcpyPath));

        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            _adbValidated = adbValid;
            _scrcpyValidated = scrcpyValid;
            UpdateAdbStatus();
            RefreshMirrorPanelState();
        }));
    }

    /// <summary>
    /// 应用设置到 UI 组件，包括字体大小和过滤器默认值。
    /// </summary>
    private void ApplySettings()
    {
        var font = new Font("Consolas", _settings.FontSize);
        _lstNetworkLogs.Font = font;
        _lstSystemLogs.Font = font;
        _jsonHeadersView?.SetFont(font);
        _jsonRequestBodyView?.SetFont(font);
        _jsonResponseBodyView?.SetFont(font);
        if (_cmbMethod.SelectedIndex < 0 && _cmbMethod.Items.Count > 0) _cmbMethod.SelectedIndex = 0;
        if (_cmbStatusCode.SelectedIndex < 0 && _cmbStatusCode.Items.Count > 0) _cmbStatusCode.SelectedIndex = 0;
        if (_cmbLogLevel.SelectedIndex < 0 && _cmbLogLevel.Items.Count > 0) _cmbLogLevel.SelectedIndex = 0;
        if (_cmbLogTag.SelectedIndex < 0 && _cmbLogTag.Items.Count > 0) _cmbLogTag.SelectedIndex = 0;
        UpdateLogCount();
        RefreshMirrorPanelState();
    }

    /// <summary>
    /// 获取当前选中的 JSON 树视图控件。
    /// </summary>
    private JsonTreeView? GetActiveJsonView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _jsonHeadersView;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _jsonRequestBodyView;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _jsonResponseBodyView;
        return null;
    }

    /// <summary>
    /// 获取当前选中的原始文本视图控件。
    /// </summary>
    private TextBox? GetActiveRawView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _rawHeaders;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _rawRequestBody;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _rawResponseBody;
        return null;
    }

    /// <summary>
    /// 切换详情视图模式（原始文本/JSON 树）。
    /// </summary>
    private void OnToggleDetailView(object? sender, EventArgs e)
    {
        _detailViewIsRaw = !_detailViewIsRaw;
        _btnToggleView.Text = _detailViewIsRaw ? Language.Tree : Language.Raw;
        _btnExpandAll.Enabled = !_detailViewIsRaw;
        _btnCollapseAll.Enabled = !_detailViewIsRaw;
        _btnCollapseTo2.Enabled = !_detailViewIsRaw;
        _txtJsonSearch.Enabled = !_detailViewIsRaw;
        _btnJsonSearch.Enabled = !_detailViewIsRaw;
        SyncDetailViewVisibility();
    }

    /// <summary>
    /// 同步详情视图的可见性，根据当前模式显示对应的控件。
    /// </summary>
    private void SyncDetailViewVisibility()
    {
        if (_jsonHeadersView != null) _jsonHeadersView.Visible = !_detailViewIsRaw;
        _rawHeaders.Visible = _detailViewIsRaw;
        if (_jsonRequestBodyView != null) _jsonRequestBodyView.Visible = !_detailViewIsRaw;
        _rawRequestBody.Visible = _detailViewIsRaw;
        if (_jsonResponseBodyView != null) _jsonResponseBodyView.Visible = !_detailViewIsRaw;
        _rawResponseBody.Visible = _detailViewIsRaw;
    }

    /// <summary>
    /// 初始化运行时的 JSON 树视图控件。
    /// </summary>
    private void InitializeJsonTreeViewsRuntime()
    {
        _jsonHeadersView = CreateRuntimeJsonTreeView(_jsonHeaders, nameof(_jsonHeadersView));
        _jsonRequestBodyView = CreateRuntimeJsonTreeView(_jsonRequestBody, nameof(_jsonRequestBodyView));
        _jsonResponseBodyView = CreateRuntimeJsonTreeView(_jsonResponseBody, nameof(_jsonResponseBodyView));
    }

    /// <summary>
    /// 创建运行时的 JSON 树视图控件并添加到宿主面板。
    /// </summary>
    private static JsonTreeView CreateRuntimeJsonTreeView(Control host, string name)
    {
        var view = new JsonTreeView
        {
            Dock = DockStyle.Fill,
            Name = name,
            Margin = Padding.Empty
        };
        host.Controls.Add(view);
        view.BringToFront();
        return view;
    }

    /// <summary>
    /// 获取当前设备的日志缓冲区，如果未选中设备则返回合并缓冲区。
    /// </summary>
    private RingBuffer<LogEntry> GetCurrentLogBuffer()
    {
        if (_currentDeviceId == null) return _allLogs;
        return _deviceLogs.TryGetValue(_currentDeviceId, out var buf) ? buf : _allLogs;
    }

    #region Server Events

    /// <summary>
    /// 处理设备连接事件，创建设备日志缓冲区并启动 logcat。
    /// </summary>
    private void OnDeviceConnected(object? sender, DeviceInfo info)
    {
        this.BeginInvoke(new Action(() =>
        {
            var id = info.DeviceId ?? "";
            if (!_deviceLogs.ContainsKey(id))
            {
                _deviceLogs[id] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);
            }

            TryMatchAdbSerial(info);
            _devicePanel.AddOrUpdateDevice(info, 0);
            UpdateDeviceCountStatus();
            RefreshMirrorPanelState();

            if (_settings.AutoStartLogcat && _adbHelper.IsAdbAvailable())
            {
                var adbPath = _adbHelper.GetAdbPath();
                if (adbPath != null && !string.IsNullOrEmpty(info.AdbSerial))
                {
                    StartLogcat(adbPath, info.AdbSerial, id, _settings.LogcatFilter);
                }
            }
        }));
    }

    /// <summary>
    /// 尝试匹配设备的 ADB 序列号，通过设备型号在 ADB 设备列表中查找。
    /// </summary>
    private void TryMatchAdbSerial(DeviceInfo info)
    {
        if (!_adbHelper.IsAdbAvailable()) return;
        var adbDevices = _adbHelper.GetDevices();
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
    }

    private void RebindAdbBackedState(string adbSerial, string deviceId)
    {
        if (string.IsNullOrEmpty(adbSerial) || string.IsNullOrEmpty(deviceId) || adbSerial == deviceId)
        {
            return;
        }

        if (_deviceLogs.TryGetValue(adbSerial, out var serialLogs))
        {
            if (!_deviceLogs.ContainsKey(deviceId))
            {
                _deviceLogs[deviceId] = serialLogs;
            }

            _deviceLogs.Remove(adbSerial);
        }

        if (IsSystemLogRuntimeReady())
        {
            _systemLogStore.RemapDevice(adbSerial, deviceId);
            RefreshSystemLogList();
        }

        if (_logcatReaders.TryGetValue(adbSerial, out var reader))
        {
            _logcatReaders.Remove(adbSerial);
            _logcatReaders[deviceId] = reader;
        }

        if (_currentDeviceId == adbSerial)
        {
            _currentDeviceId = deviceId;
        }
    }

    private void OnDeviceDisconnected(object? sender, string deviceId)
    {
        this.BeginInvoke(new Action(() =>
        {
            _devicePanel.SetDeviceConnected(deviceId, false);
            UpdateDeviceCountStatus();
            RefreshMirrorPanelState();
            RequestAdbScan();
        }));
    }

    private void OnLogReceived(object? sender, (string deviceId, LogEntry entry) args)
    {
        this.BeginInvoke(new Action(() =>
        {
            var (deviceId, entry) = args;
            var showingCurrentNetwork = !_showingSystemLog && (_currentDeviceId == deviceId || _currentDeviceId == null);
            var activeViewCountBeforeAdd = 0;
            var activeViewWasFull = false;

            if (showingCurrentNetwork)
            {
                if (_currentDeviceId == null)
                {
                    activeViewCountBeforeAdd = _allLogs.Count;
                    activeViewWasFull = _allLogs.Count >= _allLogs.Capacity;
                }
                else if (_deviceLogs.TryGetValue(deviceId, out var activeDeviceBuf))
                {
                    activeViewCountBeforeAdd = activeDeviceBuf.Count;
                    activeViewWasFull = activeDeviceBuf.Count >= activeDeviceBuf.Capacity;
                }
            }

            if (_deviceLogs.TryGetValue(deviceId, out var deviceBuf))
            {
                deviceBuf.Add(entry);
                _devicePanel.UpdateLogCount(deviceId, deviceBuf.Count);
            }

            _allLogs.Add(entry);

            if (showingCurrentNetwork)
            {
                var incrementalUpdated = TryAppendNetworkLogIncrementally(entry, activeViewCountBeforeAdd, activeViewWasFull);
                if (_networkAutoScrollEnabled)
                {
                    var wasAtBottom = IsAtBottom(_lstNetworkLogs);
                    if (!incrementalUpdated)
                    {
                        RefreshNetworkFilter();
                    }
                    else
                    {
                        RefreshNetworkLogList();
                        UpdateLogCount();
                    }

                    if (wasAtBottom) ScrollToBottom(_lstNetworkLogs);
                }
                else
                {
                    _networkRefreshNeedsFullFilter |= !incrementalUpdated;
                    ScheduleNetworkRefresh();
                }
            }
        }));
    }

    #endregion

    #region Network Log List

    #endregion

    #region System Log List

    #endregion

    #region Toolbar Actions

    /// <summary>
    /// 自动启动 TCP 服务器，监听指定端口。
    /// </summary>
    private void AutoStartServer()
    {
        var port = _settings.ServerPort;
        Task.Run(() =>
        {
            try
            {
                _server.Start(port);
                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        _lblStatus.Text = "\u25CF Running";
                        _lblStatus.ForeColor = Color.Green;
                        _lblServerStatus.Text = $"Server: {port}";
                    });
            }
            catch (Exception ex)
            {
                if (IsHandleCreated)
                    BeginInvoke(() =>
                    {
                        _lblStatus.Text = "\u25CB Error";
                        _lblStatus.ForeColor = Color.Red;
                        _lblServerStatus.Text = $"Server: {ex.Message}";
                        MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _btnAdbReverse.DropDownItems.Add("ADB not found").Enabled = false;
            return;
        }

        var devices = _adbHelper.GetDevices();
        if (devices.Count == 0)
        {
            _btnAdbReverse.DropDownItems.Add("No devices connected").Enabled = false;
            return;
        }

        foreach (var dev in devices)
        {
            var item = new ToolStripMenuItem(dev.DisplayName, null, (s, ev) =>
            {
                var (ok, output) = _adbHelper.ReversePort(adbPath, dev, _settings.ServerPort);
                MessageBox.Show(ok ? $"Reverse success for {dev.DisplayName}" : $"Reverse failed:\n{output}",
                    "ADB Reverse", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            });
            _btnAdbReverse.DropDownItems.Add(item);
        }

        _btnAdbReverse.DropDownItems.Add(new ToolStripSeparator());
        var allItem = new ToolStripMenuItem("Reverse All Devices", null, (s, ev) =>
        {
            var results = new List<string>();
            foreach (var dev in devices)
            {
                var (ok, output) = _adbHelper.ReversePort(adbPath, dev, _settings.ServerPort);
                results.Add($"{dev.DisplayName}: {(ok ? "OK" : output)}");
            }
            MessageBox.Show(string.Join("\n", results), "ADB Reverse All", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        _btnAdbReverse.DropDownItems.Add(allItem);
    }

    #endregion

    #region Device Panel Events

    /// <summary>
    /// 处理刷新 ADB 设备请求。
    /// </summary>
    private void OnRefreshAdbDevices(object? sender, EventArgs e)
    {
        RequestAdbScan();
    }

    /// <summary>
    /// 请求扫描 ADB 设备列表。
    /// </summary>
    private void RequestAdbScan()
    {
        if (!_adbHelper.IsAdbAvailable()) return;
        Task.Run(() =>
        {
            var devices = _adbHelper.GetDevices();
            if (IsHandleCreated)
                BeginInvoke(() => ApplyAdbDevices(devices));
        });
    }

    /// <summary>
    /// 预加载 ADB 设备列表，尝试多次扫描直到发现设备。
    /// </summary>
    private async Task PrimeAdbDeviceListAsync()
    {
        if (!_adbHelper.IsAdbAvailable())
        {
            return;
        }

        for (int attempt = 0; attempt < 4 && !IsDisposed; attempt++)
        {
            var devices = await Task.Run(() => _adbHelper.GetDevices());
            if (IsDisposed)
            {
                return;
            }

            ApplyAdbDevices(devices);
            if (devices.Count > 0)
            {
                return;
            }

            await Task.Delay(1000);
        }
    }

    private void ApplyAdbDevices(List<AdbDevice> adbDevices)
    {
        var currentSerials = new HashSet<string>(adbDevices.Select(d => d.Serial));
        _devicePanel.RemoveMissingAdbDevices(currentSerials);

        var adbPath = _adbHelper.GetAdbPath();
        var newAdbSerials = new List<string>();

        foreach (var dev in adbDevices)
        {
            var isNew = _devicePanel.AddAdbDevice(dev.Serial, dev.Model);
            if (isNew) newAdbSerials.Add(dev.Serial);

            if (!_deviceLogs.ContainsKey(dev.Serial))
                _deviceLogs[dev.Serial] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);

            var mappedDeviceId = _adbSerialToDeviceId.TryGetValue(dev.Serial, out var existingDeviceId) && !string.IsNullOrEmpty(existingDeviceId)
                ? existingDeviceId
                : dev.Serial;

            if (!_logcatReaders.ContainsKey(mappedDeviceId) && !_logcatReaders.ContainsKey(dev.Serial) && _settings.AutoStartLogcat)
            {
                if (adbPath != null)
                    StartLogcat(adbPath, dev.Serial, mappedDeviceId, _settings.LogcatFilter);
            }
        }

        if (newAdbSerials.Count > 0 && adbPath != null && _settings.AutoAdbReverse)
        {
            Task.Run(() =>
            {
                foreach (var serial in newAdbSerials)
                {
                    _adbHelper.ReversePort(adbPath, new AdbDevice { Serial = serial }, _settings.ServerPort);
                }
            });
        }

        UpdateDeviceCountStatus();
    }

    private void StartAdbScanLoop()
    {
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
                    var devices = await Task.Run(() => _adbHelper.GetDevices(), token);
                    if (IsHandleCreated && !token.IsCancellationRequested)
                        BeginInvoke(() => ApplyAdbDevices(devices));
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
        RefreshNetworkFilter();
        RefreshSystemLogList();
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

    private void OnDeleteDevice(object? sender, string deviceId)
    {
        if (_logcatReaders.TryGetValue(deviceId, out var reader))
        {
            var serial = reader.DeviceSerial;
            reader.Stop();
            _logcatReaders.Remove(deviceId);
            if (serial != null) _adbSerialToDeviceId.Remove(serial);
        }

        _deviceLogs.Remove(deviceId);
        if (IsSystemLogRuntimeReady())
        {
            _systemLogStore.ClearDevice(deviceId);
        }

        _devicePanel.RemoveDevice(deviceId);
        if (_currentDeviceId == deviceId)
        {
            _currentDeviceId = null;
            _selectedLogEntry = null;
            ShowLogDetail(null);
            StopMirror(clearStatusOnly: true);
        }

        RefreshNetworkFilter();
        RefreshSystemLogList();
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
        MessageBox.Show(ok ? $"ADB Reverse OK for {serial}" : $"Failed: {output}", "ADB Reverse", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void OnLogcatToggle(object? sender, string deviceId)
    {
        if (_logcatReaders.TryGetValue(deviceId, out var reader) && reader.IsRunning)
        {
            var serial = reader.DeviceSerial;
            reader.Stop();
            _logcatReaders.Remove(deviceId);
            if (serial != null) _adbSerialToDeviceId.Remove(serial);
        }
        else
        {
            var adbPath = _adbHelper.GetAdbPath();
            var info = _server.GetDeviceInfo(deviceId);
            var serial = info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId) ?? deviceId;
            if (adbPath != null && !string.IsNullOrEmpty(serial))
            {
                StartLogcat(adbPath, serial, deviceId, _settings.LogcatFilter);
            }
            else
            {
                MessageBox.Show("Cannot start logcat: ADB not available.", "Logcat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        UpdateLogcatStatus();
    }

    #endregion

    #region Logcat

    /// <summary>
    /// 启动指定设备的 logcat 日志读取。
    /// </summary>
    private void StartLogcat(string adbPath, string serial, string deviceId, string filter)
    {
        if (_logcatReaders.ContainsKey(deviceId)) return;
        var reader = new LogcatReader();
        reader.SystemLogReceived += OnSystemLogReceived;
        reader.ProcessExited += (s, args) => this.BeginInvoke(new Action(() =>
        {
            _logcatReaders.Remove(deviceId);
            _adbSerialToDeviceId.Remove(serial);
            UpdateLogcatStatus();
        }));
        _logcatReaders[deviceId] = reader;
        _adbSerialToDeviceId[serial] = deviceId;
        reader.Start(adbPath, serial, filter);
        UpdateLogcatStatus();
    }

    #endregion

    #region Filter

    #endregion

    #region Scroll & Count

    /// <summary>
    /// 判断列表视图是否在底部位置。
    /// </summary>
    private static bool IsAtBottom(ListView lv)
    {
        if (lv.VirtualListSize == 0) return true;
        var topIndex = lv.TopItem?.Index ?? 0;
        var visibleCount = Math.Max(1, lv.ClientSize.Height / Math.Max(1, lv.Font.Height + 6));
        return topIndex + visibleCount >= lv.VirtualListSize;
    }

    /// <summary>
    /// 滚动列表视图到底部。
    /// </summary>
    private static void ScrollToBottom(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(lv.VirtualListSize - 1); } catch { }
        }
    }

    /// <summary>
    /// 滚动列表视图到顶部。
    /// </summary>
    private static void ScrollToTop(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(0); } catch { }
        }
    }

    /// <summary>
    /// 更新日志计数显示，包括总数、过滤数和容量百分比。
    /// </summary>
    private void UpdateLogCount()
    {
        var buf = GetCurrentLogBuffer();
        var total = buf.Count;
        var filtered = _filteredNetworkIndices.Count;
        var max = _currentDeviceId == null ? _settings.MaxLogEntriesAll : _settings.MaxLogEntriesPerDevice;
        var pct = (double)total / max;
        var suffix = _networkAutoScrollEnabled && IsAtBottom(_lstNetworkLogs) ? "" : " \u2B07 Paused";
        var countText = filtered != total ? $"Logs: {filtered}/{total}" : $"Logs: {total}";
        _lblLogCount.Text = $"{countText}/{max}{suffix}";
        _lblLogCount.ForeColor = pct >= 1.0 ? Color.Red : pct >= 0.8 ? Color.Orange : DefaultForeColor;

        _btnScrollToBottom.BackColor = _networkAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
        _btnSystemScrollToBottom.BackColor = _systemAutoScrollEnabled ? Color.LightSkyBlue : DefaultBackColor;
    }

    /// <summary>
    /// 更新设备数量状态显示。
    /// </summary>
    private void UpdateDeviceCountStatus()
    {
        _lblDeviceCountStatus.Text = $"Devices: {_deviceLogs.Count}";
    }

    /// <summary>
    /// 更新 logcat 运行状态显示。
    /// </summary>
    private void UpdateLogcatStatus()
    {
        var running = _logcatReaders.Values.Count(r => r.IsRunning);
        _lblLogcatStatus.Text = $"Logcat: {running}";
    }

    #endregion

    #region Bottom Bar Actions

    /// <summary>
    /// 清除当前设备或所有设备的日志。
    /// </summary>
    private void OnClear(object? sender, EventArgs e)
    {
        if (_currentDeviceId != null)
        {
            if (_deviceLogs.TryGetValue(_currentDeviceId, out var buf)) buf.Clear();
            if (IsSystemLogRuntimeReady())
            {
                _systemLogStore.ClearDevice(_currentDeviceId);
            }
        }
        else
        {
            foreach (var buf in _deviceLogs.Values) buf.Clear();
            _allLogs.Clear();
            if (IsSystemLogRuntimeReady())
            {
                _systemLogStore.RotateSession();
            }
        }

        _filteredNetworkIndices.Clear();
        _selectedLogEntry = null;
        ShowLogDetail(null);
        RefreshNetworkFilter();
        RefreshSystemLogList();
        RefreshMirrorPanelState();
    }

    #endregion

    #region Settings

    /// <summary>
    /// 打开设置对话框，应用新的设置。
    /// </summary>
    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings, _adbHelper);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings = AppSettings.Load();
            ApplySettings();

            _scrcpyDeployError = null;
            _scrcpyDeployStatus = null;

            foreach (var kvp in _deviceLogs)
            {
                kvp.Value.Resize(_settings.MaxLogEntriesPerDevice);
            }
            _allLogs.Resize(_settings.MaxLogEntriesAll);
            if (IsSystemLogRuntimeReady())
            {
                _systemLogStore.UpdateHotCapacity(_settings.MaxSystemLogEntries);
                RefreshSystemLogList();
            }

            UpdateAdbStatus();
            RefreshMirrorPanelState();
        }
    }

    #endregion

    /// <summary>
    /// 窗口关闭前清理所有资源。
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopAdbScanLoop();
        StopMirror(clearStatusOnly: true);
        _scrcpyStartCts?.Cancel();
        _scrcpyStartCts?.Dispose();
        _mirrorRestartTimer?.Stop();
        _mirrorRestartTimer?.Dispose();
        foreach (var session in _externalScrcpySessions.ToArray())
        {
            session.Dispose();
        }
        _externalScrcpySessions.Clear();
        foreach (var reader in _logcatReaders.Values) reader.Stop();
        _systemSnapshotCts?.Cancel();
        _systemSnapshotCts?.Dispose();
        _systemPrefetchCts?.Cancel();
        _systemPrefetchCts?.Dispose();
        if (_systemLogStore is not null)
        {
            _systemLogStore.Dispose();
        }
        _server.Stop();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.End)
        {
            if (_showingSystemLog)
            {
                _systemAutoScrollEnabled = true;
                ScrollToBottom(_lstSystemLogs);
            }
            else
            {
                _networkAutoScrollEnabled = true;
                ScrollToBottom(_lstNetworkLogs);
            }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnMainFormLoad(object? sender, EventArgs e)
    {
        RefreshMirrorPanelState();
    }

    private void RefreshMirrorPanelState()
    {
        if (IsDesignTimeMode())
        {
            return;
        }

        UpdateAdbStatus();

        if (string.IsNullOrEmpty(_currentDeviceId))
        {
            _devicePanel.SetMirrorStatus("\u8BF7\u9009\u62E9\u5177\u4F53\u8BBE\u5907\u4EE5\u64CD\u63A7\u624B\u673A", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        var serial = ResolveAdbSerial(_currentDeviceId);
        if (string.IsNullOrEmpty(serial))
        {
            _devicePanel.SetMirrorStatus("\u5F53\u524D\u8BBE\u5907\u672A\u5339\u914D ADB serial\uFF0C\u65E0\u6CD5\u542F\u52A8\u624B\u673A\u955C\u50CF", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (_scrcpyPreparing)
        {
            _devicePanel.SetMirrorStatus(_scrcpyDeployStatus ?? "\u6B63\u5728\u90E8\u7F72 scrcpy...", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (string.Equals(_mirrorStartingSerial, serial, StringComparison.Ordinal))
        {
            _devicePanel.SetMirrorStatus($"\u6B63\u5728\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        var scrcpyPath = _scrcpyManager.GetScrcpyPath();
        if (string.IsNullOrEmpty(scrcpyPath) || !_scrcpyValidated)
        {
            var message = !string.IsNullOrEmpty(_scrcpyDeployError)
                ? $"\u81EA\u52A8\u90E8\u7F72 scrcpy \u5931\u8D25\uFF1A{_scrcpyDeployError}"
                : string.IsNullOrEmpty(scrcpyPath)
                    ? "\u672A\u5B8C\u6210 scrcpy \u90E8\u7F72\uFF0C\u7A0D\u540E\u4F1A\u81EA\u52A8\u51C6\u5907"
                    : "scrcpy 校验中...";
            _devicePanel.SetMirrorStatus(message, hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (_scrcpySession?.IsRunning == true && string.Equals(_scrcpySession.DeviceSerial, serial, StringComparison.Ordinal))
        {
            _devicePanel.SetMirrorStatus($"\u955C\u50CF\u5DF2\u8FDE\u63A5\uFF1A{serial}", hostVisible: true, isRunning: true, isReady: true);
            return;
        }

        _devicePanel.SetMirrorStatus($"\u5DF2\u5C31\u7EEA\uFF0C\u53EF\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
    }

    private async Task<string?> EnsureScrcpyReadyAsync(bool forceDeploy, bool reportToMirrorPanel)
    {
        try
        {
            _scrcpyPreparing = true;
            _scrcpyDeployError = null;
            _scrcpyDeployStatus = "\u6B63\u5728\u90E8\u7F72 scrcpy...";
            UpdateAdbStatus();

            if (reportToMirrorPanel && !string.IsNullOrEmpty(_currentDeviceId))
            {
                _devicePanel.SetMirrorStatus(_scrcpyDeployStatus, hostVisible: false, isRunning: false, isReady: false);
            }

            var progress = new Progress<string>(message =>
            {
                _scrcpyDeployStatus = message;
                UpdateAdbStatus();

                if (reportToMirrorPanel && !string.IsNullOrEmpty(_currentDeviceId) && _scrcpySession?.IsRunning != true)
                {
                    _devicePanel.SetMirrorStatus(message, hostVisible: false, isRunning: false, isReady: false);
                }
            });

            var scrcpyPath = await _scrcpyManager
                .EnsureScrcpyAvailableAsync(forceDeploy, progress, CancellationToken.None)
                .ConfigureAwait(true);

            if (!string.IsNullOrEmpty(scrcpyPath))
            {
                _scrcpyDeployStatus = $"\u5DF2\u5B8C\u6210 scrcpy \u90E8\u7F72\uFF1A{Path.GetFileName(scrcpyPath)}";
            }

            return scrcpyPath;
        }
        catch (Exception ex)
        {
            _scrcpyDeployError = ex.Message;
            _scrcpyDeployStatus = null;
            return null;
        }
        finally
        {
            _scrcpyPreparing = false;
            UpdateAdbStatus();

            if (reportToMirrorPanel)
            {
                RefreshMirrorPanelState();
            }
        }
    }

    private string? ResolveAdbSerial(string deviceId)
    {
        var info = _server.GetDeviceInfo(deviceId);
        return info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId);
    }

    private async void OnMirrorStartRequested(object? sender, string deviceId)
    {
        if (!string.Equals(deviceId, _currentDeviceId, StringComparison.Ordinal))
        {
            _currentDeviceId = deviceId;
        }

        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private void OnMirrorStopRequested(object? sender, string deviceId)
    {
        StopMirror(clearStatusOnly: false);
        RefreshMirrorPanelState();
    }

    private async void OnMirrorReconnectRequested(object? sender, string deviceId)
    {
        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private async void OnMirrorRotateRequested(object? sender, string deviceId)
    {
        _scrcpyRotationIndex = (_scrcpyRotationIndex + 1) % 4;
        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private async void OnMirrorPopoutRequested(object? sender, string deviceId)
    {
        await StartMirrorSessionAsync(embedded: false, restartCurrent: false);
    }

    private void OnMirrorScreenshotRequested(object? sender, string deviceId)
    {
        CaptureDeviceScreenshot(deviceId);
    }

    private async Task StartMirrorForCurrentDeviceAsync(bool restart)
    {
        if (string.IsNullOrEmpty(_currentDeviceId))
        {
            RefreshMirrorPanelState();
            return;
        }

        await StartMirrorSessionAsync(embedded: true, restartCurrent: restart);
    }

    private async Task StartMirrorSessionAsync(bool embedded, bool restartCurrent)
    {
        var deviceId = _currentDeviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            RefreshMirrorPanelState();
            return;
        }

        var serial = ResolveAdbSerial(deviceId);
        if (string.IsNullOrEmpty(serial))
        {
            RefreshMirrorPanelState();
            return;
        }

        var scrcpyPath = await EnsureScrcpyReadyAsync(forceDeploy: false, reportToMirrorPanel: true);
        if (string.IsNullOrEmpty(scrcpyPath))
        {
            RefreshMirrorPanelState();
            return;
        }

        _scrcpyManager.TerminateBundledProcesses(scrcpyPath);

        if (restartCurrent)
        {
            StopMirror(clearStatusOnly: true);
        }

        _scrcpyStartCts?.Cancel();
        _scrcpyStartCts?.Dispose();
        _scrcpyStartCts = new CancellationTokenSource();
        var token = _scrcpyStartCts.Token;

        if (embedded)
        {
            _mirrorStartingSerial = serial;
            _devicePanel.SetMirrorAspectRatio(GetDeviceContentAspectRatio(serial));
            _devicePanel.SetMirrorStatus($"\u6B63\u5728\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
        }

        try
        {
             var contentAspectRatio = GetDeviceContentAspectRatio(serial);
             var hostHandle = embedded ? _devicePanel.EnsureMirrorHostHandle() : IntPtr.Zero;
             var windowBounds = embedded ? _devicePanel.GetMirrorDisplayBounds() : Rectangle.Empty;
             var session = await _scrcpyManager.StartSessionAsync(new ScrcpyStartOptions
             {
                 ScrcpyPath = scrcpyPath,
                 DeviceSerial = serial,
                 WindowTitle = $"LogViewer.scrcpy.{serial}.{Guid.NewGuid():N}",
                 Mode = embedded ? ScrcpySessionMode.Embedded : ScrcpySessionMode.External,
                 HostHandle = hostHandle,
                 AngleDegrees = _scrcpyRotationIndex * 90,
                 ContentAspectRatio = contentAspectRatio,
                 WindowX = windowBounds.X,
                 WindowY = windowBounds.Y,
                 WindowWidth = windowBounds.Width,
                 WindowHeight = windowBounds.Height
             }, token).ConfigureAwait(false);

            if (token.IsCancellationRequested || IsDisposed)
            {
                session.Dispose();
                return;
            }

            if (embedded)
            {
                BeginInvoke(new Action(() =>
                {
                    _mirrorStartingSerial = null;
                    _scrcpySession?.Dispose();
                    _scrcpySession = session;
                    _scrcpySession.Exited += OnScrcpySessionExited;
                    _devicePanel.SetMirrorAspectRatio(contentAspectRatio);
                    _devicePanel.SetMirrorStatus($"\u955C\u50CF\u5DF2\u8FDE\u63A5\uFF1A{serial}", hostVisible: true, isRunning: true, isReady: true);
                    ApplyEmbeddedMirrorLayout();
                }));
            }
            else
            {
                BeginInvoke(new Action(() =>
                {
                    _externalScrcpySessions.Add(session);
                    session.Exited += (_, _) =>
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke(new Action(() => _externalScrcpySessions.Remove(session)));
                        }
                    };
                }));
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    _mirrorStartingSerial = null;
                    _devicePanel.SetMirrorStatus($"\u955C\u50CF\u542F\u52A8\u5931\u8D25\uFF1A{ex.Message}", hostVisible: false, isRunning: false, isReady: false);
                }));
            }
        }
    }

    private void OnScrcpySessionExited(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            _mirrorStartingSerial = null;
            _scrcpySession?.Dispose();
            _scrcpySession = null;
            RefreshMirrorPanelState();
        }));
    }

    private void StopMirror(bool clearStatusOnly)
    {
        _scrcpyStartCts?.Cancel();
        _mirrorStartingSerial = null;
        _mirrorRestartPending = false;
        _scrcpySession?.Dispose();
        _scrcpySession = null;
        if (clearStatusOnly)
        {
            _devicePanel.ClearMirrorHost();
        }
    }

    private void ScheduleEmbeddedMirrorRestart()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (_scrcpySession?.IsRunning != true ||
            string.IsNullOrEmpty(_currentDeviceId) ||
            _mirrorStartingSerial != null ||
            _mirrorRestartInProgress)
        {
            return;
        }

        _mirrorRestartPending = true;
        _mirrorRestartTimer ??= CreateMirrorRestartTimer();
        _mirrorRestartTimer.Stop();
        _mirrorRestartTimer.Start();
    }

    private void ApplyEmbeddedMirrorLayout()
    {
        if (_scrcpySession?.IsRunning != true)
            return;

        _devicePanel.SyncMirrorBounds();
        var bounds = _devicePanel.GetMirrorDisplayBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        _scrcpySession.ResizeEmbeddedBounds(bounds);
    }

    private System.Windows.Forms.Timer CreateMirrorRestartTimer()
    {
        var timer = new System.Windows.Forms.Timer
        {
            Interval = 260
        };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            if (!_mirrorRestartPending ||
                _mirrorRestartInProgress ||
                _mirrorStartingSerial != null ||
                IsDisposed ||
                !IsHandleCreated ||
                string.IsNullOrEmpty(_currentDeviceId))
            {
                return;
            }

            _mirrorRestartPending = false;
            _mirrorRestartInProgress = true;
            try
            {
                await StartMirrorForCurrentDeviceAsync(restart: true);
            }
            finally
            {
                _mirrorRestartInProgress = false;
            }
        };
        return timer;
    }

    private void CaptureDeviceScreenshot(string deviceId)
    {
        var adbPath = _adbHelper.GetAdbPath();
        var serial = ResolveAdbSerial(deviceId);
        if (adbPath == null || string.IsNullOrEmpty(serial))
        {
            RefreshMirrorPanelState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"screenshot_{serial}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            var bytes = RunAdbBinary(adbPath, serial, "exec-out screencap -p");
            File.WriteAllBytes(dialog.FileName, bytes);
            _devicePanel.SetMirrorStatus($"\u622A\u56FE\u5DF2\u4FDD\u5B58\uFF1A{Path.GetFileName(dialog.FileName)}", hostVisible: _scrcpySession?.IsRunning == true, isRunning: _scrcpySession?.IsRunning == true, isReady: _scrcpySession != null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"\u622A\u56FE\u5931\u8D25\uFF1A{ex.Message}", "Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshMirrorPanelState();
        }
    }

    private static byte[] RunAdbBinary(string adbPath, string serial, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = $"-s {serial} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start adb.");

        using var memory = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(memory);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "adb screenshot failed." : error.Trim());
        }

        return memory.ToArray();
    }

    private double GetDeviceContentAspectRatio(string serial)
    {
        var adbPath = _adbHelper.GetAdbPath();
        if (string.IsNullOrEmpty(adbPath))
        {
            return 9d / 16d;
        }

        try
        {
            var output = System.Text.Encoding.UTF8.GetString(RunAdbBinary(adbPath, serial, "shell wm size"));
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)\s*x\s*(\d+)");
            if (!match.Success)
            {
                return 9d / 16d;
            }

            var width = int.Parse(match.Groups[1].Value);
            var height = int.Parse(match.Groups[2].Value);
            if (width <= 0 || height <= 0)
            {
                return 9d / 16d;
            }

            var normalizedWidth = width;
            var normalizedHeight = height;
            if ((_scrcpyRotationIndex % 2) == 1)
            {
                normalizedWidth = height;
                normalizedHeight = width;
            }

            return (double)normalizedWidth / normalizedHeight;
        }
        catch
        {
            return 9d / 16d;
        }
    }

}
