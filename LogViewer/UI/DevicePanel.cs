using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Utils;

namespace LogViewer.UI;

public class DevicePanel : UserControl
{
    private ComboBox _cmbDevices = null!;
    private Button _btnDeviceActions = null!;
    private Button _btnRefreshAdb = null!;
    private Panel _workspacePanel = null!;
    private ContextMenuStrip _deviceMenu = null!;
    private Dictionary<string, DeviceRecord> _devices = new();
    private string? _selectedDeviceId;

    public event EventHandler<string?>? DeviceSelected;
    public event EventHandler<string>? DeleteDeviceRequested;
    public event EventHandler<string>? AdbReverseRequested;
    public event EventHandler<string>? LogcatToggleRequested;
    public event EventHandler? RefreshAdbRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedDeviceId => _selectedDeviceId;

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

        var selector = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 30,
            Font = new Font("Consolas", 9f)
        };
        selector.Items.Add("● All");
        selector.SelectedIndex = 0;

        var refresh = new Button
        {
            Text = "\u21BB Scan ADB",
            Dock = DockStyle.Top,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8f)
        };

        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control
        };

        Controls.Add(workspace);
        Controls.Add(refresh);
        Controls.Add(selector);
    }

    private void InitializeComponents()
    {
        if (_cmbDevices != null)
        {
            return;
        }

        _cmbDevices = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 30,
            Font = new Font("Consolas", 9f)
        };
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;

        _btnDeviceActions = new Button
        {
            Text = "...",
            Dock = DockStyle.Right,
            Width = 32,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9f, FontStyle.Bold)
        };
        _btnDeviceActions.Click += (s, e) => ShowDeviceMenu();

        _btnRefreshAdb = new Button
        {
            Text = "\u21BB Scan ADB",
            Dock = DockStyle.Top,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 8f)
        };
        _btnRefreshAdb.Click += (s, e) => RefreshAdbRequested?.Invoke(this, EventArgs.Empty);

        _deviceMenu = new ContextMenuStrip();
        _deviceMenu.Items.Add("Delete Logs", null, (s, ev) =>
        {
            if (_selectedDeviceId != null && _devices.TryGetValue(_selectedDeviceId, out var r) && !r.IsAdbOnly)
                DeleteDeviceRequested?.Invoke(this, _selectedDeviceId);
        });
        _deviceMenu.Items.Add("ADB Reverse", null, (s, ev) =>
        {
            if (_selectedDeviceId != null)
                AdbReverseRequested?.Invoke(this, _selectedDeviceId);
        });
        _deviceMenu.Items.Add("Toggle Logcat", null, (s, ev) =>
        {
            if (_selectedDeviceId != null && (_devices.TryGetValue(_selectedDeviceId, out var r) && (!r.IsAdbOnly || r.Info.AdbSerial != null)))
                LogcatToggleRequested?.Invoke(this, _selectedDeviceId);
        });

        _workspacePanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        var selectorPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30
        };
        selectorPanel.Controls.Add(_cmbDevices);
        selectorPanel.Controls.Add(_btnDeviceActions);

        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(_workspacePanel);
        panel.Controls.Add(_btnRefreshAdb);
        panel.Controls.Add(selectorPanel);

        Controls.Add(panel);
        RefreshList();
    }

    public void AddOrUpdateDevice(DeviceInfo info, int logCount)
    {
        var id = info.DeviceId ?? "";
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
        if (!_devices.ContainsKey(serial))
        {
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
        return false;
    }

    public void RemoveMissingAdbDevices(HashSet<string> currentAdbSerials)
    {
        var toRemove = _devices.Where(kvp => kvp.Value.IsAdbOnly && !currentAdbSerials.Contains(kvp.Key)).ToList();
        foreach (var kvp in toRemove)
        {
            _devices.Remove(kvp.Key);
            if (_selectedDeviceId == kvp.Key) _selectedDeviceId = null;
        }
        if (toRemove.Count > 0) RefreshList();
    }

    public void MergeTcpDevice(string adbSerial, DeviceInfo tcpInfo, int logCount)
    {
        if (_devices.TryGetValue(adbSerial, out var existing) && existing.IsAdbOnly)
        {
            existing.Info = tcpInfo;
            existing.LogCount = logCount;
            existing.IsAdbOnly = false;
            _devices.Remove(adbSerial);
            _devices[tcpInfo.DeviceId ?? ""] = existing;
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
            if (connected) record.IsAdbOnly = false;
            RefreshList();
        }
    }

    public void RemoveDevice(string deviceId)
    {
        _devices.Remove(deviceId);
        if (_selectedDeviceId == deviceId) _selectedDeviceId = null;
        RefreshList();
    }

    public void ClearAll()
    {
        _devices.Clear();
        _selectedDeviceId = null;
        RefreshList();
    }

    public string? GetAdbSerialForKey(string deviceId)
    {
        if (_devices.TryGetValue(deviceId, out var record))
            return record.Info.AdbSerial;
        return null;
    }

    private void SelectDevice(string? deviceId)
    {
        _selectedDeviceId = deviceId;
        SyncSelectedItem();
        UpdateDeviceMenuState();
        DeviceSelected?.Invoke(this, deviceId);
    }

    private void RefreshList()
    {
        InitializeComponents();
        _cmbDevices.BeginUpdate();
        _cmbDevices.SelectedIndexChanged -= OnDeviceSelected;
        _cmbDevices.Items.Clear();
        _cmbDevices.Items.Add(new DeviceSelectorItem(null, "\u25CF All"));

        foreach (var kvp in _devices)
        {
            var id = kvp.Key;
            var record = kvp.Value;
            _cmbDevices.Items.Add(new DeviceSelectorItem(id, BuildDeviceDisplayText(record)));
        }

        if (_selectedDeviceId != null && !_devices.ContainsKey(_selectedDeviceId))
        {
            _selectedDeviceId = null;
        }

        SyncSelectedItem();
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;
        _cmbDevices.EndUpdate();
        UpdateDeviceMenuState();
    }

    private string BuildDeviceDisplayText(DeviceRecord record)
    {
        var status = record.IsAdbOnly || !record.Info.IsConnected ? "\u25CB" : "\u25CF";
        var qa = record.Info.IsQa ? " [QA]" : "";
        var adb = record.IsAdbOnly ? " [ADB]" : "";
        return $"{status} {record.Info.DisplayName}{qa}{adb} ({record.LogCount} logs)";
    }

    private void OnDeviceSelected(object? sender, EventArgs e)
    {
        if (_cmbDevices.SelectedItem is DeviceSelectorItem item)
        {
            if (string.Equals(_selectedDeviceId ?? string.Empty, item.DeviceId ?? string.Empty, StringComparison.Ordinal))
            {
                UpdateDeviceMenuState();
                return;
            }

            SelectDevice(item.DeviceId);
        }
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

    private void UpdateDeviceMenuState()
    {
        DeviceRecord? record = null;
        var hasDevice = _selectedDeviceId != null && _devices.TryGetValue(_selectedDeviceId, out record);
        _btnDeviceActions.Enabled = hasDevice;
        if (_deviceMenu.Items.Count < 3)
        {
            return;
        }

        _deviceMenu.Items[0].Enabled = hasDevice && record != null && !record.IsAdbOnly;
        _deviceMenu.Items[1].Enabled = hasDevice;
        _deviceMenu.Items[2].Enabled = hasDevice && record != null && (!record.IsAdbOnly || record.Info.AdbSerial != null);
    }

    private void ShowDeviceMenu()
    {
        UpdateDeviceMenuState();
        if (_btnDeviceActions.Enabled)
        {
            _deviceMenu.Show(_btnDeviceActions, new Point(0, _btnDeviceActions.Height));
        }
    }

    private class DeviceRecord
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
