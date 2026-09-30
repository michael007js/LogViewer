using System.Text.Json;
using System.Text.RegularExpressions;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// TreeView 的扩展方法类，提供 JSON/纯文本加载、搜索高亮、折叠展开等功能。
/// JSON 加载采用懒加载策略：仅构建当前展开层的节点，Object/Array 节点添加哨兵子节点
/// 以显示 [+] 展开指示器，BeforeExpand 事件触发时才构建实际子节点。
/// </summary>
public static class JsonTreeViewLoader
{
    /// <summary>哨兵对象，标记尚未加载子节点的 Object/Array 节点。</summary>
    internal static readonly object LazyMarker = new();

    internal const int PageSize = 100;
    internal const int UiBatchSize = 20;
    private const int LongTextThreshold = 512;
    internal const int TextChunkSize = 256;
    private const int MaxKeyDisplayLength = 128;

    internal sealed record JsonPageRequest(JsonPageCursor Cursor);

    internal sealed record TextPageRequest(string Source, int Offset);

    internal sealed record JsonNodeDescriptor(string Text, JsonPathInfo Info, bool HasChildren);

    internal sealed record JsonNodePage(IReadOnlyList<JsonNodeDescriptor> Items, object? NextPage);

    internal sealed record JsonSearchPath(IReadOnlyList<JsonNodeDescriptor> Nodes);

    internal sealed class JsonSearchSession : IDisposable
    {
        private readonly string _keyword;
        private readonly CancellationToken _token;
        private readonly IEnumerator<JsonSearchPath> _enumerator;

        internal JsonSearchSession(JsonElement root, string keyword, CancellationToken token)
        {
            _keyword = keyword;
            _token = token;
            _enumerator = EnumerateElement(root, new List<JsonNodeDescriptor>()).GetEnumerator();
        }

        internal bool HasReturnedMatch { get; private set; }

        internal JsonSearchPath? FindNext()
        {
            _token.ThrowIfCancellationRequested();
            if (!_enumerator.MoveNext()) return null;
            HasReturnedMatch = true;
            return _enumerator.Current;
        }

        public void Dispose() => _enumerator.Dispose();

        private IEnumerable<JsonSearchPath> EnumerateElement(JsonElement element,
            List<JsonNodeDescriptor> path)
        {
            _token.ThrowIfCancellationRequested();
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    int objectOrdinal = 0;
                    foreach (var property in element.EnumerateObject())
                    {
                        var descriptor = CreateDescriptor(property.Name, null, objectOrdinal++, property.Value,
                            null);
                        foreach (var result in VisitDescriptor(descriptor, path)) yield return result;
                    }
                    break;

                case JsonValueKind.Array:
                    int arrayIndex = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var descriptor = CreateDescriptor(null, arrayIndex, arrayIndex, item, null);
                        arrayIndex++;
                        foreach (var result in VisitDescriptor(descriptor, path)) yield return result;
                    }
                    break;

