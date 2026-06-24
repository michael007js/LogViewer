using System.ComponentModel;
using System.Diagnostics;
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

    /// <summary>ADB 是否已验证可用。</summary>
    private bool _adbValidated;

    /// <summary>当前选中的设备 deviceId。</summary>
    private string? _currentDeviceId;

    /// <summary>当前是否显示系统日志（true=系统日志，false=网络日志）。</summary>
    private bool _showingSystemLog;

    /// <summary>系统日志自动滚动是否启用。</summary>
    private bool _systemAutoScrollEnabled = true;

    /// <summary>外层水平分割容器，左侧为设备面板，右侧为日志区域。</summary>
    private SplitContainer _outerSplit;

    /// <summary>内层水平分割容器，上侧为日志列表，下侧为详情面板。</summary>
    private SplitContainer _innerSplit;

    /// <summary>左侧 ADB 设备操控面板，包含设备选择、scrcpy 宿主和控制条。</summary>
    private DevicePanel _devicePanel;

    /// <summary>日志类型切换 TabControl，切换网络日志与系统日志视图。</summary>
    private TabControl _tabLogType;

    /// <summary>网络日志 Tab 页。</summary>
    private TabPage _tabNetwork;

    /// <summary>系统日志 Tab 页。</summary>
    private TabPage _tabSystem;

    /// <summary>网络日志过滤条件面板。</summary>
    private Panel _pnlNetworkFilter;

    /// <summary>网络日志关键字搜索输入框。</summary>
    private TextBox _txtNetworkKeyword;

    /// <summary>HTTP 方法过滤下拉框（ALL/GET/POST/PUT/DELETE/PATCH）。</summary>
    private ComboBox _cmbMethod;

    /// <summary>HTTP 状态码范围过滤下拉框（ALL/2xx/3xx/4xx/5xx/0）。</summary>
    private ComboBox _cmbStatusCode;

    /// <summary>滚动到顶部按钮，同时禁用自动滚动。</summary>
    private Button _btnScrollToTop;

    /// <summary>滚动到底部按钮，同时启用自动滚动。</summary>
    private Button _btnScrollToBottom;

    /// <summary>网络日志计数标签，显示过滤数/总数/容量百分比。</summary>
    private Label _lblLogCount;

    /// <summary>网络日志列表，使用 VirtualMode 渲染百万级数据。</summary>
    private ListView _lstNetworkLogs;

    /// <summary>系统日志列表，使用 VirtualMode 渲染。</summary>
    private ListView _lstSystemLogs;

    /// <summary>系统日志过滤条件面板。</summary>
    private Panel _pnlSystemFilter;

    /// <summary>系统日志操作栏面板（Pause/Resume、滚动等）。</summary>
    private Panel _systemActionBar;

    /// <summary>系统日志关键字搜索输入框。</summary>
    private TextBox _txtSystemKeyword;

    /// <summary>系统日志级别过滤下拉框（ALL/V/D/I/W/E/F）。</summary>
    private ComboBox _cmbLogLevel;

    /// <summary>系统日志 Tag 过滤下拉框，选项动态刷新。</summary>
    private ComboBox _cmbLogTag;

    /// <summary>系统日志滚动到顶部按钮。</summary>
    private Button _btnSystemScrollToTop;

    /// <summary>系统日志滚动到底部按钮。</summary>
    private Button _btnSystemScrollToBottom;

    /// <summary>系统日志暂停/恢复按钮，Pause 冻结视图并累计 backlog。</summary>
    private Button _btnSystemPauseResume;

    /// <summary>系统日志 backlog 计数标签，显示 Pause 期间累积的未刷新条数。</summary>
    private Label _lblSystemBacklog;

    /// <summary>日志详情 TabControl，包含 Headers/RequestBody/ResponseBody 三个 Tab。</summary>
    private TabControl _tabDetail;

    /// <summary>请求头 Tab 页。</summary>
    private TabPage _tabHeaders;

    /// <summary>请求体 Tab 页。</summary>
    private TabPage _tabRequestBody;

    /// <summary>响应体 Tab 页。</summary>
    private TabPage _tabResponseBody;

    /// <summary>Headers JSON 折叠视图容器面板。</summary>
    private Panel _jsonHeaders;

    /// <summary>请求体 JSON 折叠视图容器面板。</summary>
    private Panel _jsonRequestBody;

    /// <summary>响应体 JSON 折叠视图容器面板。</summary>
    private Panel _jsonResponseBody;

    /// <summary>Headers JSON 折叠+语法高亮 TreeView。</summary>
    private JsonTreeView? _jsonHeadersView;

    /// <summary>请求体 JSON 折叠+语法高亮 TreeView。</summary>
    private JsonTreeView? _jsonRequestBodyView;

    /// <summary>响应体 JSON 折叠+语法高亮 TreeView。</summary>
    private JsonTreeView? _jsonResponseBodyView;

    /// <summary>Headers 原始 JSON 文本框（Raw 视图）。</summary>
    private TextBox _rawHeaders;

    /// <summary>请求体原始 JSON 文本框（Raw 视图）。</summary>
    private TextBox _rawRequestBody;

    /// <summary>响应体原始 JSON 文本框（Raw 视图）。</summary>
    private TextBox _rawResponseBody;

    /// <summary>顶部工具栏，包含服务器状态和 ADB Reverse 按钮。</summary>
    private ToolStrip _toolStrip;

    /// <summary>ADB Reverse 下拉按钮，动态列出可执行 Reverse 的设备。</summary>
    private ToolStripDropDownButton _btnAdbReverse;

    /// <summary>工具栏服务器运行状态标签（Running/Error）。</summary>
    private ToolStripLabel _lblStatus;

    /// <summary>主菜单栏。</summary>
    private MenuStrip _menuStrip;

    /// <summary>底部状态栏。</summary>
    private StatusStrip _statusStrip;

    /// <summary>状态栏：服务器端口/错误信息。</summary>
    private ToolStripStatusLabel _lblServerStatus;

    /// <summary>状态栏：已连接设备数量。</summary>
    private ToolStripStatusLabel _lblDeviceCountStatus;

    /// <summary>状态栏：ADB/scrcpy 就绪状态。</summary>
    private ToolStripStatusLabel _lblAdbStatus;

    /// <summary>状态栏：正在运行的 Logcat 进程数量。</summary>
    private ToolStripStatusLabel _lblLogcatStatus;

    /// <summary>底部操作栏面板，包含清空和导出按钮。</summary>
    private FlowLayoutPanel _pnlBottomBar;

    /// <summary>清空当前日志按钮。</summary>
    private Button _btnClear;

    /// <summary>导出为 JSON 文件按钮。</summary>
    private Button _btnExportJson;

    /// <summary>导出为纯文本文件按钮。</summary>
    private Button _btnExportTxt;

    /// <summary>当前选中的网络日志条目，用于详情面板展示。</summary>
    private LogEntry? _selectedLogEntry;

    /// <summary>
    /// 初始化主窗口，加载设置并配置组件。
    /// </summary>
    public MainForm()
    {
        _settings = AppSettings.Load();
        _allLogs = new RingBuffer<LogEntry>(_settings.MaxLogEntriesAll);
        InitializeComponent();
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icon.ico"));
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
        _devicePanel.MirrorLayoutChanged += OnMirrorLayoutChanged;
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

    /// <summary>
    /// ADB 缺失时首次显示提示，询问用户是否打开设置对话框配置 ADB 路径。
    /// </summary>
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

    /// <summary>
    /// 判断当前是否处于设计时模式（Visual Studio/Rider 设计器宿主进程），
    /// 避免在设计期执行运行时逻辑（如网络连接、进程启动）。
    /// </summary>
    /// <returns>true 表示处于设计时模式，应跳过运行时初始化。</returns>
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
               commandLine.Contains("JetBrains.ReSharper.Features.WinForms.Designer.External.Core",
                   StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("WinFormsDesigner", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 更新底部状态栏的 ADB 和 scrcpy 就绪状态文本及颜色。
    /// </summary>
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
            : string.IsNullOrEmpty(_scrcpyDeployError)
                ? Color.Green
                : Color.DarkOrange;
    }

    /// <summary>
    /// 窗口首次显示后执行异步初始化：验证 ADB/scrcpy 工具、预加载设备列表、部署 scrcpy。
    /// </summary>
    private async Task OnMainFormShownAsync()
    {
        _ = ValidateBundledToolsAsync();
        await PrimeAdbDeviceListAsync();
        await EnsureScrcpyReadyAsync(forceDeploy: false, reportToMirrorPanel: false);
    }

    /// <summary>
    /// 异步验证捆绑的 ADB 和 scrcpy 工具是否可用，通过后台线程执行验证后切回 UI 线程更新状态。
    /// </summary>
    private async Task ValidateBundledToolsAsync()
    {
        var adbPath = _adbHelper.GetAdbPath();
        var scrcpyPath = _scrcpyManager.GetScrcpyPath();

        var adbValid = !string.IsNullOrEmpty(adbPath) && await Task.Run(() => _adbHelper.ValidateAdb(adbPath));
        var scrcpyValid = !string.IsNullOrEmpty(scrcpyPath) &&
                          await Task.Run(() => _scrcpyManager.ValidateScrcpy(scrcpyPath));

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

    /// <summary>
    /// 将 ADB serial 索引的日志缓冲区、logcat 读取器和当前设备 ID 重新绑定到 TCP deviceId，
    /// 确保设备身份从 ADB serial 过渡到 deviceId 后数据不丢失。
    /// </summary>
    /// <param name="adbSerial">ADB 序列号。</param>
    /// <param name="deviceId">TCP 协议中的设备 ID。</param>
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

    /// <summary>
    /// 处理设备断开连接事件，标记设备离线并请求 ADB 重新扫描。
    /// </summary>
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

    /// <summary>
    /// 处理网络日志接收事件，将日志写入设备缓冲区和合并缓冲区，
    /// 根据当前视图和自动滚动状态决定增量刷新或延迟刷新。
    /// </summary>
    private void OnLogReceived(object? sender, (string deviceId, LogEntry entry) args)
    {
        this.BeginInvoke(new Action(() =>
        {
            var (deviceId, entry) = args;
            var showingCurrentNetwork =
                !_showingSystemLog && (_currentDeviceId == deviceId || _currentDeviceId == null);
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
                var incrementalUpdated =
                    TryAppendNetworkLogIncrementally(entry, activeViewCountBeforeAdd, activeViewWasFull);
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

    // 网络日志列表的配置、过滤、显示、交互逻辑见 MainForm.NetworkLogs.cs（partial class）

    #endregion

    #region System Log List

    // 系统日志快照、过滤、Pause/Resume 运行时逻辑见 MainForm.SystemLogs.cs（partial class）

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
                        MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
            }
        });
    }

    /// <summary>
    /// ADB Reverse 下拉菜单展开时动态填充设备列表，
    /// 每个设备一个 Reverse 项，最后附加"全部 Reverse"选项。
    /// </summary>
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

            MessageBox.Show(string.Join("\n", results), "ADB Reverse All", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

    /// <summary>
    /// 应用 ADB 扫描发现的设备列表：移除离线设备、为新设备创建日志缓冲区、
    /// 自动启动 Logcat、对新增设备执行 adb reverse 端口映射。
    /// </summary>
    /// <param name="adbDevices">当前 ADB 扫描发现的设备列表。</param>
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

            var mappedDeviceId = _adbSerialToDeviceId.TryGetValue(dev.Serial, out var existingDeviceId) &&
                                 !string.IsNullOrEmpty(existingDeviceId)
                ? existingDeviceId
                : dev.Serial;

            if (!_logcatReaders.ContainsKey(mappedDeviceId) && !_logcatReaders.ContainsKey(dev.Serial) &&
                _settings.AutoStartLogcat)
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

    /// <summary>
    /// 启动 ADB 设备定时扫描循环，先取消旧循环再创建新的 CancellationTokenSource。
    /// </summary>
    private void StartAdbScanLoop()
    {
        StopAdbScanLoop();
        _adbScanCts = new CancellationTokenSource();
        _ = ScanLoopAsync(_adbScanCts.Token);
    }

    /// <summary>
    /// ADB 设备扫描循环，按配置间隔定时扫描设备列表并通过 BeginInvoke 更新 UI。
    /// 出错时延长等待时间避免频繁重试。
    /// </summary>
    /// <param name="token">取消令牌，用于终止扫描循环。</param>
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(10000, token);
            }
        }
    }

    /// <summary>
    /// 停止 ADB 设备扫描循环，取消并释放 CancellationTokenSource。
    /// </summary>
    private void StopAdbScanLoop()
    {
        _adbScanCts?.Cancel();
        _adbScanCts?.Dispose();
        _adbScanCts = null;
    }

    /// <summary>
    /// 处理设备选择变更事件，切换当前设备 ID、刷新日志视图和详情面板，
    /// 并根据设置决定是否自动启动 scrcpy 投屏。
    /// </summary>
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

    /// <summary>
    /// 处理删除设备事件，停止该设备的 logcat、清理缓冲区和系统日志存储、
    /// 从面板移除设备，若删除的是当前选中设备则重置视图。
    /// </summary>
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

    /// <summary>
    /// 对指定设备执行 ADB Reverse 端口映射，将服务端口映射到 Android 端。
    /// </summary>
    private void OnAdbReverseForDevice(object? sender, string deviceId)
    {
        var adbPath = _adbHelper.GetAdbPath();
        if (adbPath == null) return;
        int port = _settings.ServerPort;
        var info = _server.GetDeviceInfo(deviceId);
        var serial = info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId) ?? deviceId;
        var dev = new AdbDevice { Serial = serial };
        var (ok, output) = _adbHelper.ReversePort(adbPath, dev, port);
        MessageBox.Show(ok ? $"ADB Reverse OK for {serial}" : $"Failed: {output}", "ADB Reverse", MessageBoxButtons.OK,
            ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    /// <summary>
    /// 切换指定设备的 Logcat 读取状态：运行中则停止，已停止则重新启动。
    /// </summary>
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
                MessageBox.Show("Cannot start logcat: ADB not available.", "Logcat", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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

    // 网络日志/系统日志过滤逻辑见各自的 partial class 文件

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
            try
            {
                lv.EnsureVisible(lv.VirtualListSize - 1);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 滚动列表视图到顶部。
    /// </summary>
    private static void ScrollToTop(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try
            {
                lv.EnsureVisible(0);
            }
            catch
            {
            }
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

    /// <summary>
    /// 处理快捷键，End 键切换到底部并启用自动滚动。
    /// </summary>
    /// <returns>true 表示已处理该快捷键，不再传递。</returns>
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

    /// <summary>
    /// 窗口 Load 事件处理，初始化 scrcpy 镜像面板状态。
    /// </summary>
    private void OnMainFormLoad(object? sender, EventArgs e)
    {
        RefreshMirrorPanelState();
    }
}