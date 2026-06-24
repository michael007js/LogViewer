using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// 左侧设备控制面板，包含 ADB 设备下拉列表、scrcpy 投屏宿主区域和控制按钮栏。
/// 管理 ADB 设备扫描、scrcpy 生命周期、设备选择和投屏显示布局。
/// </summary>
public sealed partial class DevicePanel : UserControl
{
    /// <summary>是否处于设计器模式。</summary>
    private readonly bool _isDesignMode;
    /// <summary>设备ID到设备记录的映射表。</summary>
    private readonly Dictionary<string, DeviceRecord> _devices = new();
    /// <summary>当前选中的设备ID。</summary>
    private string? _selectedDeviceId;
    /// <summary>scrcpy 投屏是否正在运行。</summary>
    private bool _mirrorRunning;
    /// <summary>scrcpy 投屏是否就绪（窗口已创建）。</summary>
    private bool _mirrorReady;
    /// <summary>投屏宿主是否可见（替代占位符）。</summary>
    private bool _mirrorHostVisible;
    /// <summary>投屏宽高比（宽/高），默认竖屏 9:16。</summary>
    private double _mirrorAspectRatio = 9d / 16d;
    /// <summary>投屏视口在宿主面板内的显示区域矩形。</summary>
    private Rectangle _mirrorDisplayBounds = Rectangle.Empty;
    /// <summary>投屏状态提示文本。</summary>
    private string _mirrorStatusText = Language.DeviceSelectPrompt;

    /// <summary>设备选中事件，参数为选中的设备ID（null表示"全部"）。</summary>
    public event EventHandler<string?>? DeviceSelected;
    /// <summary>ADB 扫描刷新请求事件。</summary>
    public event EventHandler? RefreshAdbRequested;
    /// <summary>投屏启动请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorStartRequested;
    /// <summary>投屏停止请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorStopRequested;
    /// <summary>投屏重连请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorReconnectRequested;
    /// <summary>投屏旋转请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorRotateRequested;
    /// <summary>投屏截图请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorScreenshotRequested;
    /// <summary>投屏弹出窗口请求事件，参数为设备ID。</summary>
    public event EventHandler<string>? MirrorPopoutRequested;
    /// <summary>投屏布局变更事件（视口位置/尺寸发生变化）。</summary>
    public event EventHandler? MirrorLayoutChanged;

    /// <summary>获取当前选中的设备ID，null 表示"全部"。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedDeviceId => _selectedDeviceId;

    /// <summary>
    /// 获取 scrcpy 投屏宿主面板的窗口句柄，用于嵌入 scrcpy 窗口。设计器模式返回零。
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IntPtr MirrorHostHandle
    {
        get
        {
            if (_isDesignMode)
            {
                return IntPtr.Zero;
            }

            if (_mirrorHostPanel?.IsHandleCreated == true)
            {
                return _mirrorHostPanel.Handle;
            }

            return IntPtr.Zero;
        }
    }

    /// <summary>获取投屏视口在宿主面板内的显示区域矩形。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle MirrorDisplayBounds => _mirrorDisplayBounds;

    /// <summary>
    /// 初始化 DevicePanel，创建子控件并根据是否设计器模式应用预览或运行时布局。
    /// </summary>
    public DevicePanel()
    {
        _isDesignMode = IsDesignTimeMode();
        InitializeComponent();
        WireRuntimeEvents();
        if (_isDesignMode)
        {
            ApplyDesignTimePreview();
        }
        else
        {
            RefreshList();
            UpdateMirrorUiState();
        }
    }

    /// <summary>
    /// 绑定所有运行时事件处理器。放在此处而非 Designer.cs，避免设计器重新生成时丢失事件绑定。
    /// </summary>
    private void WireRuntimeEvents()
    {
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;
        _btnRefreshAdb.Click += OnRefreshAdbClick;
        _mirrorHostPanel.Resize += OnMirrorHostResized;
        _mirrorHostPanel.SizeChanged += OnMirrorHostResized;
        _mirrorHostPanel.VisibleChanged += OnMirrorHostResized;
        _mirrorHostPanel.Layout += OnMirrorHostLayoutChanged;
        _mirrorHostPanel.Paint += OnMirrorHostPaint;
        button1.Click += OnMirrorToggleClick;
        button2.Click += OnMirrorReconnectClick;
        _btnMirrorToggle.Click += OnMirrorToggleClick;
        _btnMirrorReconnect.Click += OnMirrorReconnectClick;
        _btnMirrorRotate.Click += OnMirrorRotateClick;
        _btnMirrorScreenshot.Click += OnMirrorScreenshotClick;
        _btnMirrorPopout.Click += OnMirrorPopoutClick;
        Resize += OnPanelResized;
        Layout += OnPanelLayoutChanged;
    }

