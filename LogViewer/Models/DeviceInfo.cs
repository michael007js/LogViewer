namespace LogViewer.Models;

public class DeviceInfo
{
    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public string? AndroidVersion { get; set; }
    public string? AppVersion { get; set; }
    public bool IsQa { get; set; }

    public DateTime ConnectedTime { get; set; } = DateTime.Now;
    public bool IsConnected { get; set; } = true;
    public string? AdbSerial { get; set; }

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