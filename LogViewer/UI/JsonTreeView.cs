using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// 自定义 UserControl，封装 TreeView 以提供 JSON 折叠显示、语法高亮着色、
/// JSONPath 复制、键盘搜索等功能。使用 OwnerDrawText 模式自绘节点。
/// </summary>
[ToolboxItem(true)]
public class JsonTreeView : UserControl
{
    // JSON 语法高亮颜色常量
    private static readonly Color ColorKey = Color.FromArgb(0x56, 0x9C, 0xD6); // JSON 键名颜色
    private static readonly Color ColorString = Color.FromArgb(0xCE, 0x91, 0x78); // 字符串值颜色
    private static readonly Color ColorNumber = Color.FromArgb(0xB5, 0xCE, 0xA8); // 数字值颜色
    private static readonly Color ColorBool = Color.FromArgb(0x56, 0x9C, 0xD6); // 布尔值颜色
    private static readonly Color ColorNull = Color.Gray; // null 值颜色
    private static readonly Color ColorSummary = Color.FromArgb(0x80, 0x80, 0x80); // 对象/数组摘要颜色
    private static readonly Color ColorHighlight = Color.Yellow; // 搜索高亮背景色

    /// <summary>是否处于设计器模式。</summary>
    private readonly bool _isDesignMode;

    /// <summary>搜索高亮节点集合。</summary>
    private readonly HashSet<TreeNode> _highlightedNodes = new();

    /// <summary>当前 JSON 文档引用，保持存活以使 JsonElement 引用有效，切换/关闭时释放。</summary>
    private JsonDocument? _jsonDoc;

    /// <summary>运行时内部的 TreeView 控件。</summary>
    private TreeView? _treeView;

    /// <summary>设计时预览标签控件。</summary>
    private Control? _designPreview;

    /// <summary>节点显示字体。</summary>
    private Font _displayFont;

    /// <summary>自绘模式缓存值。</summary>
    private TreeViewDrawMode _drawMode = TreeViewDrawMode.OwnerDrawText;

    /// <summary>隐藏选中状态缓存值。</summary>
    private bool _hideSelection;

    /// <summary>节点行高缓存值。</summary>
    private int _itemHeight = 20;

    /// <summary>显示连线缓存值。</summary>
    private bool _showLines;

    /// <summary>显示根连线缓存值。</summary>
    private bool _showRootLines;

    /// <summary>原始文本内容缓存。</summary>
    private string? _rawText;

    /// <summary>是否为有效 JSON。</summary>
    private bool _isJson;

    /// <summary>当前搜索关键字缓存。</summary>
    private string? _searchKeyword;

    /// <summary>
    /// 初始化 JsonTreeView，设计器模式下创建预览标签，运行时模式下创建内部 TreeView。
    /// </summary>
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

    /// <summary>获取当前选中的树节点。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? SelectedNode => _treeView?.SelectedNode;

    /// <summary>
    /// 获取或设置内部 TreeView 的上下文菜单，对设计器隐藏序列化。
    /// </summary>
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

    /// <summary>
    /// 获取或设置 TreeView 的绘制模式，默认为 OwnerDrawText 以支持语法高亮。
    /// </summary>
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

    /// <summary>
    /// 获取或设置是否在失去焦点时隐藏选中状态。
    /// </summary>
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

    /// <summary>
    /// 获取或设置每个节点行的像素高度。
    /// </summary>
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

    /// <summary>
    /// 获取或设置是否在节点之间显示连线。
    /// </summary>
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

    /// <summary>
    /// 获取或设置是否在根节点之间显示连线。
    /// </summary>
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

    /// <summary>
    /// 设置显示字体，同步更新内部 TreeView 和设计预览的字体及行高。
    /// </summary>
    /// <param name="font">新的显示字体。</param>
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

    /// <summary>
    /// 显示 JSON 内容。仅解析一次 JsonDocument，懒加载构建树视图。解析失败则降级为纯文本。
    /// </summary>
    /// <param name="rawJson">原始 JSON 字符串。</param>
    public void DisplayJson(string rawJson)
    {
        if (_treeView == null)
        {
            return;
        }

        _rawText = rawJson;
        _highlightedNodes.Clear();
        _searchKeyword = null;
        _jsonDoc?.Dispose();
        _jsonDoc = null;

        try
        {
            _jsonDoc = JsonDocument.Parse(rawJson);
            _isJson = true;
            _treeView.LoadJson(_jsonDoc.RootElement, _displayFont);
        }
        catch
        {
            _isJson = false;
            _treeView.LoadPlainText(rawJson, _displayFont);
        }
    }

