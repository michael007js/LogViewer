namespace LogViewer.Models;

public class LogEntry
{
    public string? Send { get; set; }
    public string? Message { get; set; }
    public string? Url { get; set; }
    public int Code { get; set; }
    public bool IsRedirect { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsHttps { get; set; }
    public string? Protocol { get; set; }
    public string? Method { get; set; }
    public string? Headers { get; set; }
    public string? Content { get; set; }
    public long SendTime { get; set; }
    public long ReceiveTime { get; set; }

    public long Duration => ReceiveTime - SendTime;
    public DateTime SendTimeDt => DateTimeOffset.FromUnixTimeMilliseconds(SendTime).LocalDateTime;

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

    public string? SourceDeviceId { get; set; }

    public bool IsSuccessStatusCode => Code >= 200 && Code < 300;

    private static string TruncatePreview(string? text, int maxLen = 120)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = text.Replace("\r", "").Replace("\n", " ").Trim();
        return s.Length <= maxLen ? s : s[..maxLen] + "...";
    }

    public string SendPreview => TruncatePreview(Send);
    public string ContentPreview => TruncatePreview(Content);
}