using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

public static class JsonTreeViewLoader
{
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

    public static void LoadPlainText(this TreeView treeView, string text, Font displayFont)
    {
        treeView.BeginUpdate();
        treeView.Nodes.Clear();

        if (!string.IsNullOrEmpty(text))
        {
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var node = new TreeNode(lines[i]) { Tag = new JsonPathInfo { RawValue = lines[i] }, NodeFont = displayFont };
                treeView.Nodes.Add(node);
            }
        }

        treeView.EndUpdate();
    }

    public static void CollapseToLevel(this TreeView treeView, int level)
    {
        treeView.BeginUpdate();
        treeView.CollapseAll();
        ExpandToLevel(treeView.Nodes, level, 0);
        treeView.EndUpdate();
    }

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

    private static void SearchNodes(TreeNodeCollection nodes, string keyword, HashSet<TreeNode> result)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                result.Add(node);
            SearchNodes(node.Nodes, keyword, result);
        }
    }

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
