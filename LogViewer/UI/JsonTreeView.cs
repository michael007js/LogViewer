using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

[ToolboxItem(true)]
public class JsonTreeView : UserControl
{
    private static readonly Color ColorKey = Color.FromArgb(0x56, 0x9C, 0xD6);
    private static readonly Color ColorString = Color.FromArgb(0xCE, 0x91, 0x78);
    private static readonly Color ColorNumber = Color.FromArgb(0xB5, 0xCE, 0xA8);
    private static readonly Color ColorBool = Color.FromArgb(0x56, 0x9C, 0xD6);
    private static readonly Color ColorNull = Color.Gray;
    private static readonly Color ColorSummary = Color.FromArgb(0x80, 0x80, 0x80);
    private static readonly Color ColorHighlight = Color.Yellow;

    private readonly TreeView _treeView;
    private readonly bool _isDesignMode;
    private bool _syncingFont;
    private string? _rawText;
    private bool _isJson;
    private string? _searchKeyword;
    internal readonly HashSet<TreeNode> _highlightedNodes = new();

    private Font _displayFont;
    private Font _italicFont;

    public JsonTreeView()
    {
        _isDesignMode = IsDesignTimeMode();
        _treeView = new TreeView();
        _displayFont = new Font("Consolas", 11f);
        _italicFont = new Font("Consolas", 11f, FontStyle.Italic);

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        Controls.Add(_treeView);
        _treeView.Dock = DockStyle.Fill;
        _treeView.BorderStyle = BorderStyle.None;
        _treeView.ShowNodeToolTips = false;
        _treeView.ShowLines = false;
        _treeView.ShowPlusMinus = true;
        _treeView.ShowRootLines = false;
        _treeView.FullRowSelect = false;
        _treeView.HideSelection = false;
        _treeView.ItemHeight = 20;
        _treeView.Font = _displayFont;
        _treeView.DrawMode = _isDesignMode ? TreeViewDrawMode.Normal : TreeViewDrawMode.OwnerDrawText;
        _treeView.DrawNode += OnTreeViewDrawNode;
        Font = _displayFont;
        ResumeLayout(false);

        if (_isDesignMode)
        {
            InitializeDesignTimePreview();
        }
        else
        {
            _treeView.ContextMenuStrip = CreateContextMenu();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? SelectedNode => _treeView.SelectedNode;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ContextMenuStrip? ContextMenuStrip
    {
        get => _treeView.ContextMenuStrip;
        set => _treeView.ContextMenuStrip = value;
    }

    [DefaultValue(TreeViewDrawMode.OwnerDrawText)]
    public TreeViewDrawMode DrawMode
    {
        get => _treeView.DrawMode;
        set => _treeView.DrawMode = value;
    }

    [DefaultValue(false)]
    public bool HideSelection
    {
        get => _treeView.HideSelection;
        set => _treeView.HideSelection = value;
    }

    [DefaultValue(20)]
    public int ItemHeight
    {
        get => _treeView.ItemHeight;
        set => _treeView.ItemHeight = value;
    }

    [DefaultValue(false)]
    public bool ShowLines
    {
        get => _treeView.ShowLines;
        set => _treeView.ShowLines = value;
    }

    [DefaultValue(false)]
    public bool ShowRootLines
    {
        get => _treeView.ShowRootLines;
        set => _treeView.ShowRootLines = value;
    }

    public void SetFont(Font font)
    {
        _displayFont?.Dispose();
        _italicFont?.Dispose();
        _displayFont = new Font(font.FontFamily, font.Size);
        _italicFont = new Font(font.FontFamily, font.Size, FontStyle.Italic);
        _syncingFont = true;
        try
        {
            base.Font = _displayFont;
            _treeView.Font = _displayFont;
        }
        finally
        {
            _syncingFont = false;
        }

        _treeView.ItemHeight = (int)(_displayFont.Height * 1.3);
        _treeView.Invalidate();
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
            _treeView.LoadJson(rawJson, _displayFont);
        }
        else
        {
            _treeView.LoadPlainText(rawJson, _displayFont);
        }
    }

    public void DisplayPlainText(string text)
    {
        _rawText = text;
        _isJson = false;
        _highlightedNodes.Clear();
        _searchKeyword = null;
        _treeView.LoadPlainText(text, _displayFont);
    }

    public void ExpandAll()
    {
        _treeView.ExpandAll();
    }

    public void CollapseAll()
    {
        _treeView.CollapseAll();
    }

    public void CollapseToLevel(int level)
    {
        _treeView.CollapseToLevelStatic(level);
    }

    public void SearchAndHighlight(string keyword)
    {
        _searchKeyword = keyword;
        _treeView.SearchAndHighlight(_highlightedNodes, keyword);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (!_syncingFont && Font != null)
        {
            SetFont(Font);
        }
    }

    private void OnTreeViewDrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (_isDesignMode)
        {
            e.DrawDefault = true;
            return;
        }

        if (e.Node == null)
        {
            return;
        }

        var bounds = e.Bounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        var drawBounds = new Rectangle(
            bounds.X,
            bounds.Y,
            Math.Max(0, _treeView.ClientSize.Width - bounds.X),
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
            text = text[1..];
        }
        else if (text.StartsWith(" "))
        {
            x += MeasureTextWidth("\u25B6", _displayFont);
            text = text[1..];
        }

        if (info.Key != null && (info.ValueKind == JsonValueKind.Object || info.ValueKind == JsonValueKind.Array))
        {
            var keyPart = $"\"{info.Key}\": ";
            x += DrawTextSegment(g, keyPart, _displayFont, ColorKey, x, y, bounds);

            var summary = isNodeExpanded ? (text.Contains("{") ? "{" : "[") : text[(text.IndexOf(":") + 2)..];
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

            var valueText = text[(text.IndexOf(":") + 2)..];
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
            if (_treeView.SelectedNode?.Tag is JsonPathInfo info)
            {
                ClipboardTextHelper.TrySetText(info.RawValue);
            }
        };

        var copyPath = new ToolStripMenuItem("Copy JSONPath");
        copyPath.Click += (s, e) =>
        {
            if (_treeView.SelectedNode != null)
            {
                ClipboardTextHelper.TrySetText(JsonFormatter.GetJsonPath(_treeView.SelectedNode));
            }
        };

        var copyNode = new ToolStripMenuItem("Copy Node JSON");
        copyNode.Click += (s, e) =>
        {
            if (_treeView.SelectedNode != null)
            {
                ClipboardTextHelper.TrySetText(_treeView.SelectedNode.Text);
            }
        };

        var sep1 = new ToolStripSeparator();
        var expandAll = new ToolStripMenuItem("Expand All");
        expandAll.Click += (s, e) => _treeView.ExpandAll();

        var collapseAll = new ToolStripMenuItem("Collapse All");
        collapseAll.Click += (s, e) => _treeView.CollapseAll();

        var collapseTo2 = new ToolStripMenuItem("Collapse to Level 2");
        collapseTo2.Click += (s, e) => CollapseToLevel(2);

        menu.Items.AddRange(new ToolStripItem[] { copyValue, copyPath, copyNode, sep1, expandAll, collapseAll, collapseTo2 });
        return menu;
    }

    private void InitializeDesignTimePreview()
    {
        if (_treeView.Nodes.Count > 0)
        {
            return;
        }

        var root = _treeView.Nodes.Add("{ sample: json }");
        root.Nodes.Add("\"request\": { ... }");
        root.Nodes.Add("\"response\": [ ... ]");
        root.Expand();
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
