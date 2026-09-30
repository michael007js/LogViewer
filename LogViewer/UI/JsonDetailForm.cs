using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// JSON 详情窗口，双击网络日志时弹出，显示请求和响应的 JSON 内容。
/// 支持 JSON 树视图和原始文本视图切换，以及搜索高亮功能。
/// </summary>
public partial class JsonDetailForm : Form
{
    /// <summary>当前显示的日志条目。</summary>
    private LogEntry _entry = new();

    /// <summary>自定义字体实例（从调用方传入的字体克隆，需在 Dispose 中释放）。</summary>
    private Font _font = null!;

    private SplitContainer _split;

    private JsonTreeView _jsonRequest;

    private JsonTreeView _jsonResponse;

    private TextBox _rawRequest;

    private TextBox _rawResponse;

    /// <summary>切换请求视图模式的按钮。</summary>
    private Button _btnToggleRequest = null!;

    /// <summary>切换响应视图模式的按钮。</summary>
    private Button _btnToggleResponse = null!;

    /// <summary>展开请求 JSON 树的按钮。</summary>
    private Button _btnExpandReq = null!;

    /// <summary>折叠请求 JSON 树的按钮。</summary>
    private Button _btnCollapseReq = null!;

    /// <summary>折叠请求 JSON 树到第2级的按钮。</summary>
    private Button _btnLvl2Req;

    /// <summary>请求 JSON 搜索关键字输入框。</summary>
    private TextBox _txtSearchReq;

    /// <summary>执行请求 JSON 搜索高亮的按钮。</summary>
    private Button _btnSearchReq;

    /// <summary>展开响应 JSON 树的按钮。</summary>
    private Button _btnExpandRes = null!;

    /// <summary>折叠响应 JSON 树的按钮。</summary>
    private Button _btnCollapseRes = null!;

    /// <summary>折叠响应 JSON 树到第2级的按钮。</summary>
    private Button _btnLvl2Res;

    /// <summary>响应 JSON 搜索关键字输入框。</summary>
    private TextBox _txtSearchRes;

    /// <summary>执行响应 JSON 搜索高亮的按钮。</summary>
    private Button _btnSearchRes;

    /// <summary>请求视图是否为原始文本模式。</summary>
    private bool _requestIsRaw;

    /// <summary>响应视图是否为原始文本模式。</summary>
    private bool _responseIsRaw;

    private PagedTextView _pagedRequest = null!;
    private PagedTextView _pagedResponse = null!;
    private CancellationTokenSource? _rawLoadCts;
    private int _rawLoadVersion;
    private int _requestRawState;
    private int _responseRawState;

    /// <summary>
    /// 设计器模式构造函数。
    /// </summary>
    public JsonDetailForm()
    {
        _font = (Font)SystemFonts.DefaultFont.Clone();
        InitializeComponent();
    }

    /// <summary>
    /// 运行时构造函数，传入日志条目和字体初始化窗口。
    /// 设置标题栏文本，连接事件，加载 JSON 数据到树视图和文本框。
    /// </summary>
    public JsonDetailForm(LogEntry entry, Font font)
    {
        _entry = entry;
        _font = new Font(font.FontFamily, font.Size);
        InitializeComponent();
        InitializePagedRawViews();
        Text = string.Format(Language.JsonDetailTitle, _entry.Method ?? string.Empty, _entry.UrlPath ?? string.Empty,
            _entry.Code, _entry.Duration);
        _txtSearchReq.PlaceholderText = Language.SearchPlaceholder;
        _txtSearchRes.PlaceholderText = Language.SearchPlaceholder;
        WireComponentEvents();
        LoadData();
    }

    private void InitializePagedRawViews()
    {
        _pagedRequest = new PagedTextView { Dock = DockStyle.Fill, Visible = false };
        _pagedResponse = new PagedTextView { Dock = DockStyle.Fill, Visible = false };
        _pagedRequest.SetDisplayFont(_font);
        _pagedResponse.SetDisplayFont(_font);
        _requestContainer.Controls.Add(_pagedRequest);
        _responseContainer.Controls.Add(_pagedResponse);
        _requestContainer.Controls.SetChildIndex(_pagedRequest, 0);
        _responseContainer.Controls.SetChildIndex(_pagedResponse, 0);
        _requestContainer.Controls.SetChildIndex(_requestToolbar, _requestContainer.Controls.Count - 1);
        _responseContainer.Controls.SetChildIndex(_responseToolbar, _responseContainer.Controls.Count - 1);
        _requestContainer.PerformLayout();
        _responseContainer.PerformLayout();
    }

    /// <summary>连接所有工具栏按钮和搜索控件的 Click 事件，以及窗口 Shown 事件用于延迟布局。</summary>
    private void WireComponentEvents()
    {
        _btnToggleRequest.Click += (s, e) => ToggleRequestView();
        _btnExpandReq.Click += (s, e) => _jsonRequest.ExpandAll();
        _btnCollapseReq.Click += (s, e) => _jsonRequest.CollapseAll();
        _btnLvl2Req.Click += (s, e) => _jsonRequest.CollapseToLevel(2);
        _btnSearchReq.Click += (s, e) => _jsonRequest.SearchAndHighlight(_txtSearchReq.Text);
        _btnToggleResponse.Click += (s, e) => ToggleResponseView();
        _btnExpandRes.Click += (s, e) => _jsonResponse.ExpandAll();
        _btnCollapseRes.Click += (s, e) => _jsonResponse.CollapseAll();
        _btnLvl2Res.Click += (s, e) => _jsonResponse.CollapseToLevel(2);
        _btnSearchRes.Click += (s, e) => _jsonResponse.SearchAndHighlight(_txtSearchRes.Text);
    }