    /// <summary>ADB 扫描刷新按钮点击事件处理器。</summary>
    private void OnRefreshAdbClick(object? sender, EventArgs e)
    {
        RefreshAdbRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>投屏宿主面板尺寸变化事件处理器。</summary>
    private void OnMirrorHostResized(object? sender, EventArgs e)
    {
        UpdateMirrorHostBounds();
    }

    /// <summary>投屏宿主面板 Paint 事件处理器，在投屏未显示时绘制占位文本。</summary>
    private void OnMirrorHostPaint(object? sender, PaintEventArgs e)
    {
        if (_mirrorHostVisible)
        {
            return;
        }

        using var brush = new SolidBrush(Color.Gainsboro);
        var textSize = e.Graphics.MeasureString(_mirrorStatusText, Font);
        var x = (_mirrorHostPanel.ClientSize.Width - textSize.Width) / 2;
        var y = (_mirrorHostPanel.ClientSize.Height - textSize.Height) / 2;
        e.Graphics.DrawString(_mirrorStatusText, Font, brush, x, y);
    }

    /// <summary>投屏宿主面板布局变化事件处理器。</summary>
    private void OnMirrorHostLayoutChanged(object? sender, LayoutEventArgs e)
    {
        UpdateMirrorHostBounds();
    }

    /// <summary>投屏启动/停止按钮点击事件处理器。</summary>
    private void OnMirrorToggleClick(object? sender, EventArgs e)
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
    }

    /// <summary>投屏重连按钮点击事件处理器。</summary>
    private void OnMirrorReconnectClick(object? sender, EventArgs e)
    {
        if (TryGetSelectedDeviceForAction(out var deviceId))
        {
            MirrorReconnectRequested?.Invoke(this, deviceId);
        }
    }

    /// <summary>投屏旋转按钮点击事件处理器。</summary>
    private void OnMirrorRotateClick(object? sender, EventArgs e)
    {
        if (TryGetSelectedDeviceForAction(out var deviceId))
        {
            MirrorRotateRequested?.Invoke(this, deviceId);
        }
    }

    /// <summary>投屏截图按钮点击事件处理器。</summary>
    private void OnMirrorScreenshotClick(object? sender, EventArgs e)
    {
        if (TryGetSelectedDeviceForAction(out var deviceId))
        {
            MirrorScreenshotRequested?.Invoke(this, deviceId);
        }
    }

    /// <summary>投屏弹出窗口按钮点击事件处理器。</summary>
    private void OnMirrorPopoutClick(object? sender, EventArgs e)
    {
        if (TryGetSelectedDeviceForAction(out var deviceId))
        {
            MirrorPopoutRequested?.Invoke(this, deviceId);
        }
    }

    /// <summary>面板自身尺寸变化事件处理器。</summary>
    private void OnPanelResized(object? sender, EventArgs e)
    {
        UpdateMirrorHostBounds();
    }

    /// <summary>面板自身布局变化事件处理器。</summary>
    private void OnPanelLayoutChanged(object? sender, LayoutEventArgs e)
    {
        UpdateMirrorHostBounds();
    }

