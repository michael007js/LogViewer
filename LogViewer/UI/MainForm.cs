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
    private CancellationTokenSource? _adbScanCts;
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

    private NetworkLogForm _networkLogForm = null!;
    private NormalLogForm _normalLogForm = null!;
    private SystemLogForm _systemLogForm = null!;

    private SplitContainer _outerSplit;
    private SplitContainer _innerSplit;
    private DevicePanel _devicePanel;
    private TabControl _tabLogType;
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
    private FlowLayoutPanel _pnlBottomBar;
    private Button _btnClear;
    private Button _btnExportJson;
    private Button _btnExportTxt;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _allLogs = new RingBuffer<LogEntry>(_settings.MaxLogEntriesAll);
        _allNormalLogs = new RingBuffer<LogEntry>(_settings.MaxNormalLogEntries);
        InitializeComponent();
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "icon.ico"));

        if (IsDesignTimeMode()) return;

        CreateAndEmbedLogForms();
        ApplyLanguage();
        InitializeJsonTreeViewsRuntime();
        WireComponentEvents();
        ApplySettings();

        if (_adbHelper.IsAdbAvailable())
        {
            Task.Run(() =>
            {
                _adbHelper.EnsureServerStarted();
                if (IsHandleCreated) BeginInvoke(StartAdbScanLoop);
            });
        }
        else
        {
            Shown += OnMissingAdbPromptShown;
            StartAdbScanLoop();
        }

        AutoStartServer();
    }

    private void CreateAndEmbedLogForms()
    {
        InitializeSystemLogRuntime();

        _networkLogForm = new NetworkLogForm(_deviceLogs, _allLogs, _settings, () => _currentDeviceId);
        _normalLogForm = new NormalLogForm(_deviceNormalLogs, _allNormalLogs, _settings, () => _currentDeviceId);
        _systemLogForm = new SystemLogForm(_systemLogStore, _settings, () => _currentDeviceId, _adbSerialToDeviceId, () => _showingSystemLog);

        EmbedFormInTab(_networkLogForm, _tabNetwork);
        EmbedFormInTab(_normalLogForm, _tabNormal);
        EmbedFormInTab(_systemLogForm, _tabSystem);
    }

    private void EmbedFormInTab(Form form, TabPage tab)
    {
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        tab.Controls.Clear();
        tab.Controls.Add(form);
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

        _tabLogType.SelectedIndexChanged += (s, e) =>
        {
            _showingNormalLog = _tabLogType.SelectedTab == _tabNormal;
            _showingSystemLog = _tabLogType.SelectedTab == _tabSystem;
            if (_showingSystemLog)
                _systemLogForm.RefreshSystemLogList(preferBackground: true);
            else if (_showingNormalLog)
                _normalLogForm.RebuildFilter();
            else
                _networkLogForm.RebuildFilter();
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
        _normalLogForm.RebuildFilter();
        _systemLogForm.RefreshSystemLogList();

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
        var font = new Font("Consolas", _settings.FontSize);
        _networkLogForm.ApplyFont(font);
        _normalLogForm.ApplyFont(font);
        _systemLogForm.ApplyFont(font);
        _jsonHeadersView?.SetFont(font);
        _jsonRequestBodyView?.SetFont(font);
        _jsonResponseBodyView?.SetFont(font);
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

    private void OnDeviceConnected(object? sender, DeviceInfo info)
    {
        this.BeginInvoke(new Action(() =>
        {
            var id = info.DeviceId ?? "";
            if (!_deviceLogs.ContainsKey(id))
                _deviceLogs[id] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);
            if (!_deviceNormalLogs.ContainsKey(id))
                _deviceNormalLogs[id] = new RingBuffer<LogEntry>(_settings.MaxNormalLogEntriesPerDevice);
            TryMatchAdbSerial(info);
            _devicePanel.AddOrUpdateDevice(info, 0);
            UpdateDeviceCountStatus();
            RefreshMirrorPanelState();
            if (_settings.AutoStartLogcat && _adbHelper.IsAdbAvailable())
            {
                var adbPath = _adbHelper.GetAdbPath();
                if (adbPath != null && !string.IsNullOrEmpty(info.AdbSerial))
                    StartLogcat(adbPath, info.AdbSerial, id, _settings.LogcatFilter);
            }
        }));
    }

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

        if (_currentDeviceId == adbSerial) _currentDeviceId = deviceId;
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
                _networkLogForm.OnLogAdded(entry, true, activeViewCountBeforeAdd, activeViewWasFull);
        }));
    }

    private void OnNormalLogReceived(object? sender, (string deviceId, LogEntry entry) args)
    {
        this.BeginInvoke(new Action(() =>
        {
            var (deviceId, entry) = args;
            var showingCurrentNormal =
                _showingNormalLog && (_currentDeviceId == deviceId || _currentDeviceId == null);
            var activeViewCountBeforeAdd = 0;
            var activeViewWasFull = false;

            if (showingCurrentNormal)
            {
                if (_currentDeviceId == null)
                {
                    activeViewCountBeforeAdd = _allNormalLogs.Count;
                    activeViewWasFull = _allNormalLogs.Count >= _allNormalLogs.Capacity;
                }
                else if (_deviceNormalLogs.TryGetValue(deviceId, out var activeDeviceBuf))
                {
                    activeViewCountBeforeAdd = activeDeviceBuf.Count;
                    activeViewWasFull = activeDeviceBuf.Count >= activeDeviceBuf.Capacity;
                }
            }

            if (_deviceNormalLogs.TryGetValue(deviceId, out var deviceBuf))
                deviceBuf.Add(entry);
            _allNormalLogs.Add(entry);

            if (showingCurrentNormal)
                _normalLogForm.OnNormalLogAdded(entry, true, activeViewCountBeforeAdd, activeViewWasFull);
        }));
    }

    private void OnSystemLogReceived(object? sender, SystemLogEntry entry)
    {
        lock (_pendingSystemLogsLock)
        {
            _pendingSystemLogs.Enqueue(entry);
            if (_systemLogFlushScheduled) return;
            _systemLogFlushScheduled = true;
        }

        _ = Task.Run(FlushPendingSystemLogsAsync);
    }

    private async Task FlushPendingSystemLogsAsync()
    {
        while (true)
        {
            var entries = new List<SystemLogEntry>();
            lock (_pendingSystemLogsLock)
            {
                while (_pendingSystemLogs.Count > 0 && entries.Count < 200)
                    entries.Add(_pendingSystemLogs.Dequeue());
                if (entries.Count == 0) { _systemLogFlushScheduled = false; return; }
            }

            if (!_systemLogForm.IsRuntimeReady()) continue;
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => _systemLogForm.ProcessPendingLogs(entries)));
        }
    }

    #endregion

    #region Toolbar Actions

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
        if (!_adbHelper.IsAdbAvailable()) return;
        Task.Run(() =>
        {
            var devices = _adbHelper.GetDevices();
            if (IsHandleCreated) BeginInvoke(() => ApplyAdbDevices(devices));
        });
    }

    private async Task PrimeAdbDeviceListAsync()
    {
        if (!_adbHelper.IsAdbAvailable()) return;
        for (int attempt = 0; attempt < 4 && !IsDisposed; attempt++)
        {
            var devices = await Task.Run(() => _adbHelper.GetDevices());
            if (IsDisposed) return;
            ApplyAdbDevices(devices);
            if (devices.Count > 0) return;
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
            if (!_deviceNormalLogs.ContainsKey(dev.Serial))
                _deviceNormalLogs[dev.Serial] = new RingBuffer<LogEntry>(_settings.MaxNormalLogEntriesPerDevice);

            var mappedDeviceId = _adbSerialToDeviceId.TryGetValue(dev.Serial, out var existingDeviceId) &&
                                 !string.IsNullOrEmpty(existingDeviceId)
                ? existingDeviceId
                : dev.Serial;

            if (!_logcatReaders.ContainsKey(mappedDeviceId) && !_logcatReaders.ContainsKey(dev.Serial) &&
                _settings.AutoStartLogcat)
            {
                if (adbPath != null) StartLogcat(adbPath, dev.Serial, mappedDeviceId, _settings.LogcatFilter);
            }
        }

        if (newAdbSerials.Count > 0 && adbPath != null && _settings.AutoAdbReverse)
        {
            Task.Run(() =>
            {
                foreach (var serial in newAdbSerials)
                    _adbHelper.ReversePort(adbPath, new AdbDevice { Serial = serial }, _settings.ServerPort);
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
        _networkLogForm.RebuildFilter();
        _normalLogForm.RebuildFilter();
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
        _deviceNormalLogs.Remove(deviceId);
        if (_systemLogForm.IsRuntimeReady()) _systemLogStore.ClearDevice(deviceId);
        _devicePanel.RemoveDevice(deviceId);
        if (_currentDeviceId == deviceId)
        {
            _currentDeviceId = null;
            _selectedLogEntry = null;
            ShowLogDetail(null);
            StopMirror(clearStatusOnly: true);
        }

        _networkLogForm.RebuildFilter();
        _systemLogForm.RefreshSystemLogList();
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
        StopAdbScanLoop();
        StopMirror(clearStatusOnly: true);
        _scrcpyStartCts?.Cancel();
        _scrcpyStartCts?.Dispose();
        _mirrorRestartTimer?.Stop();
        _mirrorRestartTimer?.Dispose();
        foreach (var session in _externalScrcpySessions.ToArray()) session.Dispose();
        _externalScrcpySessions.Clear();
        foreach (var reader in _logcatReaders.Values) reader.Stop();
        _systemLogForm.CancelAsyncOperations();
        if (_systemLogStore is not null) _systemLogStore.Dispose();
        _networkLogForm.Dispose();
        _normalLogForm.Dispose();
        _systemLogForm.Dispose();
        _server.Stop();
        base.OnFormClosing(e);
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

    private void OnMainFormLoad(object? sender, EventArgs e) => RefreshMirrorPanelState();
}
