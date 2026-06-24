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

    private readonly bool _isDesignMode;
    private readonly HashSet<TreeNode> _highlightedNodes = new();
    private TreeView? _treeView;
    private Control? _designPreview;
    private Font _displayFont;
    private TreeViewDrawMode _drawMode = TreeViewDrawMode.OwnerDrawText;
    private bool _hideSelection;
    private int _itemHeight = 20;
    private bool _showLines;
    private bool _showRootLines;
    private string? _rawText;
    private bool _isJson;
    private string? _searchKeyword;

    public JsonTreeView()
    {
        _isDesignMode = IsDesignTimeMode();
        _displayFont = new Font("Consolas", 11f);
        AutoScaleMode = AutoScaleMode.None;
        base.Font = _displayFont;

        if (_isDesignMode)
        {
            InitializeDesignPreview();
        }
        else
        {
            InitializeRuntimeTreeView();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? SelectedNode => _treeView?.SelectedNode;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ContextMenuStrip? ContextMenuStrip
    {
        get => _treeView?.ContextMenuStrip;
        set
        {
            if (_treeView != null)
            {
                _treeView.ContextMenuStrip = value;
            }
        }
    }

    [DefaultValue(TreeViewDrawMode.OwnerDrawText)]
    public TreeViewDrawMode DrawMode
    {
        get => _treeView?.DrawMode ?? _drawMode;
        set
        {
            _drawMode = value;
            if (_treeView != null)
            {
                _treeView.DrawMode = value;
            }
        }
    }

    [DefaultValue(false)]
    public bool HideSelection
    {
        get => _treeView?.HideSelection ?? _hideSelection;
        set
        {
            _hideSelection = value;
            if (_treeView != null)
            {
                _treeView.HideSelection = value;
            }
        }
    }

    [DefaultValue(20)]
    public int ItemHeight
    {
        get => _treeView?.ItemHeight ?? _itemHeight;
        set
        {
            _itemHeight = value;
            if (_treeView != null)
            {
                _treeView.ItemHeight = value;
            }
        }
    }

    [DefaultValue(false)]
    public bool ShowLines
    {
        get => _treeView?.ShowLines ?? _showLines;
        set
        {
            _showLines = value;
            if (_treeView != null)
            {
                _treeView.ShowLines = value;
            }
        }
    }

    [DefaultValue(false)]
    public bool ShowRootLines
    {
        get => _treeView?.ShowRootLines ?? _showRootLines;
        set
        {
            _showRootLines = value;
            if (_treeView != null)
            {
                _treeView.ShowRootLines = value;
            }
        }
    }

    public void SetFont(Font font)
    {
        _displayFont = font;
        base.Font = font;
        if (_treeView != null)
        {
            _treeView.Font = font;
            _treeView.ItemHeight = (int)(font.Height * 1.3);
            _treeView.Invalidate();
        }

        if (_designPreview != null)
        {
            _designPreview.Font = font;
        }
    }

    public void DisplayJson(string rawJson)
    {
        if (_treeView == null)
        {
            return;
        }

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
        if (_treeView == null)
        {
            return;
        }

        _rawText = text;
        _isJson = false;
        _highlightedNodes.Clear();
        _searchKeyword = null;
        _treeView.LoadPlainText(text, _displayFont);
    }

    public void ExpandAll()
    {
        _treeView?.ExpandAll();
    }

    public void CollapseAll()
    {
        _treeView?.CollapseAll();
    }

    public void CollapseToLevel(int level)
    {
        _treeView?.CollapseToLevelStatic(level);
    }

    public void SearchAndHighlight(string keyword)
    {
        if (_treeView == null)
        {
            return;
        }

        _searchKeyword = keyword;
        _treeView.SearchAndHighlight(_highlightedNodes, keyword);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (Font != null)
        {
            SetFont(Font);
        }
    }

    private void InitializeDesignPreview()
    {
        var preview = new Label
        {
            Dock = DockStyle.Fill,
            Text = "{ sample: json }\r\n  \"request\": { ... }\r\n  \"response\": [ ... ]",
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(8),
            BackColor = SystemColors.Window,
            ForeColor = SystemColors.ControlText,
            Font = _displayFont
        };

        _designPreview = preview;
        Controls.Add(preview);
        BackColor = SystemColors.Window;
    }

    private void InitializeRuntimeTreeView()
    {
        var treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowNodeToolTips = false,
            ShowPlusMinus = true,
            FullRowSelect = false,
            Font = _displayFont
        };

        _treeView = treeView;
        Controls.Add(treeView);
        treeView.ShowLines = _showLines;
        treeView.ShowRootLines = _showRootLines;
        treeView.HideSelection = _hideSelection;
        treeView.ItemHeight = _itemHeight;
        treeView.DrawMode = _drawMode;
        treeView.DrawNode += OnTreeViewDrawNode;
        treeView.ContextMenuStrip = CreateContextMenu();
    }

    private void OnTreeViewDrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null)
        {
            return;
        }

        var bounds = e.Bounds;
        if (bounds.IsEmpty || _treeView == null)
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
            if (_treeView?.SelectedNode?.Tag is JsonPathInfo info)
            {
                ClipboardTextHelper.TrySetText(info.RawValue);
            }
        };

        var copyPath = new ToolStripMenuItem("Copy JSONPath");
        copyPath.Click += (s, e) =>
        {
            if (_treeView?.SelectedNode != null)
            {
                ClipboardTextHelper.TrySetText(JsonFormatter.GetJsonPath(_treeView.SelectedNode));
            }
        };

        var copyNode = new ToolStripMenuItem("Copy Node JSON");
        copyNode.Click += (s, e) =>
        {
            if (_treeView?.SelectedNode != null)
            {
                ClipboardTextHelper.TrySetText(_treeView.SelectedNode.Text);
            }
        };

        var sep1 = new ToolStripSeparator();
        var expandAll = new ToolStripMenuItem("Expand All");
        expandAll.Click += (s, e) => _treeView?.ExpandAll();

        var collapseAll = new ToolStripMenuItem("Collapse All");
        collapseAll.Click += (s, e) => _treeView?.CollapseAll();

        var collapseTo2 = new ToolStripMenuItem("Collapse to Level 2");
        collapseTo2.Click += (s, e) => CollapseToLevel(2);

        menu.Items.AddRange(new ToolStripItem[] { copyValue, copyPath, copyNode, sep1, expandAll, collapseAll, collapseTo2 });
        return menu;
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
