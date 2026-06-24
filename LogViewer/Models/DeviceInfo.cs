namespace LogViewer.Models;

/// <summary>
/// 设备注册信息模型，记录已连接 Android 设备的基本信息和状态。
/// 设备连接后通过 TCP 发送注册消息（类型 0x01），PC 端解析并创建此对象。
/// 用于设备列表展示和日志源标识。
/// </summary>
public class DeviceInfo
{
    /// <summary>设备唯一标识（由 App 端生成，如 UUID）。</summary>
    public string? DeviceId { get; set; }

    /// <summary>设备型号名称（如 Pixel 6、Xiaomi 12）。</summary>
    public string? DeviceModel { get; set; }

    /// <summary>Android 系统版本号（如 13、14）。</summary>
    public string? AndroidVersion { get; set; }

    /// <summary>应用版本号（如 3.2.1）。</summary>
    public string? AppVersion { get; set; }

    /// <summary>是否为 QA/测试环境版本。</summary>
    public bool IsQa { get; set; }

    /// <summary>设备连接时间戳。</summary>
    public DateTime ConnectedTime { get; set; } = DateTime.Now;

    /// <summary>设备是否处于已连接状态。</summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>设备的 ADB 序列号（用于 adb 命令操作）。</summary>
    public string? AdbSerial { get; set; }

    /// <summary>设备的友好显示名称，用于 UI 列表展示。格式："型号 v版本 [QA]"。</summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(DeviceModel)) parts.Add(DeviceModel);
            if (!string.IsNullOrEmpty(AppVersion)) parts.Add($"v{AppVersion}");
            if (IsQa) parts.Add("QA");
            return parts.Count > 0 ? string.Join(" ", parts) : DeviceId ?? "Unknown";
        }
    }
}