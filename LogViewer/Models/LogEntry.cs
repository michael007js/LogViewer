namespace LogViewer.Models;

/// <summary>
/// 网络日志条目模型，对应 Android 端发送的 HTTP 网络请求日志。
/// 由 NetworkLogSender 捕获应用内的网络请求，通过 TCP 连接发送到 PC 端。
/// 包含请求/响应的关键字段，以及用于 UI 预览和展示的衍生属性。
/// </summary>
public class LogEntry
{
    /// <summary>请求内容（如 POST body）的字符串表示。</summary>
    public string? Send { get; set; }
    /// <summary>日志附加消息或摘要。</summary>
    public string? Message { get; set; }
    /// <summary>请求 URL 地址。</summary>
    public string? Url { get; set; }
    /// <summary>HTTP 状态码（如 200、404、500）。</summary>
    public int Code { get; set; }
    /// <summary>是否为重定向请求（3xx）。</summary>
    public bool IsRedirect { get; set; }
    /// <summary>请求是否成功（2xx 且无异常）。</summary>
    public bool IsSuccessful { get; set; }
    /// <summary>是否使用 HTTPS 协议。</summary>
    public bool IsHttps { get; set; }
    /// <summary>HTTP 协议版本（如 HTTP/1.1、HTTP/2）。</summary>
    public string? Protocol { get; set; }
    /// <summary>HTTP 请求方法（GET、POST、PUT 等）。</summary>
    public string? Method { get; set; }
    /// <summary>HTTP 请求/响应头原始内容。</summary>
    public string? Headers { get; set; }
    /// <summary>HTTP 响应体内容。</summary>
    public string? Content { get; set; }
    /// <summary>请求发送时间的 Unix 毫秒时间戳。</summary>
    public long SendTime { get; set; }
    /// <summary>响应接收时间的 Unix 毫秒时间戳。</summary>
    public long ReceiveTime { get; set; }

    /// <summary>请求耗时（毫秒），由 ReceiveTime - SendTime 计算得出。</summary>
    public long Duration => ReceiveTime - SendTime;
    /// <summary>请求发送时间的本地 DateTime 表示。</summary>
    public DateTime SendTimeDt => DateTimeOffset.FromUnixTimeMilliseconds(SendTime).LocalDateTime;

    /// <summary>从完整 URL 中提取的路径部分（含 query），用于表格紧凑展示。</summary>
    public string UrlPath
    {
        get
        {
            if (Url == null) return "";
            try
            {
                var uri = new Uri(Url);
                var path = uri.AbsolutePath;
                if (uri.Query.Length > 0) path += "?" + uri.Query;
                return path;
            }
            catch
            {
                return Url;
            }
        }
    }

    /// <summary>来源设备的唯一标识（由 PC 端生成）。</summary>
    public string? SourceDeviceId { get; set; }

    /// <summary>状态码是否表示成功（200-299 范围内）。</summary>
    public bool IsSuccessStatusCode => Code >= 200 && Code < 300;

    /// <summary>
    /// 截断文本到指定长度，用于生成表格预览字段。
    /// 将换行符替换为空格，超出部分以 "..." 结尾。
    /// </summary>
    private static string TruncatePreview(string? text, int maxLen = 120)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Replace("\r", "").Replace("\n", " ").Trim();
        return s.Length <= maxLen ? s : s[..maxLen] + "...";
    }

    /// <summary>请求内容的预览文本（截断 + 去换行）。</summary>
    public string SendPreview => TruncatePreview(Send);
    /// <summary>响应体的预览文本（截断 + 去换行）。</summary>
    public string ContentPreview => TruncatePreview(Content);
}