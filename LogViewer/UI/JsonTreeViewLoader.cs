using System.Text.Json;
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

    /// <summary>
    /// 将 JsonElement 加载到 TreeView 中，仅构建顶层节点（懒加载）。
    /// </summary>
    public static void LoadJson(this TreeView treeView, JsonElement rootElement, Font displayFont)
    {
        treeView.BeginUpdate();
        treeView.Nodes.Clear();
        BuildTreeLevel(rootElement, treeView.Nodes, "", displayFont);
        treeView.EndUpdate();
        treeView.CollapseToLevel(1);
    }

    /// <summary>
    /// BeforeExpand 事件处理：检测哨兵子节点，移除后从 JsonElement 构建真实子节点。
    /// </summary>
    public static void OnBeforeExpand(TreeNode node, Font displayFont)
    {
        if (node.Tag is not JsonPathInfo { Element: not null }) return;

        if (node.Nodes.Count == 1 && ReferenceEquals(node.Nodes[0].Tag, LazyMarker))
        {
            node.Nodes.Clear();
            var info = (JsonPathInfo)node.Tag;
            BuildTreeLevel(info.Element!.Value, node.Nodes, GetPathPrefix(node), displayFont);
        }
    }

    /// <summary>
    /// 将纯文本按行加载到 TreeView 中，每行作为一个节点。
    /// </summary>
    public static void LoadPlainText(this TreeView treeView, string text, Font displayFont)
    {
        treeView.BeginUpdate();
        treeView.Nodes.Clear();

        if (!string.IsNullOrEmpty(text))
        {
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var node = new TreeNode(lines[i])
                    { Tag = new JsonPathInfo { RawValue = lines[i] }, NodeFont = displayFont };
                treeView.Nodes.Add(node);
            }
        }

        treeView.EndUpdate();
    }

    /// <summary>
    /// 折叠 TreeView 所有节点，然后展开到指定层级。
    /// </summary>
    public static void CollapseToLevel(this TreeView treeView, int level)
    {
        treeView.BeginUpdate();
        treeView.CollapseAll();
        ExpandToLevel(treeView.Nodes, level, 0);
        treeView.EndUpdate();
    }

    /// <summary>
    /// 搜索 TreeView 中包含关键字的节点，将其加入高亮集合并展开到根节点可见。
    /// 仅搜索已构建（已展开过）的节点。
    /// </summary>
    public static void SearchAndHighlight(this TreeView treeView, HashSet<TreeNode> highlightedNodes, string keyword)
    {
        highlightedNodes.Clear();
        if (string.IsNullOrEmpty(keyword))
        {
            treeView.Invalidate();
            return;
        }

        SearchNodes(treeView.Nodes, keyword, highlightedNodes);
        foreach (var node in highlightedNodes)
            ExpandToRoot(node);
        treeView.Invalidate();
    }

    /// <summary>
    /// 仅构建一层节点。Object/Array 节点添加哨兵子节点以显示 [+] 展开指示器，
    /// 子节点在 BeforeExpand 时由 OnBeforeExpand 懒加载构建。
    /// </summary>
    private static void BuildTreeLevel(JsonElement element, TreeNodeCollection parent, string pathPrefix,
        Font displayFont)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    AddPropertyNode(parent, prop.Name, prop.Value, $".{prop.Name}", pathPrefix, displayFont);
                }

                break;

            case JsonValueKind.Array:
                int idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AddArrayItemNode(parent, idx, item, pathPrefix, displayFont);
                    idx++;
                }

                break;

            default:
                var rootInfo = new JsonPathInfo
                {
                    PathSegment = "$",
                    ValueKind = element.ValueKind,
                    RawValue = GetRawValue(element)
                };
                parent.Add(new TreeNode(FormatValue(element)) { Tag = rootInfo, NodeFont = displayFont });
                break;
        }
    }

    /// <summary>添加 JSON 对象属性节点，Object/Array 类型的值添加哨兵子节点。</summary>
    private static void AddPropertyNode(TreeNodeCollection parent, string name, JsonElement value,
        string pathSegment, string pathPrefix, Font displayFont)
    {
        var info = new JsonPathInfo
        {
            Key = name,
            PathSegment = pathSegment,
            ValueKind = value.ValueKind
        };

        if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
        {
            info.Element = value;
            int count = value.ValueKind == JsonValueKind.Object
                ? value.EnumerateObject().Count()
                : value.GetArrayLength();
            var summary = value.ValueKind == JsonValueKind.Object ? $"{{{count}}}" : $"[{count}]";
            var node = new TreeNode($"\u25B6 \"{name}\": {summary}")
            {
                Tag = info,
                NodeFont = displayFont
            };
            node.Nodes.Add(new TreeNode { Tag = LazyMarker });
            parent.Add(node);
        }
        else
        {
            info.RawValue = GetRawValue(value);
            var node = new TreeNode($" \"{name}\": {FormatValue(value)}")
            {
                Tag = info,
                NodeFont = displayFont
            };
            parent.Add(node);
        }
    }

    /// <summary>添加 JSON 数组元素节点，Object/Array 类型的项添加哨兵子节点。</summary>
    private static void AddArrayItemNode(TreeNodeCollection parent, int index, JsonElement item,
        string pathPrefix, Font displayFont)
    {
        var info = new JsonPathInfo
        {
            Key = $"[{index}]",
            PathSegment = $"[{index}]",
            ValueKind = item.ValueKind
        };

        if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
        {
            info.Element = item;
            int count = item.ValueKind == JsonValueKind.Object
                ? item.EnumerateObject().Count()
                : item.GetArrayLength();
            var summary = item.ValueKind == JsonValueKind.Object ? $"{{{count}}}" : $"[{count}]";
            var node = new TreeNode($"\u25B6 [{index}]: {summary}")
            {
                Tag = info,
                NodeFont = displayFont
            };
            node.Nodes.Add(new TreeNode { Tag = LazyMarker });
            parent.Add(node);
        }
        else
        {
            info.RawValue = GetRawValue(item);
            var node = new TreeNode($" [{index}]: {FormatValue(item)}")
            {
                Tag = info,
                NodeFont = displayFont
            };
            parent.Add(node);
        }
    }

    /// <summary>沿父节点向上拼接 PathSegment，生成当前节点的 JSONPath 前缀。</summary>
    private static string GetPathPrefix(TreeNode node)
    {
        var parts = new List<string>();
        var current = node;
        while (current != null && current.Tag is JsonPathInfo info)
        {
            parts.Insert(0, info.PathSegment);
            current = current.Parent;
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// 将 JsonElement 的值格式化为带引号的显示文本。
    /// </summary>
    internal static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
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

    /// <summary>
    /// 递归展开/折叠节点到指定层级。
    /// </summary>
    private static void ExpandToLevel(TreeNodeCollection nodes, int targetLevel, int currentLevel)
    {
        foreach (TreeNode node in nodes)
        {
            if (currentLevel < targetLevel)
            {
                node.Expand();
                ExpandToLevel(node.Nodes, targetLevel, currentLevel + 1);
            }
            else
            {
                node.Collapse();
            }
        }
    }

    /// <summary>
    /// 递归搜索节点集合，将文本包含关键字的节点加入结果集。
    /// </summary>
    private static void SearchNodes(TreeNodeCollection nodes, string keyword, HashSet<TreeNode> result)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                result.Add(node);
            SearchNodes(node.Nodes, keyword, result);
        }
    }

    /// <summary>
    /// 从指定节点向上逐级展开父节点，确保该节点可见。
    /// </summary>
    private static void ExpandToRoot(TreeNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            current.Expand();
            current = current.Parent;
        }
    }
}