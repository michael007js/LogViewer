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
    private Font _font = SystemFonts.DefaultFont;

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

    /// <summary>
    /// 设计器模式构造函数。
    /// </summary>
    public JsonDetailForm()
    {
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
        Text = string.Format(Language.JsonDetailTitle, _entry.Method ?? string.Empty, _entry.UrlPath ?? string.Empty,
            _entry.Code, _entry.Duration);
        _txtSearchReq.PlaceholderText = Language.SearchPlaceholder;
        _txtSearchRes.PlaceholderText = Language.SearchPlaceholder;
        WireComponentEvents();
        LoadData();
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

    /// <summary>将日志条目的请求/响应 JSON 加载到树视图和原始文本框，尝试格式化 JSON。</summary>
    private void LoadData()
    {
        _jsonRequest.DisplayJson(_entry.Send ?? "");
        _jsonResponse.DisplayJson(_entry.Content ?? "");
        _rawRequest.Text = JsonFormatter.FormatJson(_entry.Send) ?? _entry.Send ?? "";
        _rawResponse.Text = JsonFormatter.FormatJson(_entry.Content) ?? _entry.Content ?? "";
    }

    /// <summary>切换请求区域在 JSON 树视图和原始文本之间，同步启用/禁用树操作按钮。</summary>
    private void ToggleRequestView()
    {
        _requestIsRaw = !_requestIsRaw;
        _jsonRequest.Visible = !_requestIsRaw;
        _rawRequest.Visible = _requestIsRaw;
        _btnToggleRequest.Text = _requestIsRaw ? Language.Tree : Language.Raw;
        _btnExpandReq.Enabled = !_requestIsRaw;
        _btnCollapseReq.Enabled = !_requestIsRaw;
        _btnLvl2Req.Enabled = !_requestIsRaw;
        _txtSearchReq.Enabled = !_requestIsRaw;
        _btnSearchReq.Enabled = !_requestIsRaw;
    }

    /// <summary>切换响应区域在 JSON 树视图和原始文本之间，同步启用/禁用树操作按钮。</summary>
    private void ToggleResponseView()
    {
        _responseIsRaw = !_responseIsRaw;
        _jsonResponse.Visible = !_responseIsRaw;
        _rawResponse.Visible = _responseIsRaw;
        _btnToggleResponse.Text = _responseIsRaw ? Language.Tree : Language.Raw;
        _btnExpandRes.Enabled = !_responseIsRaw;
        _btnCollapseRes.Enabled = !_responseIsRaw;
        _btnLvl2Res.Enabled = !_responseIsRaw;
        _txtSearchRes.Enabled = !_responseIsRaw;
        _btnSearchRes.Enabled = !_responseIsRaw;
    }

    /// <summary>拦截 Ctrl+W 快捷键关闭窗口。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.W))
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>释放自定义字体实例，调用基类清理。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing) _font?.Dispose();
        base.Dispose(disposing);
    }
}