    /// <summary>
    /// 在设计器模式下填充示例设备数据并禁用所有交互按钮。
    /// </summary>
    private void ApplyDesignTimePreview()
    {
        _cmbDevices.SelectedIndexChanged -= OnDeviceSelected;
        _cmbDevices.BeginUpdate();
        _cmbDevices.Items.Clear();
        _cmbDevices.Items.Add(new DeviceSelectorItem(null, Language.All));
        _cmbDevices.Items.Add(new DeviceSelectorItem("demo", "Pixel 8 Pro [ADB] (12)"));
        _cmbDevices.SelectedIndex = 1;
        _cmbDevices.EndUpdate();
        _cmbDevices.SelectedIndexChanged += OnDeviceSelected;

        _selectedDeviceId = "demo";
        _mirrorStatusText = Language.ScrcpyHost;
        _mirrorHostVisible = false;
        _mirrorRunning = false;
        _mirrorReady = true;
        _btnRefreshAdb.Enabled = false;
        _btnMirrorToggle.Enabled = false;
        _btnMirrorReconnect.Enabled = false;
        button1.Enabled = false;
        button2.Enabled = false;
        _btnMirrorRotate.Enabled = false;
        _btnMirrorScreenshot.Enabled = false;
        _btnMirrorPopout.Enabled = false;
        UpdateMirrorUiState();
    }

    /// <summary>
    /// 添加或更新 TCP 连接设备。若设备已存在则更新信息和日志计数；
    /// 若设备已连接则标记为非 ADB-only。
    /// </summary>
    /// <param name="info">设备注册信息。</param>
    /// <param name="logCount">该设备的日志条数。</param>
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

    /// <summary>
    /// 添加 ADB-only 设备（纯 ADB 扫描发现的设备，无 TCP 连接）。
    /// 若设备已存在且是 ADB-only，则更新型号并返回 false；若已存在非 ADB-only，返回 false。
    /// </summary>
    /// <param name="serial">ADB 序列号。</param>
    /// <param name="model">设备型号名称。</param>
    /// <returns>是否为新添加的设备。</returns>
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

    /// <summary>
    /// 移除不在当前 ADB 扫描结果中的 ADB-only 设备。若选中设备被移除则清空选中状态。
    /// </summary>
    /// <param name="currentAdbSerials">当前 ADB 扫描发现的序列号集合。</param>
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

    /// <summary>
    /// 将 TCP 连接设备与 ADB-only 设备合并，用 TCP 信息替换 ADB-only 记录并迁移键名。
    /// </summary>
    /// <param name="adbSerial">ADB 序列号（旧键名）。</param>
    /// <param name="tcpInfo">TCP 连接的设备信息。</param>
    /// <param name="logCount">日志条数。</param>
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

    /// <summary>
    /// 更新指定设备的日志计数并刷新下拉列表显示。
    /// </summary>
    /// <param name="deviceId">设备ID。</param>
    /// <param name="count">新的日志条数。</param>
    public void UpdateLogCount(string deviceId, int count)
    {
        if (_devices.TryGetValue(deviceId, out var record))
        {
            record.LogCount = count;
            RefreshList();
        }
    }

    /// <summary>
    /// 设置指定设备的连接状态，连接成功时标记为非 ADB-only。
    /// </summary>
    /// <param name="deviceId">设备ID。</param>
    /// <param name="connected">是否已连接。</param>
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

    /// <summary>
    /// 移除指定设备。若该设备是当前选中设备则清空选中状态。
    /// </summary>
    /// <param name="deviceId">要移除的设备ID。</param>
    public void RemoveDevice(string deviceId)
    {
        _devices.Remove(deviceId);
        if (_selectedDeviceId == deviceId)
        {
            _selectedDeviceId = null;
        }

        RefreshList();
    }

    /// <summary>
    /// 获取指定设备ID对应的 ADB 序列号，用于 scrcpy 启动。
    /// </summary>
    /// <param name="deviceId">设备ID。</param>
    /// <returns>ADB 序列号，设备不存在时返回 null。</returns>
    public string? GetAdbSerialForKey(string deviceId)
    {
        return _devices.TryGetValue(deviceId, out var record) ? record.Info.AdbSerial : null;
    }

    /// <summary>
    /// 设置投屏状态参数并更新 UI 显示。
    /// </summary>
    /// <param name="text">状态提示文本。</param>
    /// <param name="hostVisible">投屏宿主是否可见。</param>
    /// <param name="isRunning">scrcpy 是否正在运行。</param>
    /// <param name="isReady">投屏窗口是否就绪。</param>
    public void SetMirrorStatus(string text, bool hostVisible, bool isRunning, bool isReady)
    {
        _mirrorStatusText = text;
        _mirrorHostVisible = hostVisible;
        _mirrorRunning = isRunning;
        _mirrorReady = isReady;
        _mirrorHostPanel.MirrorActive = hostVisible;
        UpdateMirrorUiState();
    }