    /// <summary>加载请求/响应树；原文只在用户切换后后台格式化。</summary>
    private void LoadData()
    {
        _rawLoadCts?.Cancel();
        _rawLoadCts?.Dispose();
        _rawLoadCts = new CancellationTokenSource();
        _rawLoadVersion++;
        _requestRawState = 0;
        _responseRawState = 0;
        _pagedRequest.Clear();
        _pagedResponse.Clear();
        _rawRequest.Clear();
        _rawResponse.Clear();
        _jsonRequest.DisplayJson(_entry.Send ?? string.Empty);
        _jsonResponse.DisplayJson(_entry.Content ?? string.Empty);
    }

    /// <summary>切换请求区域在 JSON 树视图和原始文本之间，同步启用/禁用树操作按钮。</summary>
    private void ToggleRequestView()
    {
        _requestIsRaw = !_requestIsRaw;
        _jsonRequest.Visible = !_requestIsRaw;
        _pagedRequest.Visible = _requestIsRaw;
        _rawRequest.Visible = false;
        _btnToggleRequest.Text = _requestIsRaw ? Language.Tree : Language.Raw;
        _btnExpandReq.Enabled = !_requestIsRaw;
        _btnCollapseReq.Enabled = !_requestIsRaw;
        _btnLvl2Req.Enabled = !_requestIsRaw;
        _txtSearchReq.Enabled = !_requestIsRaw;
        _btnSearchReq.Enabled = !_requestIsRaw;
        if (_requestIsRaw) EnsureRawLoaded(true);
    }

    /// <summary>切换响应区域在 JSON 树视图和原始文本之间，同步启用/禁用树操作按钮。</summary>
    private void ToggleResponseView()
    {
        _responseIsRaw = !_responseIsRaw;
        _jsonResponse.Visible = !_responseIsRaw;
        _pagedResponse.Visible = _responseIsRaw;
        _rawResponse.Visible = false;
        _btnToggleResponse.Text = _responseIsRaw ? Language.Tree : Language.Raw;
        _btnExpandRes.Enabled = !_responseIsRaw;
        _btnCollapseRes.Enabled = !_responseIsRaw;
        _btnLvl2Res.Enabled = !_responseIsRaw;
        _txtSearchRes.Enabled = !_responseIsRaw;
        _btnSearchRes.Enabled = !_responseIsRaw;
        if (_responseIsRaw) EnsureRawLoaded(false);
    }

    private void EnsureRawLoaded(bool request)
    {
        if (request)
        {
            if (_requestRawState != 0) return;
            _requestRawState = 1;
        }
        else
        {
            if (_responseRawState != 0) return;
            _responseRawState = 1;
        }

        var target = request ? _pagedRequest : _pagedResponse;
        string source = request ? _entry.Send ?? string.Empty : _entry.Content ?? string.Empty;
        int version = _rawLoadVersion;
        _ = LoadRawTextAsync(request, target, source, version, _rawLoadCts!.Token);
    }

    private async Task LoadRawTextAsync(bool request, PagedTextView target, string source, int version,
        CancellationToken token)
    {
        try
        {
            var document = await JsonFormatter.RunBackgroundAsync(() =>
            {
                string text = JsonFormatter.FormatJson(source) ?? source;
                return PagedTextView.BuildDocument(text, token);
            }, token);

            await InvokeRawUiAsync(() =>
            {
                if (!IsCurrentRawLoad(version, token) || target.IsDisposed) return;
                target.Display(document);
                if (request) _requestRawState = 2;
                else _responseRawState = 2;
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task InvokeRawUiAsync(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return Task.CompletedTask;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Execute()
        {
            try
            {
                if (!IsDisposed) action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        try
        {
            if (InvokeRequired) BeginInvoke((Action)Execute);
            else Execute();
        }
        catch (InvalidOperationException)
        {
            completion.TrySetResult(false);
        }
        return completion.Task;
    }

    private bool IsCurrentRawLoad(int version, CancellationToken token) =>
        !token.IsCancellationRequested && !IsDisposed && version == _rawLoadVersion;

    /// <summary>拦截 Esc 或 Ctrl+W 快捷键关闭窗口。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape || keyData == (Keys.Control | Keys.W))
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>取消后台加载并释放自定义字体实例。</summary>
    protected override void Dispose(bool disposing)
    {
        Font? fontToDispose = null;
        if (disposing)
        {
            var rawLoadCts = _rawLoadCts;
            _rawLoadCts = null;
            try { rawLoadCts?.Cancel(); } catch (ObjectDisposedException) { }
            rawLoadCts?.Dispose();
            fontToDispose = _font;
        }
        base.Dispose(disposing);
        fontToDispose?.Dispose();
    }
}