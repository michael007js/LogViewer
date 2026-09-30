using System.Text.Encodings.Web;
using System.Text.Json;

namespace LogViewer.Utils;

/// <summary>
/// JSON 格式化和解析工具类，提供 JSON 美化输出、有效性校验和 JSONPath 查询功能。
/// </summary>
public static class JsonFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly SemaphoreSlim BackgroundJsonGate = new(2, 2);

    internal static async Task<T> RunBackgroundAsync<T>(Func<T> work, CancellationToken token)
    {
        await BackgroundJsonGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            return await Task.Run(work).ConfigureAwait(false);
        }
        finally
        {
            BackgroundJsonGate.Release();
        }
    }

    /// <summary>
    /// 将原始 JSON 字符串格式化为缩进美化的输出。
    /// 使用 UnsafeRelaxedJsonEscaping 避免中文等 Unicode 字符被转义为 \uXXXX。
    /// </summary>
    /// <param name="raw">原始 JSON 字符串，如果为空或非有效 JSON 则原样返回。</param>
    /// <returns>格式化后的 JSON 字符串，如果输入无效则返回原始值。</returns>
    public static string? FormatJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc, IndentedOptions);
        }
        catch
        {
            return raw;
        }
    }

    /// <summary>
    /// 检查字符串是否为有效的 JSON 格式。
    /// </summary>
    /// <param name="text">待检查的字符串。</param>
    /// <returns>如果是有效 JSON 返回 true，否则返回 false。</returns>
    public static bool IsValidJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全解析 JSON 字符串为 JsonDocument，解析失败返回 null。
    /// </summary>
    /// <param name="text">待解析的 JSON 字符串。</param>
    /// <returns>解析成功的 JsonDocument，失败返回 null。</returns>
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

    /// <summary>
    /// 根据 TreeNode 的 Tag（JsonPathInfo）沿父节点向上拼接，生成标准 JSONPath 表达式。
    /// 例如：$.store.book[0].title
    /// </summary>
    /// <param name="node">目标树节点，其 Tag 须为 JsonPathInfo。</param>
    /// <returns>从根到当前节点的 JSONPath 字符串。</returns>
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

internal enum JsonDeferredContentKind
{
    None,
    EmbeddedJson,
    LongText
}

/// <summary>
/// JSON 路径信息，附加到 TreeView 节点的 Tag 上，
/// 用于标识节点在 JSON 结构中的位置、值类型和原始值。
/// </summary>
public class JsonPathInfo
{
    /// <summary>JSON 对象的完整属性键名。</summary>
    public string? Key { get; set; }

    /// <summary>经过单行转义和长度限制的键名，仅用于 UI 绘制。</summary>
    internal string? DisplayKey { get; set; }

    /// <summary>JSONPath 路径片段（如属性名或数组索引）。</summary>
    public string PathSegment { get; set; } = "";

    /// <summary>节点对应 JSON 值的类型。</summary>
    public JsonValueKind ValueKind { get; set; }

    /// <summary>节点对应的原始 JSON 值文本。</summary>
    public string? RawValue { get; set; }

    /// <summary>节点对应的原始 JsonElement，用于完整复制且不依赖显示文本。</summary>
    internal JsonElement? Element { get; set; }

    /// <summary>对象、数组或解析后的内嵌 JSON 子节点数据源。</summary>
    internal JsonElement? ChildElement { get; set; }

    /// <summary>需要按需加载的内嵌 JSON 或长文本类型。</summary>
    internal JsonDeferredContentKind DeferredContent { get; set; }

    /// <summary>后台预计算的图片 URL，悬浮时不再扫描完整值。</summary>
    internal string? CachedImageUrl { get; set; }

    /// <summary>节点在直接父容器中的原始顺序，用于搜索投影和分页去重。</summary>
    internal int ChildOrdinal { get; set; }

    /// <summary>节点是否由搜索路径临时物化，正常分页加载后会接管该节点。</summary>
    internal bool IsSearchProjection { get; set; }

    /// <summary>节点是否正在异步加载，防止重复展开启动多个任务。</summary>
    internal bool IsLoading { get; set; }

    internal JsonPathInfo CloneForUi(bool searchProjection)
    {
        return new JsonPathInfo
        {
            Key = Key,
            DisplayKey = DisplayKey,
            PathSegment = PathSegment,
            ValueKind = ValueKind,
            RawValue = RawValue,
            Element = Element,
            ChildElement = ChildElement,
            DeferredContent = DeferredContent,
            CachedImageUrl = CachedImageUrl,
            ChildOrdinal = ChildOrdinal,
            IsSearchProjection = searchProjection
        };
    }

    internal string? GetFullValue()
    {
        if (RawValue != null) return RawValue;
        if (Element is not { } element) return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
    }

    internal string? GetNodeJson()
    {
        if (Element is { } element) return element.GetRawText();
        return ValueKind == JsonValueKind.String ? JsonSerializer.Serialize(RawValue) : RawValue;
    }
}