    /// <summary>
    /// 清除投屏宿主状态，将就绪和可见标志置为 false 并更新 UI。
    /// </summary>
    public void ClearMirrorHost()
    {
        _mirrorHostVisible = false;
        _mirrorReady = false;
        _mirrorHostPanel.MirrorActive = false;
        UpdateMirrorUiState();
    }

    /// <summary>
    /// 设置投屏宽高比，无效值时回退为默认竖屏 9:16，随后更新视口布局。
    /// </summary>
    /// <param name="aspectRatio">宽高比（宽/高）。</param>
    public void SetMirrorAspectRatio(double aspectRatio)
    {
        if (double.IsNaN(aspectRatio) || double.IsInfinity(aspectRatio) || aspectRatio <= 0)
        {
            aspectRatio = 9d / 16d;
        }

        _mirrorAspectRatio = aspectRatio;
        UpdateMirrorHostBounds();
    }

    /// <summary>
    /// 同步更新投屏视口的布局尺寸，触发 UpdateMirrorHostBounds。
    /// </summary>
    public void SyncMirrorBounds()
    {
        UpdateMirrorHostBounds();
    }

    /// <summary>
    /// 获取投屏视口在宿主面板内的显示区域矩形。
    /// </summary>
    /// <returns>视口显示区域矩形。</returns>
    public Rectangle GetMirrorDisplayBounds()
    {
        return _mirrorDisplayBounds;
    }

    public Size GetMirrorHostClientSize()
    {
        return _mirrorHostPanel.ClientSize;
    }

    /// <summary>
    /// 确保 scrcpy 投屏宿主面板和视口面板的窗口句柄已创建，返回宿主句柄。
    /// </summary>
    /// <returns>宿主面板窗口句柄，设计器模式返回零。</returns>
    public IntPtr EnsureMirrorHostHandle()
    {
        if (_isDesignMode)
        {
            return IntPtr.Zero;
        }

        UpdateMirrorHostBounds();

        if (_mirrorHostPanel?.IsHandleCreated != true)
        {
            _mirrorHostPanel?.CreateControl();
            _ = _mirrorHostPanel?.Handle;
        }

        EmbeddedWindowHost.EnableClipChildren(_mirrorHostPanel!.Handle);

        return MirrorHostHandle;
    }

    /// <summary>
    /// 尝试获取当前选中设备ID用于操作，返回是否选中了有效设备。
    /// </summary>
    /// <param name="deviceId">输出的设备ID，未选中时为空字符串。</param>
    /// <returns>是否选中了非空设备ID。</returns>
    private bool TryGetSelectedDeviceForAction(out string deviceId)
    {
        deviceId = _selectedDeviceId ?? string.Empty;
        return !string.IsNullOrEmpty(deviceId);
    }

