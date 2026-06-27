namespace LogViewer.Models;

/// <summary>
/// 网络/普通日志条目模型，对应 Android 端发送的日志数据。
/// 网络日志由 OkHttp3Utils 捕获（type=1），普通日志由 LogUtilsHook 捕获（type=2）。
/// 包含请求/响应的关键字段，以及用于 UI 预览和展示的衍生属性。
/// </summary>
public class LogEntry
{
    /// <summary>日志类型常量：网络日志（OkHttp3Utils 捕获）。</summary>
    public const int TypeNetworkLog = 1;

    /// <summary>日志类型常量：普通日志（LogUtilsHook 捕获）。</summary>
    public const int TypeNormalLog = 2;

    /// <summary>
    /// 日志类型标识。type=1 网络日志，type=2 普通日志。
    /// 旧版 Android 端不发送此字段，默认值为 0，IsNetworkLog 返回 true（向后兼容）。
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Android Log 级别常量（VERBOSE=2/DEBUG=3/INFO=4/WARN=5/ERROR=6/ASSERT=7）。
    /// 仅普通日志使用。旧版或不适用时默认值为 0，表示未知级别。
    /// 普通日志从 Android 端的 code 字段获取级别值（PropertyNameCaseInsensitive 反序列化到 Code），
    /// 当 Level 未被显式设置时（=0），对普通日志回退取 Code 值。
    /// </summary>
    public int Level { get; set; }

    /// <summary>有效级别值：Level > 0 时取 Level，否则普通日志回退取 Code。</summary>
    public int EffectiveLevel => Level > 0 ? Level : (IsNormalLog ? Code : 0);

    /// <summary>是否为普通日志（Type == TypeNormalLog）。</summary>
    public bool IsNormalLog => Type == TypeNormalLog;

    /// <summary>是否为网络日志（Type == 0 旧版兼容 或 Type == TypeNetworkLog）。</summary>
    public bool IsNetworkLog => Type != TypeNormalLog;
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
    public long Duration => ReceiveTime > 0 ? ReceiveTime - SendTime : 0;

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

    /// <summary>
    /// 普通日志调用位置信息，格式如 "[main, com.xxx.Activity.onCreate(Activity.java:123)]: "。
    /// 由 Android 端 LogUtils 的 TagHead.fileHead 传入。
    /// </summary>
    public string? FileHead { get; set; }

    /// <summary>
    /// 普通日志调用栈头部信息数组，由 Android 端 LogUtils 的 TagHead.consoleHead 传入。
    /// </summary>
    public string[]? ConsoleHead { get; set; }

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