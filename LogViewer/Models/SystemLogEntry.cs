using System.Drawing;
using System.Text.Json.Serialization;

namespace LogViewer.Models;

/// <summary>
/// Android 系统日志条目模型，对应 adb logcat 输出的单条日志记录。
/// 包含日志级别、标签、消息内容等原始字段，以及用于 UI 着色的衍生属性。
/// 序列号 SequenceId 由接收端按到达顺序生成，保证显示顺序与写入顺序一致。
/// </summary>
public class SystemLogEntry
{
    /// <summary>日志到达的全局序号，由接收端递增赋值，用于保证显示顺序。</summary>
    public long SequenceId { get; set; }
    /// <summary>日志产生时间（设备本地时间）。</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>产生日志的进程 ID。</summary>
    public int ProcessId { get; set; }
    /// <summary>产生日志的线程 ID。</summary>
    public int ThreadId { get; set; }
    /// <summary>产生日志的包名（Android 应用标识）。</summary>
    public string? PackageName { get; set; }
    /// <summary>日志级别：V(Verbose)/D(Debug)/I(Info)/W(Warn)/E(Error)/F(Fatal)。</summary>
    public string? Level { get; set; }
    /// <summary>日志标签，标识日志来源模块。</summary>
    public string? Tag { get; set; }
    /// <summary>日志消息正文。</summary>
    public string? Message { get; set; }
    /// <summary>来源设备的 adb 序列号。</summary>
    public string? SourceDeviceSerial { get; set; }
    /// <summary>来源设备的唯一标识（由 PC 端生成）。</summary>
    public string? SourceDeviceId { get; set; }

    /// <summary>根据日志级别返回对应的前景色，用于 UI 行着色展示。</summary>
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

    /// <summary>日志级别的单字符缩写，用于表格紧凑显示。</summary>
    [JsonIgnore]
    public string LevelShort => Level?.ToUpper() ?? "?";
}