                default:
                    var rootDescriptor = CreateDescriptor(null, null, 0, element, null);
                    foreach (var result in VisitDescriptor(rootDescriptor, path)) yield return result;
                    break;
            }
        }

        private IEnumerable<JsonSearchPath> VisitDescriptor(JsonNodeDescriptor descriptor,
            List<JsonNodeDescriptor> path)
        {
            _token.ThrowIfCancellationRequested();
            path.Add(descriptor);
            var info = descriptor.Info;
            bool matched = info.Key?.Contains(_keyword, StringComparison.OrdinalIgnoreCase) == true;
            string? rawValue = info.ValueKind == JsonValueKind.Null ? "null" : info.RawValue;
            bool rawMatched = rawValue?.Contains(_keyword, StringComparison.OrdinalIgnoreCase) == true;
            if (matched) yield return new JsonSearchPath(path.ToArray());

            JsonElement? childElement = info.ChildElement;
            if (info.DeferredContent == JsonDeferredContentKind.EmbeddedJson && info.RawValue != null)
            {
                try
                {
                    using var document = JsonDocument.Parse(info.RawValue);
                    childElement = document.RootElement.Clone();
                    info.ChildElement = childElement;
                    info.DeferredContent = JsonDeferredContentKind.None;
                }
                catch (JsonException)
                {
                }
            }

            if (childElement is { } child)
            {
                foreach (var result in EnumerateElement(child, path)) yield return result;
            }

            if (!matched && rawMatched) yield return new JsonSearchPath(path.ToArray());
            path.RemoveAt(path.Count - 1);
        }
    }

    internal sealed class JsonPageCursor
    {
        private readonly JsonValueKind _kind;
        private JsonElement.ObjectEnumerator _objectEnumerator;
        private JsonElement.ArrayEnumerator _arrayEnumerator;
        private readonly JsonElement _scalar;
        private JsonNodeDescriptor? _pending;
        private int _objectIndex;
        private int _arrayIndex;
        private bool _scalarRead;

        internal JsonPageCursor(JsonElement source)
        {
            _kind = source.ValueKind;
            _scalar = source;
            if (_kind == JsonValueKind.Object) _objectEnumerator = source.EnumerateObject();
            if (_kind == JsonValueKind.Array) _arrayEnumerator = source.EnumerateArray();
        }

        internal JsonNodePage ReadPage(Regex? imageUrlRegex, CancellationToken token)
        {
            var items = new List<JsonNodeDescriptor>(PageSize + 1);
            if (_pending != null)
            {
                items.Add(_pending);
                _pending = null;
            }

            while (items.Count <= PageSize && TryReadNext(imageUrlRegex, token, out var descriptor))
                items.Add(descriptor);

            object? nextPage = null;
            if (items.Count > PageSize)
            {
                _pending = items[^1];
                items.RemoveAt(items.Count - 1);
                nextPage = new JsonPageRequest(this);
            }
            return new JsonNodePage(items, nextPage);
        }

        private bool TryReadNext(Regex? imageUrlRegex, CancellationToken token,
            out JsonNodeDescriptor descriptor)
        {
            token.ThrowIfCancellationRequested();
            switch (_kind)
            {
                case JsonValueKind.Object when _objectEnumerator.MoveNext():
                    var property = _objectEnumerator.Current;
                    descriptor = CreateDescriptor(property.Name, null, _objectIndex++, property.Value, imageUrlRegex);
                    return true;
                case JsonValueKind.Array when _arrayEnumerator.MoveNext():
                    descriptor = CreateDescriptor(null, _arrayIndex, _arrayIndex++, _arrayEnumerator.Current,
                        imageUrlRegex);
                    return true;
                default:
                    if (!_scalarRead && _kind is not JsonValueKind.Object and not JsonValueKind.Array)
                    {
                        _scalarRead = true;
                        descriptor = CreateDescriptor(null, null, 0, _scalar, imageUrlRegex);
                        return true;
                    }
                    descriptor = null!;
                    return false;
            }
        }
    }

    internal static JsonNodePage CreateJsonPage(JsonElement element, Regex? imageUrlRegex,
        CancellationToken token) => new JsonPageCursor(element).ReadPage(imageUrlRegex, token);

    internal static JsonNodePage CreateJsonPage(JsonPageCursor cursor, Regex? imageUrlRegex,
        CancellationToken token) => cursor.ReadPage(imageUrlRegex, token);

    internal static JsonNodePage CreateTextPage(string text, int offset, Regex? imageUrlRegex,
        CancellationToken token)
    {
        var items = new List<JsonNodeDescriptor>(PageSize);
        int position = offset;
        while (position < text.Length && items.Count < PageSize)
        {
            token.ThrowIfCancellationRequested();
            int chunkIndex = position / TextChunkSize;
            var descriptor = CreateTextDescriptor(text, chunkIndex, imageUrlRegex);
            items.Add(descriptor);
            position += descriptor.Info.RawValue?.Length ?? TextChunkSize;
        }

        object? nextPage = position < text.Length ? new TextPageRequest(text, position) : null;
        return new JsonNodePage(items, nextPage);
    }

    internal static JsonNodeDescriptor CreateTextDescriptor(string text, int chunkIndex, Regex? imageUrlRegex)
    {
        int position = chunkIndex * TextChunkSize;
        int length = Math.Min(TextChunkSize, Math.Max(0, text.Length - position));
        string chunk = length == 0 ? string.Empty : text.Substring(position, length);
        var info = new JsonPathInfo
        {
            PathSegment = $"[text:{chunkIndex}]",
            ChildOrdinal = chunkIndex,
            ValueKind = JsonValueKind.String,
            RawValue = chunk,
            CachedImageUrl = FindImageUrl(chunk, imageUrlRegex)
        };
        string escaped = JsonSerializer.Serialize(chunk);
        return new JsonNodeDescriptor($" [{chunkIndex + 1}]: {escaped}", info, false);
    }

    internal static JsonNodeDescriptor CloneForUi(JsonNodeDescriptor descriptor, bool searchProjection) =>
        new(descriptor.Text, descriptor.Info.CloneForUi(searchProjection), descriptor.HasChildren);

    internal static TreeNode CreateTreeNode(JsonNodeDescriptor descriptor)
    {
        var node = new TreeNode(descriptor.Text) { Tag = descriptor.Info };
        if (descriptor.HasChildren) node.Nodes.Add(new TreeNode { Tag = LazyMarker });
        return node;
    }

    internal static TreeNode CreatePageNode(object request)
    {
        var node = new TreeNode($"\u25B6 {Language.JsonLoadMore}") { Tag = request };
        node.Nodes.Add(new TreeNode { Tag = LazyMarker });
        return node;
    }

    private static JsonNodeDescriptor CreateDescriptor(string? key, int? index, int childOrdinal,
        JsonElement value, Regex? imageUrlRegex)
    {
        var info = new JsonPathInfo
        {
            Key = key,
            DisplayKey = key == null ? null : FormatKeyForDisplay(key),
            PathSegment = key != null ? $".{key}" : index.HasValue ? $"[{index.Value}]" : "$",
            ChildOrdinal = childOrdinal,
            ValueKind = value.ValueKind,
            Element = value
        };

        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            info.ChildElement = value;
            string summary = value.ValueKind == JsonValueKind.Object ? "{...}" : $"[{value.GetArrayLength()}]";
            return new JsonNodeDescriptor(BuildNodeText(key, index, summary, true), info, true);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string rawValue = value.GetString() ?? string.Empty;
            info.RawValue = rawValue;
            info.CachedImageUrl = FindImageUrl(rawValue, imageUrlRegex);

            if (LooksLikeJsonContainer(rawValue))
            {
                info.DeferredContent = JsonDeferredContentKind.EmbeddedJson;
                return new JsonNodeDescriptor(
                    BuildNodeText(key, index, Language.EmbeddedJsonSummary(rawValue.Length), true), info, true);
            }

            if (rawValue.Length > LongTextThreshold || rawValue.IndexOfAny(['\r', '\n']) >= 0)
            {
                info.DeferredContent = JsonDeferredContentKind.LongText;
                return new JsonNodeDescriptor(
                    BuildNodeText(key, index, Language.LongTextSummary(rawValue.Length), true), info, true);
            }
        }
        else
        {
            info.RawValue = GetRawValue(value);
            if (info.RawValue is { Length: > LongTextThreshold } rawValue)
            {
                info.DeferredContent = JsonDeferredContentKind.LongText;
                return new JsonNodeDescriptor(
                    BuildNodeText(key, index, Language.LongTextSummary(rawValue.Length), true), info, true);
            }
        }

        return new JsonNodeDescriptor(BuildNodeText(key, index, FormatValue(value), false), info, false);
    }

    private static string BuildNodeText(string? key, int? index, string value, bool expandable)
    {
        string marker = expandable ? "\u25B6 " : " ";
        if (key != null) return $"{marker}\"{FormatKeyForDisplay(key)}\": {value}";
        if (index.HasValue) return $"{marker}[{index.Value}]: {value}";
        return marker + value;
    }

    private static string FormatKeyForDisplay(string key)
    {
        string preview = key.Length <= MaxKeyDisplayLength
            ? key
            : $"{key[..96]}...{key[^16..]} <{key.Length:N0}>";
        return preview.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private static bool LooksLikeJsonContainer(string text)
    {
        ReadOnlySpan<char> trimmed = text.AsSpan().Trim();
        return trimmed.Length >= 2 &&
               ((trimmed[0] == '{' && trimmed[^1] == '}') || (trimmed[0] == '[' && trimmed[^1] == ']'));
    }

    private static string? FindImageUrl(string text, Regex? imageUrlRegex)
    {
        if (imageUrlRegex == null || text.Length == 0) return null;
        try
        {
            var match = imageUrlRegex.Match(text);
            return match.Success ? match.Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// 将 JsonElement 的值格式化为带引号的显示文本。
    /// </summary>
    internal static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetRawText(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// 获取 JsonElement 的原始值字符串，用于复制到剪贴板。
    /// </summary>
    internal static string? GetRawValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

}