using System.Text.Json;

namespace LogViewer.Utils;

public static class JsonFormatter
{
    public static string? FormatJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        try
        {
            var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            return raw;
        }
    }

    public static bool IsValidJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static JsonDocument? ParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonDocument.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    public static string GetJsonPath(TreeNode node)
    {
        var parts = new List<string>();
        var current = node;
        while (current != null && current.Tag is JsonPathInfo info)
        {
            parts.Insert(0, info.PathSegment);
            current = current.Parent;
        }
        return parts.Count > 0 ? "$." + string.Join(".", parts) : "$";
    }
}

public class JsonPathInfo
{
    public string? Key { get; set; }
    public string PathSegment { get; set; } = "";
    public JsonValueKind ValueKind { get; set; }
    public string? RawValue { get; set; }
}