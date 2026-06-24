using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// TreeView 的扩展方法类，提供 JSON/纯文本加载、搜索高亮、折叠展开等功能。
/// </summary>
public static class JsonTreeViewLoader
{
    /// <summary>
    /// 将 JSON 文本解析并加载到 TreeView 中，生成语法高亮的树节点。
    /// 若 JSON 解析失败则自动降级为纯文本加载。
    /// </summary>
    /// <param name="treeView">目标 TreeView 控件。</param>
    /// <param name="rawJson">原始 JSON 字符串。</param>
    /// <param name="displayFont">节点显示字体。</param>
    public static void LoadJson(this TreeView treeView, string rawJson, Font displayFont)
    {
        var formatted = JsonFormatter.FormatJson(rawJson);
        var doc = JsonFormatter.ParseJson(formatted ?? rawJson);
        if (doc != null)
        {
            treeView.BeginUpdate();
            treeView.Nodes.Clear();
            BuildTree(doc.RootElement, treeView.Nodes, "", displayFont);
            treeView.EndUpdate();
            treeView.CollapseToLevel(1);
        }
        else
        {
            treeView.LoadPlainText(rawJson, displayFont);
        }
    }

    /// <summary>
    /// 将纯文本按行加载到 TreeView 中，每行作为一个节点。
    /// </summary>
    /// <param name="treeView">目标 TreeView 控件。</param>
    /// <param name="text">纯文本内容。</param>
    /// <param name="displayFont">节点显示字体。</param>
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
    /// <param name="treeView">目标 TreeView 控件。</param>
    /// <param name="level">要展开到的层级（0=全折叠，1=只展开根节点）。</param>
    public static void CollapseToLevel(this TreeView treeView, int level)
    {
        treeView.BeginUpdate();
        treeView.CollapseAll();
        ExpandToLevel(treeView.Nodes, level, 0);
        treeView.EndUpdate();
    }

    /// <summary>
    /// 搜索 TreeView 中包含关键字的节点，将其加入高亮集合并展开到根节点可见。
    /// </summary>
    /// <param name="treeView">目标 TreeView 控件。</param>
    /// <param name="highlightedNodes">高亮节点集合，会被清空后重新填充。</param>
    /// <param name="keyword">搜索关键字，为空时清除所有高亮。</param>
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
    /// 根据 JsonElement 的值类型递归构建树节点，区分对象、数组、叶值三种情况。
    /// </summary>
    /// <param name="element">当前 JSON 元素。</param>
    /// <param name="parent">父节点的子节点集合。</param>
    /// <param name="pathPrefix">当前节点的 JSONPath 前缀。</param>
    /// <param name="displayFont">节点显示字体。</param>
    private static void BuildTree(JsonElement element, TreeNodeCollection parent, string pathPrefix, Font displayFont)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var info = new JsonPathInfo
                    {
                        Key = prop.Name,
                        PathSegment = $".{prop.Name}",
                        ValueKind = prop.Value.ValueKind
                    };

                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        int count = prop.Value.ValueKind == JsonValueKind.Object
                            ? prop.Value.EnumerateObject().Count()
                            : prop.Value.GetArrayLength();
                        var summary = prop.Value.ValueKind == JsonValueKind.Object ? $"{{{count}}}" : $"[{count}]";
                        var node = new TreeNode($"\u25B6 \"{prop.Name}\": {summary}")
                        {
                            Tag = info,
                            NodeFont = displayFont
                        };
                        parent.Add(node);
                        BuildTree(prop.Value, node.Nodes, $"{pathPrefix}.{prop.Name}", displayFont);
                    }
                    else
                    {
                        info.RawValue = GetRawValue(prop.Value);
                        var node = new TreeNode($" \"{prop.Name}\": {FormatValue(prop.Value)}")
                        {
                            Tag = info,
                            NodeFont = displayFont
                        };
                        parent.Add(node);
                    }
                }

                break;

            case JsonValueKind.Array:
                int idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var info = new JsonPathInfo
                    {
                        Key = $"[{idx}]",
                        PathSegment = $"[{idx}]",
                        ValueKind = item.ValueKind
                    };

                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        int count = item.ValueKind == JsonValueKind.Object
                            ? item.EnumerateObject().Count()
                            : item.GetArrayLength();
                        var summary = item.ValueKind == JsonValueKind.Object ? $"{{{count}}}" : $"[{count}]";
                        var node = new TreeNode($"\u25B6 [{idx}]: {summary}")
                        {
                            Tag = info,
                            NodeFont = displayFont
                        };
                        parent.Add(node);
                        BuildTree(item, node.Nodes, $"{pathPrefix}[{idx}]", displayFont);
                    }
                    else
                    {
                        info.RawValue = GetRawValue(item);
                        var node = new TreeNode($" [{idx}]: {FormatValue(item)}")
                        {
                            Tag = info,
                            NodeFont = displayFont
                        };
                        parent.Add(node);
                    }

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

    /// <summary>
    /// 将 JsonElement 的值格式化为带引号的显示文本（字符串加引号，数字/布尔/null原样显示）。
    /// </summary>
    /// <param name="element">JSON 元素。</param>
    /// <returns>格式化后的显示字符串。</returns>
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
    /// 获取 JsonElement 的原始值字符串，用于复制到剪贴板。null 类型返回 null。
    /// </summary>
    /// <param name="element">JSON 元素。</param>
    /// <returns>原始值字符串，null 类型返回 null。</returns>
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
    /// 递归展开/折叠节点到指定层级。层级内展开，层级外折叠。
    /// </summary>
    /// <param name="nodes">当前层级的节点集合。</param>
    /// <param name="targetLevel">目标展开层级。</param>
    /// <param name="currentLevel">当前递归层级。</param>
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
    /// 递归搜索节点集合，将文本包含关键字的节点加入结果集（忽略大小写）。
    /// </summary>
    /// <param name="nodes">要搜索的节点集合。</param>
    /// <param name="keyword">搜索关键字。</param>
    /// <param name="result">匹配节点结果集。</param>
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
    /// 从指定节点向上逐级展开父节点，直到根节点，确保该节点可见。
    /// </summary>
    /// <param name="node">需要展开到可见的目标节点。</param>
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