    /// <summary>
    /// 以纯文本模式显示内容，每行作为一个节点，不做语法高亮。
    /// </summary>
    /// <param name="text">纯文本内容。</param>
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
        _jsonDoc?.Dispose();
        _jsonDoc = null;
        _treeView.LoadPlainText(text, _displayFont);
    }

    /// <summary>展开所有树节点。</summary>
    public void ExpandAll()
    {
        _treeView?.ExpandAll();
    }

    /// <summary>折叠所有树节点。</summary>
    public void CollapseAll()
    {
        _treeView?.CollapseAll();
    }

    /// <summary>
    /// 折叠所有节点后展开到指定层级。
    /// </summary>
    /// <param name="level">目标展开层级。</param>
    public void CollapseToLevel(int level)
    {
        _treeView?.CollapseToLevelStatic(level);
    }

    /// <summary>
    /// 搜索并高亮包含关键字的节点，忽略大小写。空关键字时清除所有高亮。
    /// </summary>
    /// <param name="keyword">搜索关键字。</param>
    public void SearchAndHighlight(string keyword)
    {
        if (_treeView == null)
        {
            return;
        }

        _searchKeyword = keyword;
        _treeView.SearchAndHighlight(_highlightedNodes, keyword);
    }

    /// <summary>
    /// 响应字体变更事件，同步更新内部控件字体。
    /// </summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (Font != null)
        {
            SetFont(Font);
        }
    }

    /// <summary>
    /// 创建设计时预览标签，模拟 JSON 树的外观。
    /// </summary>
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

    /// <summary>
    /// 创建运行时内部的 TreeView 控件，配置自绘模式和上下文菜单。
    /// </summary>
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
        treeView.BeforeExpand += OnBeforeExpand;
        treeView.ContextMenuStrip = CreateContextMenu();
    }

    /// <summary>
    /// TreeView 的 BeforeExpand 事件处理器，懒加载：移除哨兵子节点，从 JsonElement 构建真实子节点。
    /// </summary>
    private void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        JsonTreeViewLoader.OnBeforeExpand(e.Node, _displayFont);
    }

    /// <summary>
    /// TreeView 的 DrawNode 事件处理器，实现语法高亮着色和搜索高亮背景绘制。
    /// </summary>
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
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine);
        }
    }

    /// <summary>
    /// 对单个节点绘制语法高亮着色文本，区分键名、值类型（字符串/数字/布尔/null）、对象/数组摘要。
    /// </summary>
    /// <param name="g">绘图 Graphics 对象。</param>
    /// <param name="text">节点显示文本。</param>
    /// <param name="info">节点的 JsonPathInfo 元数据。</param>
    /// <param name="bounds">绘制区域矩形。</param>
    /// <param name="isNodeExpanded">节点是否处于展开状态。</param>
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

    /// <summary>
    /// 在指定位置绘制一段着色文本，返回文本像素宽度。
    /// </summary>
    /// <param name="g">绘图 Graphics 对象。</param>
    /// <param name="text">要绘制的文本段。</param>
    /// <param name="font">绘制字体。</param>
    /// <param name="color">绘制颜色。</param>
    /// <param name="x">起始 X 坐标。</param>
    /// <param name="y">起始 Y 坐标。</param>
    /// <param name="bounds">绘制区域矩形。</param>
    /// <returns>文本段的像素宽度。</returns>
    private static int DrawTextSegment(Graphics g, string text, Font font, Color color, float x, float y,
        Rectangle bounds)
    {
        int left = (int)MathF.Round(x);
        int width = Math.Max(0, bounds.Right - left);
        if (width <= 0)
        {
            return 0;
        }

        var rect = new Rectangle(left, bounds.Y, width, bounds.Height);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, text, font, rect, color, flags);
        return MeasureTextWidth(text, font);
    }

    /// <summary>
    /// 测量文本的像素宽度，用于自绘对齐计算。
    /// </summary>
    /// <param name="text">要测量的文本。</param>
    /// <param name="font">测量字体。</param>
    /// <returns>文本的像素宽度。</returns>
    private static int MeasureTextWidth(string text, Font font)
    {
        return TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding)
            .Width;
    }

    /// <summary>
    /// 创建右键上下文菜单，包含复制值、复制 JSONPath、复制节点 JSON、展开/折叠等操作。
    /// </summary>
    /// <returns>构建完成的 ContextMenuStrip。</returns>
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

        menu.Items.AddRange(new ToolStripItem[]
            { copyValue, copyPath, copyNode, sep1, expandAll, collapseAll, collapseTo2 });
        return menu;
    }

    /// <summary>释放 JsonDocument 和自定义字体资源。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _jsonDoc?.Dispose();
            _displayFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 判断当前是否处于设计器模式，通过 LicenseManager 和进程名/命令行检测 IDE 环境。
    /// </summary>
    /// <returns>是否处于设计器模式。</returns>
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
               commandLine.Contains("JetBrains.ReSharper.Features.WinForms.Designer.External.Core",
                   StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("WinFormsDesigner", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// TreeView 的文件级静态扩展方法类，提供折叠到指定层级的功能。
/// </summary>
file static class TreeViewExtensions
{
    /// <summary>
    /// 折叠 TreeView 所有节点后展开到指定层级。
    /// </summary>
    /// <param name="treeView">目标 TreeView 控件。</param>
    /// <param name="level">目标展开层级。</param>
    public static void CollapseToLevelStatic(this TreeView treeView, int level)
    {
        treeView.BeginUpdate();
        treeView.CollapseAll();
        ExpandToLevel(treeView.Nodes, level, 0);
        treeView.EndUpdate();
    }

    /// <summary>
    /// 递归展开/折叠节点到指定层级。
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
}