using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

[ToolboxItem(true)]
public class JsonTreeView : TreeView
{
    private const int TvsNotooltips = 0x80;
    private static readonly Color ColorKey = Color.FromArgb(0x56, 0x9C, 0xD6);
    private static readonly Color ColorString = Color.FromArgb(0xCE, 0x91, 0x78);
    private static readonly Color ColorNumber = Color.FromArgb(0xB5, 0xCE, 0xA8);
    private static readonly Color ColorBool = Color.FromArgb(0x56, 0x9C, 0xD6);
    private static readonly Color ColorNull = Color.Gray;
    private static readonly Color ColorSummary = Color.FromArgb(0x80, 0x80, 0x80);
    private static readonly Color ColorHighlight = Color.Yellow;

    private string? _rawText;
    private bool _isJson;
    private string? _searchKeyword;
    internal readonly HashSet<TreeNode> _highlightedNodes = new();

    private Font _displayFont;
    private Font _italicFont;

    public JsonTreeView()
    {
        base.DrawMode = TreeViewDrawMode.OwnerDrawText;
        ShowNodeToolTips = false;
        ShowLines = false;
        ShowPlusMinus = true;
        ShowRootLines = false;
        FullRowSelect = false;
        HideSelection = false;
        ItemHeight = 20;

        _displayFont = new Font("Consolas", 11f);
        _italicFont = new Font("Consolas", 11f, FontStyle.Italic);
        Font = _displayFont;

        if (!IsDesignTimeMode())
        {
            ContextMenuStrip = CreateContextMenu();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new TreeViewDrawMode DrawMode
    {
        get => base.DrawMode;
        set => base.DrawMode = value;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= TvsNotooltips;
            return cp;
        }
    }

    public void SetFont(Font font)
    {
        _displayFont = new Font(font.FontFamily, font.Size);
        _italicFont = new Font(font.FontFamily, font.Size, FontStyle.Italic);
        Font = _displayFont;
        ItemHeight = (int)(font.Height * 1.3);
        Invalidate();
    }

    public void DisplayJson(string rawJson)
    {
        _rawText = rawJson;
        _highlightedNodes.Clear();
        _searchKeyword = null;
        var doc = JsonFormatter.ParseJson(JsonFormatter.FormatJson(rawJson) ?? rawJson);
        _isJson = doc != null;
        if (_isJson)
        {
            this.LoadJson(rawJson, _displayFont);
        }
        else
        {
            this.LoadPlainText(rawJson, _displayFont);
        }
    }

    public void DisplayPlainText(string text)
    {
        _rawText = text;
        _isJson = false;
        _highlightedNodes.Clear();
        _searchKeyword = null;
        this.LoadPlainText(text, _displayFont);
    }

    public void CollapseToLevel(int level)
    {
        this.CollapseToLevelStatic(level);
    }

    public void SearchAndHighlight(string keyword)
    {
        _searchKeyword = keyword;
        this.SearchAndHighlight(_highlightedNodes, keyword);
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (DesignMode)
        {
            e.DrawDefault = true;
            return;
        }

        if (e.Node == null) return;

        var bounds = e.Bounds;
        if (bounds.IsEmpty) return;

        var drawBounds = new Rectangle(
            bounds.X,
            bounds.Y,
            Math.Max(0, ClientSize.Width - bounds.X),
            bounds.Height);

        var g = e.Graphics;
        var isHighlighted = _highlightedNodes.Contains(e.Node);
        var text = e.Node.Text;

        if (isHighlighted)
        {
            using var hlBrush = new SolidBrush(ColorHighlight);
            g.FillRectangle(hlBrush, drawBounds.X - 2, drawBounds.Y, drawBounds.Width + 4, drawBounds.Height);
        }

        if (e.Node.Tag is JsonPathInfo info)
        {
            DrawColoredText(g, text, info, drawBounds, e.Node.IsExpanded);
        }
        else
        {
            TextRenderer.DrawText(g, text, _displayFont, drawBounds, ForeColor,
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    private void DrawColoredText(Graphics g, string text, JsonPathInfo info, Rectangle bounds, bool isNodeExpanded)
    {
        float x = bounds.X;
        float y = bounds.Y;

        if (text.StartsWith("\u25B6"))
        {
            var arrow = isNodeExpanded ? "\u25BC" : "\u25B6";
            x += DrawTextSegment(g, arrow, _displayFont, SystemColors.ControlText, x, y, bounds);
            text = text.Substring(1);
        }
        else if (text.StartsWith(" "))
        {
            x += MeasureTextWidth("\u25B6", _displayFont);
            text = text.Substring(1);
        }

        if (info.Key != null && (info.ValueKind == JsonValueKind.Object || info.ValueKind == JsonValueKind.Array))
        {
            var keyPart = $"\"{info.Key}\": ";
            x += DrawTextSegment(g, keyPart, _displayFont, ColorKey, x, y, bounds);

            var summary = isNodeExpanded ? (text.Contains("{") ? "{" : "[") : text.Substring(text.IndexOf(":") + 2);
            DrawTextSegment(g, summary, _displayFont, ColorSummary, x, y, bounds);
        }
        else if (info.Key != null)
        {
            var keyPart = $"\"{info.Key}\": ";
            x += DrawTextSegment(g, keyPart, _displayFont, ColorKey, x, y, bounds);

            var valueColor = info.ValueKind switch
            {
                JsonValueKind.String => ColorString,
                JsonValueKind.Number => ColorNumber,
                JsonValueKind.True or JsonValueKind.False => ColorBool,
                JsonValueKind.Null => ColorNull,
                _ => SystemColors.ControlText
            };

            var valueText = text.Substring(text.IndexOf(":") + 2);
            DrawTextSegment(g, valueText, _displayFont, valueColor, x, y, bounds);
        }
        else
        {
            var valueColor = info.ValueKind switch
            {
                JsonValueKind.String => ColorString,
                JsonValueKind.Number => ColorNumber,
                JsonValueKind.True or JsonValueKind.False => ColorBool,
                JsonValueKind.Null => ColorNull,
                _ => SystemColors.ControlText
            };
            DrawTextSegment(g, text, _displayFont, valueColor, x, y, bounds);
        }
    }

    private static int DrawTextSegment(Graphics g, string text, Font font, Color color, float x, float y, Rectangle bounds)
    {
        int left = (int)MathF.Round(x);
        int width = Math.Max(0, bounds.Right - left);
        if (width <= 0)
        {
            return 0;
        }

        var rect = new Rectangle(left, bounds.Y, width, bounds.Height);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

        TextRenderer.DrawText(g, text, font, rect, color, flags);
        return MeasureTextWidth(text, font);
    }

    private static int MeasureTextWidth(string text, Font font)
    {
        return TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var copyValue = new ToolStripMenuItem("Copy Value");
        copyValue.Click += (s, e) =>
        {
            if (SelectedNode?.Tag is JsonPathInfo info)
                ClipboardTextHelper.TrySetText(info.RawValue);
        };

        var copyPath = new ToolStripMenuItem("Copy JSONPath");
        copyPath.Click += (s, e) =>
        {
            if (SelectedNode != null)
                ClipboardTextHelper.TrySetText(JsonFormatter.GetJsonPath(SelectedNode));
        };

        var copyNode = new ToolStripMenuItem("Copy Node JSON");
        copyNode.Click += (s, e) =>
        {
            if (SelectedNode != null)
                ClipboardTextHelper.TrySetText(SelectedNode.Text);
        };

        var sep1 = new ToolStripSeparator();
        var expandAll = new ToolStripMenuItem("Expand All");
        expandAll.Click += (s, e) => ExpandAll();

        var collapseAll = new ToolStripMenuItem("Collapse All");
        collapseAll.Click += (s, e) => CollapseAll();

        var collapseTo2 = new ToolStripMenuItem("Collapse to Level 2");
        collapseTo2.Click += (s, e) => CollapseToLevel(2);

        menu.Items.AddRange(new ToolStripItem[] { copyValue, copyPath, copyNode, sep1, expandAll, collapseAll, collapseTo2 });
        return menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _displayFont?.Dispose();
            _italicFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static bool IsDesignTimeMode()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return true;
        }

        var processName = Process.GetCurrentProcess().ProcessName;
        var commandLine = Environment.CommandLine;
        return processName.Contains("devenv", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("rider", StringComparison.OrdinalIgnoreCase) ||
               processName.Contains("jetbrains", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("JetBrains.ReSharper.Features.WinForms.Designer.External.Core", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("WinFormsDesigner", StringComparison.OrdinalIgnoreCase);
    }
}

file static class TreeViewExtensions
{
    public static void CollapseToLevelStatic(this TreeView treeView, int level)
    {
        treeView.BeginUpdate();
        treeView.CollapseAll();
        ExpandToLevel(treeView.Nodes, level, 0);
        treeView.EndUpdate();
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
}
