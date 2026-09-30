using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogViewer.Static;
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

    /// <summary>正在加载分页内容的节点集合，防止重复展开。</summary>
    private readonly HashSet<TreeNode> _loadingNodes = new();

    /// <summary>当前加载任务的取消源，切换内容时取消旧任务。</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>当前加载版本，防止旧解析结果覆盖新内容。</summary>
    private int _loadVersion;

    /// <summary>当前完整 JSON 根快照，供折叠态后台搜索使用。</summary>
    private JsonElement? _rootElement;

    private CancellationTokenSource? _searchCts;
    private JsonTreeViewLoader.JsonSearchSession? _searchSession;
    private int _searchVersion;
    private int _searchLoadVersion;
    private int _plainSearchOffset;
    private bool _searchInProgress;
    private string? _pendingSearchKeyword;

    /// <summary>加载开始时捕获的 UI 同步上下文，句柄重建期间用于可靠回写。</summary>
    private SynchronizationContext? _uiContext;

    /// <summary>运行时内部的 TreeView 控件。</summary>
    private TreeView? _treeView;

    /// <summary>设计时预览标签控件。</summary>
    private Control? _designPreview;

    /// <summary>节点显示字体。</summary>
    private Font _displayFont;

    private bool _settingFont;

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

    /// <summary>图片 URL 正则，null 表示禁用图片预览。</summary>
    private Regex? _imageUrlRegex;

    /// <summary>当前悬浮的节点，用于检测节点切换。</summary>
    private TreeNode? _hoveredNode;

    /// <summary>悬浮延迟计时器，400ms 后触发图片预览。</summary>
    private System.Windows.Forms.Timer? _hoverTimer;

    /// <summary>当前显示的图片预览弹窗。</summary>
    private ImagePreviewPopup? _imagePopup;

    /// <summary>
    /// 初始化 JsonTreeView，设计器模式下创建预览标签，运行时模式下创建内部 TreeView。
    /// </summary>
    public JsonTreeView()
    {
        _isDesignMode = IsDesignTimeMode();
        _displayFont = new Font("Consolas", 11f);
        AutoScaleMode = AutoScaleMode.None;
        _settingFont = true;
        base.Font = _displayFont;
        _settingFont = false;

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
        if (_settingFont || _displayFont.Equals(font)) return;
        _settingFont = true;
        var replacement = (Font)font.Clone();
        var previous = _displayFont;
        try
        {
            _displayFont = replacement;
            base.Font = replacement;
            if (_treeView != null)
            {
                _treeView.Font = replacement;
                _treeView.ItemHeight = (int)(replacement.Height * 1.3);
                _treeView.Invalidate();
            }

            if (_designPreview != null) _designPreview.Font = replacement;
        }
        finally
        {
            _settingFont = false;
            if (!ReferenceEquals(previous, replacement)) previous.Dispose();
        }
    }

    /// <summary>
    /// 显示 JSON 内容。解析和节点投影均在后台执行，UI 仅分批挂载节点。
    /// </summary>
    /// <param name="rawJson">原始 JSON 字符串。</param>
    public void DisplayJson(string rawJson)
    {
        if (_treeView == null) return;
        int version = BeginLoad(rawJson, true);
        _ = LoadJsonAsync(rawJson, version, _loadCts!.Token);
    }

    /// <summary>
    /// 以纯文本模式显示内容，长内容按固定分段分页加载。
    /// </summary>
    /// <param name="text">纯文本内容。</param>
    public void DisplayPlainText(string text)
    {
        if (_treeView == null) return;
        int version = BeginLoad(text, false);
        _ = LoadTextAsync(text, version, _loadCts!.Token);
    }

    private int BeginLoad(string rawText, bool isJson)
    {
        _uiContext = SynchronizationContext.Current ?? _uiContext;
        if (!_treeView!.IsHandleCreated) _ = _treeView.Handle;
        ResetSearch(false);
        _rootElement = null;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        _loadVersion++;
        _rawText = rawText;
        _isJson = isJson;
        _highlightedNodes.Clear();
        _loadingNodes.Clear();
        _hoverTimer?.Stop();
        _hoveredNode = null;
        HideImagePopup();
        _searchKeyword = null;

        _treeView!.BeginUpdate();
        try
        {
            _treeView.Nodes.Clear();
            _treeView.Nodes.Add(new TreeNode(Language.JsonParsing));
        }
        finally
        {
            _treeView.EndUpdate();
        }

        return _loadVersion;
    }

    private async Task LoadJsonAsync(string rawJson, int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(60, token);
            var regex = _imageUrlRegex;
            var result = await JsonFormatter.RunBackgroundAsync(() =>
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement.Clone();
                var page = JsonTreeViewLoader.CreateJsonPage(root, regex, token);
                return (Root: root, Page: page);
            }, token);

            TreeNodeCollection? target = null;
            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentLoad(version, token)) return;
                _isJson = true;
                _rootElement = result.Root;
                _treeView!.Nodes.Clear();
                target = _treeView.Nodes;
            });
            if (target != null) await AppendPageAsync(target, 0, result.Page, version, token);
            await RunPendingSearchAsync(version, token);
        }
        catch (JsonException)
        {
            if (!token.IsCancellationRequested && version == _loadVersion)
                await LoadTextAsync(rawJson, version, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadTextAsync(string text, int version, CancellationToken token)
    {
        try
        {
            var regex = _imageUrlRegex;
            var page = await Task.Run(() => JsonTreeViewLoader.CreateTextPage(text, 0, regex, token), token);
            TreeNodeCollection? target = null;
            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentLoad(version, token)) return;
                _isJson = false;
                _rootElement = null;
                _treeView!.Nodes.Clear();
                target = _treeView.Nodes;
            });
            if (target != null) await AppendPageAsync(target, 0, page, version, token);
            await RunPendingSearchAsync(version, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool IsCurrentLoad(int version, CancellationToken token) =>
        !token.IsCancellationRequested && !IsDisposed && _treeView is { IsDisposed: false } && version == _loadVersion;

    private Task RunPendingSearchAsync(int version, CancellationToken token) => InvokeOnUiAsync(() =>
    {
        if (!IsCurrentLoad(version, token) || _pendingSearchKeyword == null) return;
        string keyword = _pendingSearchKeyword;
        _pendingSearchKeyword = null;
        SearchAndHighlight(keyword);
    });

    private Task InvokeOnUiAsync(Action action)
    {
        var invokeTarget = _treeView;
        if (IsDisposed || invokeTarget is not { IsDisposed: false }) return Task.CompletedTask;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Execute()
        {
            try
            {
                if (!IsDisposed && !invokeTarget.IsDisposed) action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        try
        {
            if (invokeTarget.IsHandleCreated)
            {
                if (invokeTarget.InvokeRequired) invokeTarget.BeginInvoke((Action)Execute);
                else Execute();
            }
            else if (_uiContext != null)
            {
                _uiContext.Post(_ => Execute(), null);
            }
            else
            {
                completion.TrySetResult(false);
            }
        }
        catch (InvalidOperationException)
        {
            completion.TrySetResult(false);
        }
        return completion.Task;
    }

    /// <summary>仅展开已经物化的树节点，不触发未加载内容的全量构建。</summary>
    public void ExpandAll()
    {
        if (_treeView != null) ExpandMaterializedNodes(_treeView.Nodes);
    }

    private static void ExpandMaterializedNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            bool hasLazyMarker = node.Nodes.Count == 1 &&
                                 ReferenceEquals(node.Nodes[0].Tag, JsonTreeViewLoader.LazyMarker);
            if (hasLazyMarker) continue;
            node.Expand();
            ExpandMaterializedNodes(node.Nodes);
        }
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

    /// <summary>后台搜索完整 JSON；同一关键字重复调用时跳到下一处。</summary>
    public void SearchAndHighlight(string keyword)
    {
        if (_treeView == null) return;
        if (string.IsNullOrEmpty(keyword))
        {
            ResetSearch(true);
            return;
        }

        bool sameKeyword = string.Equals(_searchKeyword, keyword, StringComparison.OrdinalIgnoreCase);
        if (_searchInProgress && sameKeyword) return;
        if (!sameKeyword) StartSearch(keyword);

        if (_isJson && _rootElement == null)
        {
            _pendingSearchKeyword = keyword;
            _searchInProgress = false;
            return;
        }
        if (_isJson && _searchSession == null && _rootElement is { } searchRoot)
            _searchSession = new JsonTreeViewLoader.JsonSearchSession(searchRoot, keyword,
                _searchCts?.Token ?? CancellationToken.None);

        _searchInProgress = true;
        int searchVersion = _searchVersion;
        int loadVersion = _loadVersion;
        var token = _searchCts?.Token ?? CancellationToken.None;
        var session = _searchSession;
        var root = _rootElement;
        int plainOffset = _plainSearchOffset;
        _ = SearchNextAsync(keyword, session, root, plainOffset, loadVersion, searchVersion, token);
    }

    private void StartSearch(string keyword)
    {
        ResetSearch(true);
        _searchKeyword = keyword;
        _searchLoadVersion = _loadVersion;
        _searchCts = CancellationTokenSource.CreateLinkedTokenSource(
            _loadCts?.Token ?? CancellationToken.None);
        _searchVersion++;
        _plainSearchOffset = 0;
        if (_isJson && _rootElement is { } root)
            _searchSession = new JsonTreeViewLoader.JsonSearchSession(root, keyword, _searchCts.Token);
    }

    private void ResetSearch(bool clearHighlight)
    {
        var searchCts = _searchCts;
        _searchCts = null;
        try { searchCts?.Cancel(); } catch (ObjectDisposedException) { }
        searchCts?.Dispose();
        var searchSession = _searchSession;
        _searchSession = null;
        if (!_searchInProgress) searchSession?.Dispose();
        _searchVersion++;
        _searchLoadVersion = 0;
        _plainSearchOffset = 0;
        _searchInProgress = false;
        _pendingSearchKeyword = null;
        _searchKeyword = null;
        if (_treeView != null) RemoveSearchProjections(_treeView.Nodes);
        if (clearHighlight)
        {
            _highlightedNodes.Clear();
            _treeView?.Invalidate();
        }
    }

    private async Task SearchNextAsync(string keyword, JsonTreeViewLoader.JsonSearchSession? session,
        JsonElement? root, int plainOffset, int loadVersion, int searchVersion, CancellationToken token)
    {
        JsonTreeViewLoader.JsonSearchSession? restartedSession = null;
        try
        {
            JsonTreeViewLoader.JsonSearchPath? path = null;
            int plainMatch = -1;
            bool wrapped = false;

            if (_isJson && root is { } rootElement && session != null)
            {
                path = await JsonFormatter.RunBackgroundAsync(session.FindNext, token);
                if (path == null && session.HasReturnedMatch)
                {
                    restartedSession = new JsonTreeViewLoader.JsonSearchSession(rootElement, keyword, token);
                    path = await JsonFormatter.RunBackgroundAsync(restartedSession.FindNext, token);
                    wrapped = path != null;
                }
            }
            else
            {
                string text = _rawText ?? string.Empty;
                var result = await JsonFormatter.RunBackgroundAsync(
                    () => FindPlainTextMatch(text, keyword, plainOffset, token), token);
                plainMatch = result.Index;
                wrapped = result.Wrapped;
            }

            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentSearch(loadVersion, searchVersion, keyword, token)) return;
                if (restartedSession != null)
                    _searchSession = path == null ? null : restartedSession;

                if (_treeView != null) RemoveSearchProjections(_treeView.Nodes);
                _highlightedNodes.Clear();
                TreeNode? matchNode = path != null
                    ? MaterializeSearchPath(path)
                    : plainMatch >= 0 ? MaterializePlainTextMatch(plainMatch) : null;
                if (matchNode == null)
                {
                    System.Media.SystemSounds.Beep.Play();
                    return;
                }

                if (plainMatch >= 0)
                    _plainSearchOffset = plainMatch + Math.Max(1, keyword.Length);
                HighlightSearchNode(matchNode);
                if (wrapped) System.Media.SystemSounds.Beep.Play();
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            bool keepOriginalSession = false;
            bool keepRestartedSession = false;
            await InvokeOnUiAsync(() =>
            {
                keepOriginalSession = ReferenceEquals(_searchSession, session);
                keepRestartedSession = ReferenceEquals(_searchSession, restartedSession);
                if (searchVersion == _searchVersion) _searchInProgress = false;
            });
            if (session != null && !keepOriginalSession) session.Dispose();
            if (restartedSession != null && !keepRestartedSession) restartedSession.Dispose();
        }
    }

    private static (int Index, bool Wrapped) FindPlainTextMatch(string text, string keyword, int start,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int safeStart = Math.Clamp(start, 0, text.Length);
        int index = text.IndexOf(keyword, safeStart, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) return (index, false);
        if (safeStart > 0)
        {
            index = text.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) return (index, true);
        }
        return (-1, false);
    }

    private bool IsCurrentSearch(int loadVersion, int searchVersion, string keyword,
        CancellationToken token) =>
        !token.IsCancellationRequested && loadVersion == _loadVersion && searchVersion == _searchVersion &&
        _searchLoadVersion == loadVersion &&
        string.Equals(_searchKeyword, keyword, StringComparison.OrdinalIgnoreCase);

    private TreeNode? MaterializeSearchPath(JsonTreeViewLoader.JsonSearchPath path)
    {
        if (_treeView == null || path.Nodes.Count == 0) return null;
        TreeNodeCollection collection = _treeView.Nodes;
        TreeNode? lastNode = null;

        for (int level = 0; level < path.Nodes.Count; level++)
        {
            var descriptor = path.Nodes[level];
            var node = FindNodeByOrdinal(collection, descriptor.Info.ChildOrdinal);
            if (node == null)
            {
                var uiDescriptor = JsonTreeViewLoader.CloneForUi(descriptor, true);
                node = JsonTreeViewLoader.CreateTreeNode(uiDescriptor);
                InsertProjectionNode(collection, node, uiDescriptor.Info.ChildOrdinal);
            }
            else
            {
                MergeNodeInfo(node, descriptor, false);
            }

            lastNode = node;
            if (level >= path.Nodes.Count - 1) continue;
            if (node.Tag is not JsonPathInfo info || info.ChildElement is not { } childElement) return null;
            PrepareProjectedChildren(node, childElement);
            node.Expand();
            collection = node.Nodes;
        }
        return lastNode;
    }

    private TreeNode? MaterializePlainTextMatch(int characterIndex)
    {
        if (_treeView == null || _rawText == null) return null;
        int chunkIndex = characterIndex / JsonTreeViewLoader.TextChunkSize;
        var existing = FindNodeByOrdinal(_treeView.Nodes, chunkIndex);
        if (existing != null) return existing;
        var descriptor = JsonTreeViewLoader.CreateTextDescriptor(_rawText, chunkIndex, _imageUrlRegex);
        var uiDescriptor = JsonTreeViewLoader.CloneForUi(descriptor, true);
        var node = JsonTreeViewLoader.CreateTreeNode(uiDescriptor);
        InsertProjectionNode(_treeView.Nodes, node, chunkIndex);
        return node;
    }

    private void HighlightSearchNode(TreeNode node)
    {
        if (_treeView == null) return;
        _highlightedNodes.Clear();
        _highlightedNodes.Add(node);
        var parent = node.Parent;
        while (parent != null)
        {
            parent.Expand();
            parent = parent.Parent;
        }
        _treeView.SelectedNode = node;
        node.EnsureVisible();
        _treeView.Invalidate();
    }

    private static TreeNode? FindNodeByOrdinal(TreeNodeCollection collection, int childOrdinal)
    {
        foreach (TreeNode node in collection)
        {
            if (node.Tag is JsonPathInfo info && info.ChildOrdinal == childOrdinal) return node;
        }
        return null;
    }

    private static void MergeNodeInfo(TreeNode node, JsonTreeViewLoader.JsonNodeDescriptor descriptor,
        bool normalPage)
    {
        if (node.Tag is not JsonPathInfo target) return;
        var source = descriptor.Info;
        target.RawValue ??= source.RawValue;
        target.Element ??= source.Element;
        if (source.ChildElement is { } childElement) target.ChildElement = childElement;
        if (source.DeferredContent == JsonDeferredContentKind.None)
            target.DeferredContent = JsonDeferredContentKind.None;
        target.CachedImageUrl ??= source.CachedImageUrl;
        if (normalPage)
        {
            target.IsSearchProjection = false;
            node.Text = descriptor.Text;
        }
        if (descriptor.HasChildren && node.Nodes.Count == 0)
            node.Nodes.Add(new TreeNode { Tag = JsonTreeViewLoader.LazyMarker });
    }

    private static void InsertProjectionNode(TreeNodeCollection collection, TreeNode node, int childOrdinal)
    {
        int pageIndex = -1;
        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].Tag is JsonTreeViewLoader.JsonPageRequest or
                JsonTreeViewLoader.TextPageRequest)
            {
                pageIndex = i;
                break;
            }
        }

        int insertIndex = pageIndex >= 0 ? pageIndex + 1 : collection.Count;
        while (insertIndex < collection.Count && collection[insertIndex].Tag is JsonPathInfo info &&
               info.IsSearchProjection && info.ChildOrdinal < childOrdinal)
            insertIndex++;
        collection.Insert(insertIndex, node);
    }

    private static void PrepareProjectedChildren(TreeNode node, JsonElement childElement)
    {
        bool hadLazyMarker = node.Nodes.Count == 1 &&
                             ReferenceEquals(node.Nodes[0].Tag, JsonTreeViewLoader.LazyMarker);
        if (hadLazyMarker) node.Nodes.Clear();
        bool hasPageNode = node.Nodes.Cast<TreeNode>().Any(child =>
            child.Tag is JsonTreeViewLoader.JsonPageRequest or JsonTreeViewLoader.TextPageRequest);
        if (node.Nodes.Count == 0 || hadLazyMarker && !hasPageNode)
        {
            var cursor = new JsonTreeViewLoader.JsonPageCursor(childElement);
            node.Nodes.Add(JsonTreeViewLoader.CreatePageNode(new JsonTreeViewLoader.JsonPageRequest(cursor)));
        }
    }

    private void RemoveSearchProjections(TreeNodeCollection nodes)
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (node.Tag is JsonPathInfo { IsSearchProjection: true })
            {
                nodes.RemoveAt(i);
                continue;
            }
            RemoveSearchProjections(node.Nodes);
        }
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
    /// 设置图片 URL 正则模式，匹配的节点悬浮时显示图片预览。空或无效则禁用。
    /// </summary>
    public void SetImageUrlPattern(string? pattern)
    {
        string? oldPattern = _imageUrlRegex?.ToString();
        _imageUrlRegex = null;
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            try
            {
                _imageUrlRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(500));
            }
            catch
            {
            }
        }

        if (_rawText != null && !string.Equals(oldPattern, _imageUrlRegex?.ToString(), StringComparison.Ordinal))
        {
            if (_isJson) DisplayJson(_rawText);
            else DisplayPlainText(_rawText);
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
        var treeView = new NoToolTipTreeView
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
        treeView.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) treeView.SelectedNode = e.Node;
        };
        treeView.ContextMenuStrip = CreateContextMenu();

        treeView.MouseMove += OnTreeViewMouseMove;
        treeView.MouseLeave += OnTreeViewMouseLeave;
        _hoverTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _hoverTimer.Tick += OnHoverTimerTick;
    }

    /// <summary>
    /// TreeView 展开事件：后台准备当前页，UI 线程分批挂载节点。
    /// </summary>
    private async void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (_treeView == null || e.Node.Nodes.Count != 1 ||
            !ReferenceEquals(e.Node.Nodes[0].Tag, JsonTreeViewLoader.LazyMarker)) return;

        e.Cancel = true;
        int version = _loadVersion;
        var token = _loadCts?.Token ?? CancellationToken.None;

        if (e.Node.Tag is JsonTreeViewLoader.JsonPageRequest or JsonTreeViewLoader.TextPageRequest)
        {
            await LoadPageNodeAsync(e.Node, version, token);
            return;
        }

        if (e.Node.Tag is JsonPathInfo info)
            await LoadContentNodeAsync(e.Node, info, version, token);
    }

    private async Task LoadContentNodeAsync(TreeNode node, JsonPathInfo info, int version,
        CancellationToken token)
    {
        if (info.IsLoading) return;
        info.IsLoading = true;
        node.Nodes[0].Text = Language.JsonParsing;
        var deferredContent = info.DeferredContent;
        var rawValue = info.RawValue ?? string.Empty;
        var childElement = info.ChildElement;
        var regex = _imageUrlRegex;
        bool parseFailed = false;
        JsonElement? parsedRoot = null;

        try
        {
            JsonTreeViewLoader.JsonNodePage page;
            if (deferredContent == JsonDeferredContentKind.EmbeddedJson)
            {
                try
                {
                    var parsed = await ParseEmbeddedJsonAsync(rawValue, token);
                    parsedRoot = parsed.Root;
                    page = parsed.Page;
                }
                catch (JsonException)
                {
                    parseFailed = true;
                    page = await Task.Run(
                        () => JsonTreeViewLoader.CreateTextPage(rawValue, 0, regex, token), token);
                }
            }
            else if (deferredContent == JsonDeferredContentKind.LongText)
            {
                page = await Task.Run(() => JsonTreeViewLoader.CreateTextPage(rawValue, 0, regex, token), token);
            }
            else if (childElement is { } element)
            {
                page = await Task.Run(() => JsonTreeViewLoader.CreateJsonPage(element, regex, token), token);
            }
            else
            {
                return;
            }

            TreeNodeCollection? target = null;
            int insertIndex = 0;
            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentLoad(version, token) || node.TreeView != _treeView) return;
                if (parsedRoot is { } root)
                {
                    info.ChildElement = root;
                    info.DeferredContent = JsonDeferredContentKind.None;
                }
                else if (parseFailed)
                {
                    info.DeferredContent = JsonDeferredContentKind.LongText;
                }

                for (int i = node.Nodes.Count - 1; i >= 0; i--)
                {
                    var childTag = node.Nodes[i].Tag;
                    if (ReferenceEquals(childTag, JsonTreeViewLoader.LazyMarker) ||
                        childTag is JsonTreeViewLoader.JsonPageRequest or JsonTreeViewLoader.TextPageRequest ||
                        childTag is not JsonPathInfo)
                        node.Nodes.RemoveAt(i);
                }
                if (parseFailed)
                {
                    node.Nodes.Insert(0, new TreeNode(Language.JsonParseFailed));
                    insertIndex = 1;
                }
                target = node.Nodes;
            });

            if (target == null) return;
            await AppendPageAsync(target, insertIndex, page, version, token, node);
            await InvokeOnUiAsync(() =>
            {
                if (IsCurrentLoad(version, token) && node.TreeView == _treeView) node.Expand();
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            info.IsLoading = false;
        }
    }

    private async Task<(JsonElement Root, JsonTreeViewLoader.JsonNodePage Page)> ParseEmbeddedJsonAsync(
        string rawValue, CancellationToken token)
    {
        var regex = _imageUrlRegex;
        return await JsonFormatter.RunBackgroundAsync(() =>
        {
            using var document = JsonDocument.Parse(rawValue);
            var root = document.RootElement.Clone();
            var page = JsonTreeViewLoader.CreateJsonPage(root, regex, token);
            return (Root: root, Page: page);
        }, token);
    }

    private async Task LoadPageNodeAsync(TreeNode marker, int version, CancellationToken token)
    {
        if (!_loadingNodes.Add(marker)) return;
        marker.Nodes[0].Text = Language.JsonParsing;
        try
        {
            JsonTreeViewLoader.JsonNodePage page;
            if (marker.Tag is JsonTreeViewLoader.JsonPageRequest jsonPage)
            {
                var regex = _imageUrlRegex;
                page = await Task.Run(
                    () => JsonTreeViewLoader.CreateJsonPage(jsonPage.Cursor, regex, token), token);
            }
            else if (marker.Tag is JsonTreeViewLoader.TextPageRequest textPage)
            {
                var regex = _imageUrlRegex;
                page = await Task.Run(
                    () => JsonTreeViewLoader.CreateTextPage(textPage.Source, textPage.Offset, regex, token), token);
            }
            else
            {
                return;
            }

            TreeNodeCollection? collection = null;
            TreeNode? owner = null;
            int insertIndex = 0;
            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentLoad(version, token) || marker.TreeView != _treeView) return;
                collection = marker.Parent?.Nodes ?? _treeView!.Nodes;
                insertIndex = marker.Index;
                owner = marker.Parent;
                marker.Remove();
            });
            if (collection != null)
                await AppendPageAsync(collection, insertIndex, page, version, token, owner);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await InvokeOnUiAsync(() => _loadingNodes.Remove(marker));
        }
    }

    private async Task AppendPageAsync(TreeNodeCollection collection, int insertIndex,
        JsonTreeViewLoader.JsonNodePage page, int version, CancellationToken token, TreeNode? owner = null)
    {
        int itemIndex = 0;
        while (itemIndex < page.Items.Count)
        {
            bool appended = false;
            await InvokeOnUiAsync(() =>
            {
                if (!IsCurrentLoad(version, token) || owner != null && owner.TreeView != _treeView) return;
                _treeView!.BeginUpdate();
                try
                {
                    int end = Math.Min(itemIndex + JsonTreeViewLoader.UiBatchSize, page.Items.Count);
                    for (; itemIndex < end; itemIndex++)
                    {
                        var descriptor = page.Items[itemIndex];
                        var existing = FindNodeByOrdinal(collection, descriptor.Info.ChildOrdinal);
                        if (existing != null)
                        {
                            MergeNodeInfo(existing, descriptor, true);
                            insertIndex = Math.Max(insertIndex, existing.Index + 1);
                            continue;
                        }
                        collection.Insert(insertIndex++, JsonTreeViewLoader.CreateTreeNode(descriptor));
                    }
                    appended = true;
                }
                finally
                {
                    _treeView.EndUpdate();
                }
            });
            if (!appended) return;
        }

        if (page.NextPage != null)
        {
            await InvokeOnUiAsync(() =>
            {
                if (IsCurrentLoad(version, token) && (owner == null || owner.TreeView == _treeView))
                    collection.Insert(insertIndex, JsonTreeViewLoader.CreatePageNode(page.NextPage));
            });
        }
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
            var keyPart = $"\"{info.DisplayKey ?? info.Key}\": ";
            x += DrawTextSegment(g, keyPart, _displayFont, ColorKey, x, y, bounds);
            var summary = isNodeExpanded
                ? info.ValueKind == JsonValueKind.Object ? "{" : "["
                : text.Length >= keyPart.Length ? text[keyPart.Length..] : string.Empty;
            DrawTextSegment(g, summary, _displayFont, ColorSummary, x, y, bounds);
        }
        else if (info.Key != null)
        {
            var keyPart = $"\"{info.DisplayKey ?? info.Key}\": ";
            x += DrawTextSegment(g, keyPart, _displayFont, ColorKey, x, y, bounds);

            var valueColor = info.ValueKind switch
            {
                JsonValueKind.String => ColorString,
                JsonValueKind.Number => ColorNumber,
                JsonValueKind.True or JsonValueKind.False => ColorBool,
                JsonValueKind.Null => ColorNull,
                _ => SystemColors.ControlText
            };

            var valueText = text.Length >= keyPart.Length ? text[keyPart.Length..] : string.Empty;
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
    /// TreeView MouseMove：检测悬浮节点变化，重启延迟计时器。
    /// </summary>
    private void OnTreeViewMouseMove(object? sender, MouseEventArgs e)
    {
        if (_treeView == null || _imageUrlRegex == null) return;
        var node = _treeView.GetNodeAt(e.Location);
        if (node == _hoveredNode) return;

        _hoveredNode = node;
        _hoverTimer!.Stop();
        HideImagePopup();
        if (node != null && ExtractImageUrl(node) != null)
            _hoverTimer.Start();
    }

    /// <summary>
    /// TreeView MouseLeave：停止计时器并隐藏弹窗。
    /// </summary>
    private void OnTreeViewMouseLeave(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        _hoveredNode = null;
        HideImagePopup();
    }

    /// <summary>
    /// 延迟计时器触发：显示图片预览弹窗。
    /// </summary>
    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        if (_hoveredNode == null) return;
        var url = ExtractImageUrl(_hoveredNode);
        if (url == null) return;
        ShowImagePopup(url);
    }

    /// <summary>读取后台预计算的图片 URL，悬浮事件不再扫描完整字符串。</summary>
    private static string? ExtractImageUrl(TreeNode node) =>
        node.Tag is JsonPathInfo info ? info.CachedImageUrl : null;

    /// <summary>
    /// 显示图片预览弹窗，定位在鼠标右下方，超出屏幕时翻转。
    /// </summary>
    private void ShowImagePopup(string url)
    {
        HideImagePopup();
        _imagePopup = new ImagePreviewPopup();
        var cursor = Cursor.Position;
        _imagePopup.Location = new Point(cursor.X + 16, cursor.Y + 16);
        _imagePopup.Deactivate += (_, _) => HideImagePopup();
        _imagePopup.Show();
        _imagePopup.LoadImageAsync(url);
    }

    /// <summary>
    /// 隐藏并释放当前图片预览弹窗。
    /// </summary>
    private void HideImagePopup()
    {
        if (_imagePopup == null) return;
        _imagePopup.Deactivate -= (_, _) => HideImagePopup();
        _imagePopup.Close();
        _imagePopup.Dispose();
        _imagePopup = null;
    }

    /// <summary>
    /// 创建右键上下文菜单，包含复制值、复制 JSONPath、复制节点 JSON、展开/折叠等操作。
    /// </summary>
    /// <returns>构建完成的 ContextMenuStrip。</returns>
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var copyValue = new ToolStripMenuItem("Copy Value");
        copyValue.Click += async (s, e) =>
        {
            if (_treeView?.SelectedNode?.Tag is JsonPathInfo info)
            {
                string? value = await Task.Run(info.GetFullValue);
                await ClipboardTextHelper.TrySetTextAsync(value);
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
        copyNode.Click += async (s, e) =>
        {
            if (_treeView?.SelectedNode?.Tag is JsonPathInfo info)
            {
                string? nodeJson = await Task.Run(info.GetNodeJson);
                await ClipboardTextHelper.TrySetTextAsync(nodeJson);
            }
        };

        var sep1 = new ToolStripSeparator();
        var expandAll = new ToolStripMenuItem("Expand All");
        expandAll.Click += (s, e) => ExpandAll();

        var collapseAll = new ToolStripMenuItem("Collapse All");
        collapseAll.Click += (s, e) => _treeView?.CollapseAll();

        var collapseTo2 = new ToolStripMenuItem("Collapse to Level 2");
        collapseTo2.Click += (s, e) => CollapseToLevel(2);

        menu.Items.AddRange(new ToolStripItem[]
            { copyValue, copyPath, copyNode, sep1, expandAll, collapseAll, collapseTo2 });
        return menu;
    }

    private sealed class NoToolTipTreeView : TreeView
    {
        private const int TvsNoToolTips = 0x0080;

        protected override CreateParams CreateParams
        {
            get
            {
                var createParams = base.CreateParams;
                createParams.Style |= TvsNoToolTips;
                return createParams;
            }
        }
    }

    /// <summary>取消后台加载并释放控件资源。</summary>
    protected override void Dispose(bool disposing)
    {
        Font? fontToDispose = null;
        if (disposing)
        {
            ResetSearch(false);
            _rootElement = null;
            _rawText = null;
            _highlightedNodes.Clear();
            _loadingNodes.Clear();
            var loadCts = _loadCts;
            _loadCts = null;
            try { loadCts?.Cancel(); } catch (ObjectDisposedException) { }
            loadCts?.Dispose();
            HideImagePopup();
            _hoverTimer?.Stop();
            _hoverTimer?.Dispose();
            fontToDispose = _displayFont;
        }

        base.Dispose(disposing);
        fontToDispose?.Dispose();
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
        try
        {
            treeView.CollapseAll();
            ExpandToLevel(treeView.Nodes, level, 0);
        }
        finally
        {
            treeView.EndUpdate();
        }
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
            bool hasLazyMarker = node.Nodes.Count == 1 &&
                                 ReferenceEquals(node.Nodes[0].Tag, JsonTreeViewLoader.LazyMarker);
            if (currentLevel < targetLevel && !hasLazyMarker)
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