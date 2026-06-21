using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Models;
using LogViewer.Network;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class MainForm : Form
{
    private readonly LogServer _server = new();
    private readonly AdbHelper _adbHelper = new();
    private readonly Dictionary<string, LogcatReader> _logcatReaders = new();
    private AppSettings _settings;
    private CancellationTokenSource? _adbScanCts;

    private readonly Dictionary<string, RingBuffer<LogEntry>> _deviceLogs = new();
    private readonly RingBuffer<LogEntry> _allLogs;
    private readonly Dictionary<string, string> _adbSerialToDeviceId = new();
    private readonly Queue<SystemLogEntry> _pendingSystemLogs = new();
    private readonly object _pendingSystemLogsLock = new();
    private bool _systemLogFlushScheduled;
    private bool _networkRefreshScheduled;
    private bool _networkRefreshNeedsFullFilter;

    private string? _currentDeviceId;
    private bool _showingSystemLog;
    private List<int> _filteredNetworkIndices = new();
    private bool _networkAutoScrollEnabled = true;
    private bool _systemAutoScrollEnabled = true;

    private System.Windows.Forms.SplitContainer _outerSplit;
    private System.Windows.Forms.SplitContainer _innerSplit;
    private LogViewer.UI.DevicePanel _devicePanel;

    private System.Windows.Forms.TabControl _tabLogType;
    private TabPage _tabNetwork = null!;
    private TabPage _tabSystem = null!;

    private System.Windows.Forms.Panel _pnlNetworkFilter;
    private System.Windows.Forms.TextBox _txtNetworkKeyword;
    private ComboBox _cmbMethod = null!;
    private ComboBox _cmbStatusCode = null!;
    private Button _btnScrollToTop = null!;
    private Button _btnScrollToBottom = null!;
    private System.Windows.Forms.Label _lblLogCount;

    private ListView _lstNetworkLogs = null!;
    private ListView _lstSystemLogs = null!;

    private Panel _pnlSystemFilter = null!;
    private Panel _systemActionBar = null!;
    private TextBox _txtSystemKeyword = null!;
    private ComboBox _cmbLogLevel = null!;
    private ComboBox _cmbLogTag = null!;
    private Button _btnSystemScrollToTop = null!;
    private Button _btnSystemScrollToBottom = null!;
    private Button _btnSystemPauseResume = null!;
    private System.Windows.Forms.Label _lblSystemBacklog = null!;

    private System.Windows.Forms.TabControl _tabDetail;
    private TabPage _tabHeaders = null!;
    private TabPage _tabRequestBody = null!;
    private TabPage _tabResponseBody = null!;

    private LogViewer.UI.JsonTreeView _jsonHeaders;
    private JsonTreeView _jsonRequestBody = null!;
    private JsonTreeView _jsonResponseBody = null!;

    private TextBox _rawHeaders = null!;
    private TextBox _rawRequestBody = null!;
    private TextBox _rawResponseBody = null!;

    private System.Windows.Forms.Panel _pnlJsonToolbar;
    private TextBox _txtJsonSearch = null!;
    private Button _btnJsonSearch = null!;
    private Button _btnExpandAll = null!;
    private Button _btnCollapseAll = null!;
    private Button _btnCollapseTo2 = null!;
    private Button _btnToggleView = null!;
    private bool _detailViewIsRaw;

    private ToolStrip _toolStrip = null!;
    private ToolStripDropDownButton _btnAdbReverse = null!;
    private ToolStripLabel _lblStatus = null!;

    private MenuStrip _menuStrip = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _lblServerStatus = null!;
    private ToolStripStatusLabel _lblDeviceCountStatus = null!;
    private ToolStripStatusLabel _lblAdbStatus = null!;
    private ToolStripStatusLabel _lblLogcatStatus = null!;

    private FlowLayoutPanel _pnlBottomBar = null!;
    private Button _btnClear = null!;
    private Button _btnExportJson = null!;
    private Button _btnExportTxt = null!;

    private LogEntry? _selectedLogEntry;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _allLogs = new RingBuffer<LogEntry>(_settings.MaxLogEntriesAll);
        InitializeComponent();

        if (IsDesignTimeMode())
        {
            return;
        }

        WireComponentEvents();
        ApplySettings();

        if (!string.IsNullOrEmpty(_settings.AdbPath))
        {
            _adbHelper.SetManualPath(_settings.AdbPath);
        }

        if (_adbHelper.IsAdbAvailable())
        {
            _adbHelper.EnsureServerStarted();
        }

        if (!_adbHelper.IsAdbAvailable())
        {
            this.BeginInvoke(new Action(() =>
            {
                var result = MessageBox.Show(
                    "ADB was not found automatically.\n\nWould you like to set the ADB path manually?",
                    "ADB Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    OnSettingsClick(this, EventArgs.Empty);
                }
            }));
        }

        StartAdbScanLoop();
        AutoStartServer();
    }

    private void WireComponentEvents()
    {
        InitializeSystemLogRuntime();
        _settingsMenuItem.Click += OnSettingsClick;
        _server.DeviceConnected += OnDeviceConnected;
        _server.DeviceDisconnected += OnDeviceDisconnected;
        _server.LogReceived += OnLogReceived;
        _btnAdbReverse.DropDownOpening += OnAdbReverseOpening;
        _devicePanel.DeviceSelected += OnDeviceSelected;
        _devicePanel.DeleteDeviceRequested += OnDeleteDevice;
        _devicePanel.AdbReverseRequested += OnAdbReverseForDevice;
        _devicePanel.LogcatToggleRequested += OnLogcatToggle;
        _devicePanel.RefreshAdbRequested += OnRefreshAdbDevices;
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
        Load += (s, e) => _outerSplit.SplitterDistance = (int)(ClientSize.Width * 0.15);
        Shown += async (s, e) => await PrimeAdbDeviceListAsync();
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
        _lblAdbStatus.Text = adbPath != null ? $"ADB: {adbPath}" : "ADB: Not found (Tools→Settings)";
        _lblAdbStatus.ForeColor = adbPath != null ? Color.Green : Color.Red;
    }

    private void ApplySettings()
    {
        var font = new Font("Consolas", _settings.FontSize);
        _lstNetworkLogs.Font = font;
        _lstSystemLogs.Font = font;
        _jsonHeaders.SetFont(font);
        _jsonRequestBody.SetFont(font);
        _jsonResponseBody.SetFont(font);
        if (_cmbMethod.SelectedIndex < 0 && _cmbMethod.Items.Count > 0) _cmbMethod.SelectedIndex = 0;
        if (_cmbStatusCode.SelectedIndex < 0 && _cmbStatusCode.Items.Count > 0) _cmbStatusCode.SelectedIndex = 0;
        if (_cmbLogLevel.SelectedIndex < 0 && _cmbLogLevel.Items.Count > 0) _cmbLogLevel.SelectedIndex = 0;
        if (_cmbLogTag.SelectedIndex < 0 && _cmbLogTag.Items.Count > 0) _cmbLogTag.SelectedIndex = 0;
        UpdateLogCount();
    }

    private JsonTreeView? GetActiveJsonView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _jsonHeaders;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _jsonRequestBody;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _jsonResponseBody;
        return null;
    }

    private TextBox? GetActiveRawView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _rawHeaders;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _rawRequestBody;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _rawResponseBody;
        return null;
    }

    private void OnToggleDetailView(object? sender, EventArgs e)
    {
        _detailViewIsRaw = !_detailViewIsRaw;
        _btnToggleView.Text = _detailViewIsRaw ? "Tree" : "Raw";
        _btnExpandAll.Enabled = !_detailViewIsRaw;
        _btnCollapseAll.Enabled = !_detailViewIsRaw;
        _btnCollapseTo2.Enabled = !_detailViewIsRaw;
        _txtJsonSearch.Enabled = !_detailViewIsRaw;
        _btnJsonSearch.Enabled = !_detailViewIsRaw;
        SyncDetailViewVisibility();
    }

    private void SyncDetailViewVisibility()
    {
        _jsonHeaders.Visible = !_detailViewIsRaw;
        _rawHeaders.Visible = _detailViewIsRaw;
        _jsonRequestBody.Visible = !_detailViewIsRaw;
        _rawRequestBody.Visible = _detailViewIsRaw;
        _jsonResponseBody.Visible = !_detailViewIsRaw;
        _rawResponseBody.Visible = _detailViewIsRaw;
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
            {
                _deviceLogs[id] = new RingBuffer<LogEntry>(_settings.MaxLogEntriesPerDevice);
            }

            TryMatchAdbSerial(info);
            _devicePanel.AddOrUpdateDevice(info, 0);
            UpdateDeviceCountStatus();

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

    private static ListViewItem CreateNetworkLogItem(LogEntry entry)
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

    private void ShowLogDetail(LogEntry? entry)
    {
        if (entry == null)
        {
            _jsonHeaders.DisplayPlainText("");
            _jsonRequestBody.DisplayPlainText("");
            _jsonResponseBody.DisplayPlainText("");
            _rawHeaders.Text = "";
            _rawRequestBody.Text = "";
            _rawResponseBody.Text = "";
            return;
        }

        if (_settings.AutoFormatJson)
        {
            _jsonHeaders.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBody.DisplayJson(entry.Send ?? "");
            _jsonResponseBody.DisplayJson(entry.Content ?? "");
        }
        else
        {
            _jsonHeaders.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBody.DisplayPlainText(entry.Send ?? "");
            _jsonResponseBody.DisplayPlainText(entry.Content ?? "");
        }

        _rawHeaders.Text = entry.Headers ?? "";
        _rawRequestBody.Text = JsonFormatter.FormatJson(entry.Send) ?? entry.Send ?? "";
        _rawResponseBody.Text = JsonFormatter.FormatJson(entry.Content) ?? entry.Content ?? "";
    }

    #endregion

    #region System Log List

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

    private void OnRefreshAdbDevices(object? sender, EventArgs e)
    {
        RequestAdbScan();
    }

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
        }

        RefreshNetworkFilter();
        RefreshSystemLogList();
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

    private void OnNetworkFilterChanged(object? sender, EventArgs e)
    {
        RefreshNetworkFilter();
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
        if (method != "All" && !string.IsNullOrEmpty(method) && !string.Equals(entry.Method, method, StringComparison.OrdinalIgnoreCase))
            return false;

        var statusFilter = _cmbStatusCode.SelectedItem as string;
        if (statusFilter != "All" && !string.IsNullOrEmpty(statusFilter))
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

    #endregion

    #region Scroll & Count

    private static bool IsAtBottom(ListView lv)
    {
        if (lv.VirtualListSize == 0) return true;
        var topIndex = lv.TopItem?.Index ?? 0;
        var visibleCount = Math.Max(1, lv.ClientSize.Height / Math.Max(1, lv.Font.Height + 6));
        return topIndex + visibleCount >= lv.VirtualListSize;
    }

    private static void ScrollToBottom(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(lv.VirtualListSize - 1); } catch { }
        }
    }

    private static void ScrollToTop(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(0); } catch { }
        }
    }

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

    private void UpdateDeviceCountStatus()
    {
        _lblDeviceCountStatus.Text = $"Devices: {_deviceLogs.Count}";
    }

    private void UpdateLogcatStatus()
    {
        var running = _logcatReaders.Values.Count(r => r.IsRunning);
        _lblLogcatStatus.Text = $"Logcat: {running}";
    }

    #endregion

    #region Bottom Bar Actions

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

    #endregion

    #region Settings

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings, _adbHelper);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings = AppSettings.Load();
            ApplySettings();

            if (!string.IsNullOrEmpty(_settings.AdbPath))
            {
                _adbHelper.SetManualPath(_settings.AdbPath);
            }

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
        }
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopAdbScanLoop();
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
}
