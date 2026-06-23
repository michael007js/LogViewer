using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Static;

namespace LogViewer.UI;

public sealed class DevicePanel : UserControl
{
    private ComboBox _cmbDevices = null!;
    private Button _btnRefreshAdb = null!;
    private Panel _mirrorHostPanel = null!;
    private Panel _mirrorViewportPanel = null!;
    private Label _lblMirrorPlaceholder = null!;
    private Label _lblMirrorStatus = null!;
    private Button _btnMirrorToggle = null!;
    private Button _btnMirrorReconnect = null!;
    private Button _btnMirrorRotate = null!;
    private Button _btnMirrorScreenshot = null!;
    private Button _btnMirrorPopout = null!;
    private readonly Dictionary<string, DeviceRecord> _devices = new();
    private string? _selectedDeviceId;
    private bool _mirrorRunning;
    private bool _mirrorReady;
    private bool _mirrorHostVisible;
    private double _mirrorAspectRatio = 9d / 16d;
    private Rectangle _mirrorDisplayBounds = Rectangle.Empty;
    private string _mirrorStatusText = Language.DeviceSelectPrompt;

    public event EventHandler<string?>? DeviceSelected;
    public event EventHandler? RefreshAdbRequested;
    public event EventHandler<string>? MirrorStartRequested;
    public event EventHandler<string>? MirrorStopRequested;
    public event EventHandler<string>? MirrorReconnectRequested;
    public event EventHandler<string>? MirrorRotateRequested;
    public event EventHandler<string>? MirrorScreenshotRequested;
    public event EventHandler<string>? MirrorPopoutRequested;
    public event EventHandler? MirrorLayoutChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedDeviceId => _selectedDeviceId;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IntPtr MirrorHostHandle
    {
        get
        {
            if (_mirrorHostPanel?.IsHandleCreated == true)
            {
                return _mirrorHostPanel.Handle;
            }
            return IntPtr.Zero;
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle MirrorDisplayBounds => _mirrorDisplayBounds;

    public DevicePanel()
    {
        if (IsDesignTimeMode())
        {
            InitializeDesignTimePlaceholder();
            BackColor = SystemColors.Control;
            return;
        }

        InitializeComponents();
    }

    private void InitializeDesignTimePlaceholder()
    {
        if (Controls.Count > 0)
        {
            return;
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

        var selector = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Consolas", 9f)
        };
        selector.Items.Add($"● {Language.All}");
        selector.SelectedIndex = 0;

        var mirror = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.Black
        };
        mirror.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Language.ScrcpyHost,
            ForeColor = Color.WhiteSmoke,
            TextAlign = ContentAlignment.MiddleCenter
        });

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill
        };
        controls.Controls.Add(new Button { Text = Language.Start, AutoSize = true });
        controls.Controls.Add(new Button { Text = Language.Rotate, AutoSize = true });
        controls.Controls.Add(new Button { Text = Language.Screenshot, AutoSize = true });

        layout.Controls.Add(selector, 0, 0);
        layout.Controls.Add(mirror, 0, 1);
        layout.Controls.Add(controls, 0, 2);
        Controls.Add(layout);
    }

    private void InitializeComponents()
    {
        if (_cmbDevices != null)
        {
            return;
        }

        BackColor = SystemColors.Control;
        Padding = new Padding(8);

        _cmbDevices = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Consolas", 9f),
            Height = 28
        };
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;

        _btnRefreshAdb = new Button
        {
            Text = Language.ScanAdb,
            Dock = DockStyle.Top,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8f)
        };
        _btnRefreshAdb.Click += (_, _) => RefreshAdbRequested?.Invoke(this, EventArgs.Empty);

        _mirrorHostPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 8, 0, 8)
        };
        _mirrorHostPanel.Resize += (_, _) => UpdateMirrorHostBounds();
        _mirrorHostPanel.SizeChanged += (_, _) => UpdateMirrorHostBounds();
        _mirrorHostPanel.VisibleChanged += (_, _) => UpdateMirrorHostBounds();
        _mirrorHostPanel.Layout += (_, _) => UpdateMirrorHostBounds();

        _mirrorViewportPanel = new Panel
        {
            BackColor = Color.Black,
            Visible = true
        };
        _mirrorViewportPanel.Visible = false;
        _mirrorHostPanel.Controls.Add(_mirrorViewportPanel);

        _lblMirrorPlaceholder = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = _mirrorStatusText
        };
        _mirrorHostPanel.Controls.Add(_lblMirrorPlaceholder);

        _lblMirrorStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 2, 0),
            Text = _mirrorStatusText
        };

        _btnMirrorToggle = CreateControlButton(Language.Start);
        _btnMirrorToggle.Click += (_, _) =>
        {
            if (TryGetSelectedDeviceForAction(out var deviceId))
            {
                if (_mirrorRunning)
                {
                    MirrorStopRequested?.Invoke(this, deviceId);
                }
                else
                {
                    MirrorStartRequested?.Invoke(this, deviceId);
                }
            }
        };

        _btnMirrorReconnect = CreateControlButton(Language.Reconnect);
        _btnMirrorReconnect.Click += (_, _) =>
        {
            if (TryGetSelectedDeviceForAction(out var deviceId))
            {
                MirrorReconnectRequested?.Invoke(this, deviceId);
            }
        };

        _btnMirrorRotate = CreateControlButton(Language.Rotate);
        _btnMirrorRotate.Click += (_, _) =>
        {
            if (TryGetSelectedDeviceForAction(out var deviceId))
            {
                MirrorRotateRequested?.Invoke(this, deviceId);
            }
        };

        _btnMirrorScreenshot = CreateControlButton(Language.Screenshot);
        _btnMirrorScreenshot.Click += (_, _) =>
        {
            if (TryGetSelectedDeviceForAction(out var deviceId))
            {
                MirrorScreenshotRequested?.Invoke(this, deviceId);
            }
        };

        _btnMirrorPopout = CreateControlButton(Language.Popout);
        _btnMirrorPopout.Click += (_, _) =>
        {
            if (TryGetSelectedDeviceForAction(out var deviceId))
            {
                MirrorPopoutRequested?.Invoke(this, deviceId);
            }
        };

        var buttonBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            RowCount = 3,
            Height = 102,
            Margin = new Padding(0)
        };
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        buttonBar.Controls.Add(_btnMirrorToggle, 0, 0);
        buttonBar.Controls.Add(_btnMirrorReconnect, 1, 0);
        buttonBar.Controls.Add(_btnMirrorRotate, 0, 1);
        buttonBar.Controls.Add(_btnMirrorScreenshot, 1, 1);
        buttonBar.Controls.Add(_btnMirrorPopout, 0, 2);
        buttonBar.SetColumnSpan(_btnMirrorPopout, 2);

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));

        container.Controls.Add(_cmbDevices, 0, 0);
        container.Controls.Add(_btnRefreshAdb, 0, 1);
        container.Controls.Add(_mirrorHostPanel, 0, 2);
        container.Controls.Add(_lblMirrorStatus, 0, 3);
        container.Controls.Add(buttonBar, 0, 4);

        Controls.Add(container);
        RefreshList();
        UpdateMirrorUiState();
        Resize += (_, _) => UpdateMirrorHostBounds();
        Layout += (_, _) => UpdateMirrorHostBounds();
    }

    public void AddOrUpdateDevice(DeviceInfo info, int logCount)
    {
        var id = info.DeviceId ?? string.Empty;
        if (!_devices.ContainsKey(id))
        {
            _devices[id] = new DeviceRecord { Info = info, LogCount = logCount };
        }
        else
        {
            var record = _devices[id];
            if (string.IsNullOrEmpty(info.AdbSerial))
            {
                info.AdbSerial = record.Info.AdbSerial;
            }

            record.Info = info;
            record.LogCount = logCount;
            if (info.IsConnected)
            {
                record.IsAdbOnly = false;
            }
        }

        RefreshList();
    }

    public bool AddAdbDevice(string serial, string model)
    {
        if (_devices.TryGetValue(serial, out var existing) && existing.IsAdbOnly)
        {
            existing.Info.DeviceModel = model;
            RefreshList();
            return false;
        }

        if (_devices.ContainsKey(serial))
        {
            return false;
        }

        var info = new DeviceInfo
        {
            DeviceId = serial,
            DeviceModel = model,
            AdbSerial = serial,
            IsConnected = false
        };

        _devices[serial] = new DeviceRecord { Info = info, LogCount = 0, IsAdbOnly = true };
        RefreshList();
        return true;
    }

    public void RemoveMissingAdbDevices(HashSet<string> currentAdbSerials)
    {
        var toRemove = _devices.Where(kvp => kvp.Value.IsAdbOnly && !currentAdbSerials.Contains(kvp.Key)).ToList();
        foreach (var kvp in toRemove)
        {
            _devices.Remove(kvp.Key);
            if (_selectedDeviceId == kvp.Key)
            {
                _selectedDeviceId = null;
            }
        }

        if (toRemove.Count > 0)
        {
            RefreshList();
        }
    }

    public void MergeTcpDevice(string adbSerial, DeviceInfo tcpInfo, int logCount)
    {
        if (_devices.TryGetValue(adbSerial, out var existing) && existing.IsAdbOnly)
        {
            existing.Info = tcpInfo;
            existing.LogCount = logCount;
            existing.IsAdbOnly = false;
            _devices.Remove(adbSerial);
            _devices[tcpInfo.DeviceId ?? string.Empty] = existing;
            RefreshList();
        }
    }

    public void UpdateLogCount(string deviceId, int count)
    {
        if (_devices.TryGetValue(deviceId, out var record))
        {
            record.LogCount = count;
            RefreshList();
        }
    }

    public void SetDeviceConnected(string deviceId, bool connected)
    {
        if (_devices.TryGetValue(deviceId, out var record))
        {
            record.Info.IsConnected = connected;
            if (connected)
            {
                record.IsAdbOnly = false;
            }

            RefreshList();
        }
    }

    public void RemoveDevice(string deviceId)
    {
        _devices.Remove(deviceId);
        if (_selectedDeviceId == deviceId)
        {
            _selectedDeviceId = null;
        }

        RefreshList();
    }

    public string? GetAdbSerialForKey(string deviceId)
    {
        return _devices.TryGetValue(deviceId, out var record) ? record.Info.AdbSerial : null;
    }

    public void SetMirrorStatus(string text, bool hostVisible, bool isRunning, bool isReady)
    {
        _mirrorStatusText = text;
        _mirrorHostVisible = hostVisible;
        _mirrorRunning = isRunning;
        _mirrorReady = isReady;
        UpdateMirrorUiState();
    }

    public void ClearMirrorHost()
    {
        _mirrorHostVisible = false;
        _mirrorReady = false;
        UpdateMirrorUiState();
    }

    public void SetMirrorAspectRatio(double aspectRatio)
    {
        if (double.IsNaN(aspectRatio) || double.IsInfinity(aspectRatio) || aspectRatio <= 0)
        {
            aspectRatio = 9d / 16d;
        }

        _mirrorAspectRatio = aspectRatio;
        UpdateMirrorHostBounds();
    }

    public void SyncMirrorBounds()
    {
        UpdateMirrorHostBounds();
    }

    public Rectangle GetMirrorDisplayBounds()
    {
        return _mirrorDisplayBounds;
    }

    public IntPtr EnsureMirrorHostHandle()
    {
        InitializeComponents();
        UpdateMirrorHostBounds();

        if (_mirrorHostPanel?.IsHandleCreated != true)
        {
            _mirrorHostPanel?.CreateControl();
            _ = _mirrorHostPanel?.Handle;
        }

        if (_mirrorViewportPanel?.IsHandleCreated != true)
        {
            _mirrorViewportPanel.Visible = true;
            _mirrorViewportPanel?.CreateControl();
            _ = _mirrorViewportPanel?.Handle;
        }

        return MirrorHostHandle;
    }

    private Button CreateControlButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 28,
            Margin = new Padding(0, 0, 6, 6),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8f)
        };
    }

    private bool TryGetSelectedDeviceForAction(out string deviceId)
    {
        deviceId = _selectedDeviceId ?? string.Empty;
        return !string.IsNullOrEmpty(deviceId);
    }

    private void RefreshList()
    {
        InitializeComponents();

        _cmbDevices.BeginUpdate();
        _cmbDevices.SelectedIndexChanged -= OnDeviceSelected;
        _cmbDevices.Items.Clear();
        _cmbDevices.Items.Add(new DeviceSelectorItem(null, $"● {Language.All}"));

        foreach (var kvp in _devices.OrderBy(static pair => pair.Value.Info.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _cmbDevices.Items.Add(new DeviceSelectorItem(kvp.Key, BuildDeviceDisplayText(kvp.Value)));
        }

        if (_selectedDeviceId != null && !_devices.ContainsKey(_selectedDeviceId))
        {
            _selectedDeviceId = null;
        }

        SyncSelectedItem();
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;
        _cmbDevices.EndUpdate();
        UpdateMirrorUiState();
    }

    private string BuildDeviceDisplayText(DeviceRecord record)
    {
        var status = record.IsAdbOnly || !record.Info.IsConnected ? "○" : "●";
        var qa = record.Info.IsQa ? " [QA]" : string.Empty;
        var adb = record.Info.AdbSerial != null ? " [ADB]" : string.Empty;
        return $"{status} {record.Info.DisplayName}{qa}{adb} ({record.LogCount})";
    }

    private void OnDeviceSelected(object? sender, EventArgs e)
    {
        if (_cmbDevices.SelectedItem is not DeviceSelectorItem item)
        {
            return;
        }

        if (string.Equals(_selectedDeviceId ?? string.Empty, item.DeviceId ?? string.Empty, StringComparison.Ordinal))
        {
            UpdateMirrorUiState();
            return;
        }

        _selectedDeviceId = item.DeviceId;
        SyncSelectedItem();
        UpdateMirrorUiState();
        DeviceSelected?.Invoke(this, item.DeviceId);
    }

    private void SyncSelectedItem()
    {
        var selected = _cmbDevices.Items
            .OfType<DeviceSelectorItem>()
            .FirstOrDefault(item => string.Equals(item.DeviceId ?? string.Empty, _selectedDeviceId ?? string.Empty, StringComparison.Ordinal))
            ?? _cmbDevices.Items.OfType<DeviceSelectorItem>().FirstOrDefault(item => item.DeviceId == null);

        if (selected != null)
        {
            _cmbDevices.SelectedItem = selected;
        }
    }

    private void UpdateMirrorUiState()
    {
        if (_lblMirrorPlaceholder == null)
        {
            return;
        }

        var hasSpecificDevice = !string.IsNullOrEmpty(_selectedDeviceId);
        DeviceRecord? selectedRecord = null;
        var hasSelectedRecord = hasSpecificDevice && _devices.TryGetValue(_selectedDeviceId!, out selectedRecord);
        var hasAdb = hasSelectedRecord && !string.IsNullOrEmpty(selectedRecord?.Info.AdbSerial);

        _lblMirrorPlaceholder.Text = _mirrorStatusText;
        _lblMirrorPlaceholder.Visible = !_mirrorHostVisible;
        _lblMirrorStatus.Text = _mirrorStatusText;
        if (!_mirrorHostVisible)
        {
            _lblMirrorPlaceholder.BringToFront();
        }

        _btnMirrorToggle.Enabled = hasAdb;
        _btnMirrorReconnect.Enabled = hasAdb && _mirrorRunning;
        _btnMirrorRotate.Enabled = hasAdb && _mirrorRunning;
        _btnMirrorScreenshot.Enabled = hasAdb;
        _btnMirrorPopout.Enabled = hasAdb;
        _btnMirrorToggle.Text = _mirrorRunning ? Language.Stop : Language.Start;
    }

    private void UpdateMirrorHostBounds()
    {
        if (_mirrorHostPanel == null)
        {
            return;
        }

        var hostWidth = _mirrorHostPanel.ClientSize.Width;
        var hostHeight = _mirrorHostPanel.ClientSize.Height;
        if (hostWidth <= 0 || hostHeight <= 0)
        {
            if (_mirrorDisplayBounds != Rectangle.Empty)
            {
                _mirrorDisplayBounds = Rectangle.Empty;
                MirrorLayoutChanged?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        var targetWidth = hostWidth;
        var targetHeight = (int)Math.Round(targetWidth / _mirrorAspectRatio);
        if (targetHeight > hostHeight)
        {
            targetHeight = hostHeight;
            targetWidth = (int)Math.Round(targetHeight * _mirrorAspectRatio);
        }

        targetWidth = Math.Clamp(targetWidth, 1, hostWidth);
        targetHeight = Math.Clamp(targetHeight, 1, hostHeight);

        var offsetX = (hostWidth - targetWidth) / 2;
        var offsetY = (hostHeight - targetHeight) / 2;
        var nextBounds = new Rectangle(offsetX, offsetY, targetWidth, targetHeight);
        if (_mirrorViewportPanel.Bounds != nextBounds)
        {
            _mirrorViewportPanel.Bounds = nextBounds;
        }

        if (_mirrorDisplayBounds != nextBounds)
        {
            _mirrorDisplayBounds = nextBounds;
            MirrorLayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        _mirrorHostPanel.Invalidate();
    }

    private sealed class DeviceRecord
    {
        public DeviceInfo Info { get; set; } = new();
        public int LogCount { get; set; }
        public bool IsAdbOnly { get; set; }
    }

    private sealed class DeviceSelectorItem
    {
        public DeviceSelectorItem(string? deviceId, string text)
        {
            DeviceId = deviceId;
            Text = text;
        }

        public string? DeviceId { get; }
        public string Text { get; }

        public override string ToString()
        {
            return Text;
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
}