    /// <summary>
    /// 刷新设备下拉列表，按显示名称排序，保留选中状态，若选中设备已不存在则清空。
    /// </summary>
    private void RefreshList()
    {
        if (_isDesignMode)
        {
            UpdateMirrorUiState();
            return;
        }

        _cmbDevices.BeginUpdate();
        _cmbDevices.SelectedIndexChanged -= OnDeviceSelected;
        _cmbDevices.Items.Clear();
        _cmbDevices.Items.Add(new DeviceSelectorItem(null, Language.All));

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

    /// <summary>
    /// 构建设备下拉列表项的显示文本：连接状态符号 + 型号 + QA/ADB标记 + 日志数。
    /// </summary>
    /// <param name="record">设备记录。</param>
    /// <returns>格式化的显示文本。</returns>
    private string BuildDeviceDisplayText(DeviceRecord record)
    {
        var status = record.IsAdbOnly || !record.Info.IsConnected ? "\u25CB" : "\u25CF";
        var qa = record.Info.IsQa ? " [QA]" : string.Empty;
        var adb = record.Info.AdbSerial != null ? " [ADB]" : string.Empty;
        return $"{status} {record.Info.DisplayName}{qa}{adb} ({record.LogCount})";
    }

    /// <summary>
    /// 设备下拉框选中变更事件处理器，更新选中设备ID、同步选中项、刷新投屏UI并触发 DeviceSelected 事件。
    /// </summary>
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

    /// <summary>
    /// 同步下拉框的选中项与当前 _selectedDeviceId，找不到时回退到"全部"选项。
    /// </summary>
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

    /// <summary>
    /// 更新投屏区域所有 UI 元素的状态：占位符可见性、状态文本、按钮启用/禁用和启动按钮文本切换。
    /// </summary>
    private void UpdateMirrorUiState()
    {
        if (_isDesignMode)
        {
            _lblMirrorStatus.Text = _mirrorStatusText;
            _btnMirrorToggle.Text = Language.Start;
            button1.Text = Language.Start;
            return;
        }

        var hasSpecificDevice = !string.IsNullOrEmpty(_selectedDeviceId);
        DeviceRecord? selectedRecord = null;
        var hasSelectedRecord = hasSpecificDevice && _devices.TryGetValue(_selectedDeviceId!, out selectedRecord);
        var hasAdb = hasSelectedRecord && !string.IsNullOrEmpty(selectedRecord?.Info.AdbSerial);

        _lblMirrorStatus.Text = _mirrorStatusText;
        if (!_mirrorHostVisible)
        {
            _mirrorHostPanel.Invalidate();
        }

        _btnMirrorToggle.Enabled = hasAdb;
        _btnMirrorReconnect.Enabled = hasAdb && _mirrorRunning;
        button1.Enabled = hasAdb;
        button2.Enabled = hasAdb && _mirrorRunning;
        _btnMirrorRotate.Enabled = hasAdb && _mirrorRunning;
        _btnMirrorScreenshot.Enabled = hasAdb;
        _btnMirrorPopout.Enabled = hasAdb;
        _btnMirrorToggle.Text = _mirrorRunning ? Language.Stop : Language.Start;
        button1.Text = _mirrorRunning ? Language.Stop : Language.Start;
    }

    /// <summary>
    /// 根据宽高比和宿主面板尺寸计算投屏视口的居中布局矩形，并触发 MirrorLayoutChanged 事件。
    /// </summary>
    private void UpdateMirrorHostBounds()
    {
        if (_isDesignMode || _mirrorHostPanel == null)
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
        if (_mirrorDisplayBounds != nextBounds)
        {
            _mirrorDisplayBounds = nextBounds;
            MirrorLayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        if (!_mirrorHostVisible)
        {
            _mirrorHostPanel.Invalidate();
        }
    }

    /// <summary>
    /// 设备记录内部类，包含设备信息、日志计数和是否仅通过 ADB 发现。
    /// </summary>
    private sealed class DeviceRecord
    {
        /// <summary>设备信息。</summary>
        public DeviceInfo Info { get; set; } = new();
        /// <summary>日志条数。</summary>
        public int LogCount { get; set; }
        /// <summary>是否仅通过 ADB 发现（无 TCP 连接）。</summary>
        public bool IsAdbOnly { get; set; }
    }

    /// <summary>
    /// 设备下拉列表项包装类，关联设备ID和显示文本，ToString 返回显示文本供 ComboBox 渲染。
    /// </summary>
    private sealed class DeviceSelectorItem
    {
        /// <summary>
        /// 创建设备选择器项。
        /// </summary>
        /// <param name="deviceId">设备ID，null 表示"全部"选项。</param>
        /// <param name="text">下拉列表显示文本。</param>
        public DeviceSelectorItem(string? deviceId, string text)
        {
            DeviceId = deviceId;
            Text = text;
        }

        /// <summary>设备ID，null 表示"全部"选项。</summary>
        public string? DeviceId { get; }
        /// <summary>下拉列表显示文本。</summary>
        public string Text { get; }

        /// <summary>返回显示文本，供 ComboBox 渲染。</summary>
        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// 判断当前是否处于设计器模式，通过 LicenseManager 和进程名/命令行检测 IDE 环境。
    /// </summary>
    /// <returns>是否处于设计器模式。</returns>
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

/// <summary>
/// 投屏宿主面板，当嵌入镜像窗口时跳过背景绘制，避免覆盖 scrcpy 子窗口。
/// </summary>
internal sealed class MirrorHostPanel : Panel
{
    public bool MirrorActive { get; set; }
}
