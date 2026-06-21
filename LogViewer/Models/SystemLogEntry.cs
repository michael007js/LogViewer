using System.Drawing;
using System.Text.Json.Serialization;

namespace LogViewer.Models;

public class SystemLogEntry
{
    public long SequenceId { get; set; }
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public string? PackageName { get; set; }
    public string? Level { get; set; }
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public string? SourceDeviceSerial { get; set; }
    public string? SourceDeviceId { get; set; }

    [JsonIgnore]
    public Color LevelColor => Level?.ToUpper() switch
    {
        "V" => Color.Gray,
        "D" => Color.FromArgb(0, 0, 180),
        "I" => Color.Green,
        "W" => Color.Orange,
        "E" => Color.Red,
        "F" => Color.DarkRed,
        _ => Color.Black
    };

    [JsonIgnore]
    public string LevelShort => Level?.ToUpper() ?? "?";
}
