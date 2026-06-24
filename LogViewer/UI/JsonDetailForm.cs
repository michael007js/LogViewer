using System.ComponentModel;
using System.Diagnostics;
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
    private readonly LogEntry _entry;
    /// <summary>字体设置。</summary>
    private readonly Font _font;

    /// <summary>左右分栏容器。</summary>
    private SplitContainer _split = null!;
    /// <summary>请求 JSON 树视图。</summary>
    private JsonTreeView _jsonRequest = null!;
    /// <summary>响应 JSON 树视图。</summary>
    private JsonTreeView _jsonResponse = null!;
    /// <summary>请求原始文本框。</summary>
    private TextBox _rawRequest = null!;
    /// <summary>响应原始文本框。</summary>
    private TextBox _rawResponse = null!;

    /// <summary>切换请求视图模式的按钮。</summary>
    private Button _btnToggleRequest = null!;
    /// <summary>切换响应视图模式的按钮。</summary>
    private Button _btnToggleResponse = null!;
    /// <summary>展开请求 JSON 树的按钮。</summary>
    private Button _btnExpandReq = null!;
    /// <summary>折叠请求 JSON 树的按钮。</summary>
    private Button _btnCollapseReq = null!;
    /// <summary>折叠请求 JSON 树到第2级的按钮。</summary>
    private Button _btnLvl2Req = null!;
    /// <summary>请求搜索框。</summary>
    private TextBox _txtSearchReq = null!;
    /// <summary>请求搜索按钮。</summary>
    private Button _btnSearchReq = null!;

    /// <summary>展开响应 JSON 树的按钮。</summary>
    private Button _btnExpandRes = null!;
    /// <summary>折叠响应 JSON 树的按钮。</summary>
    private Button _btnCollapseRes = null!;
    /// <summary>折叠响应 JSON 树到第2级的按钮。</summary>
    private Button _btnLvl2Res = null!;
    /// <summary>响应搜索框。</summary>
    private TextBox _txtSearchRes = null!;
    /// <summary>响应搜索按钮。</summary>
    private Button _btnSearchRes = null!;

    /// <summary>请求视图是否为原始文本模式。</summary>
    private bool _requestIsRaw;
    /// <summary>响应视图是否为原始文本模式。</summary>
    private bool _responseIsRaw;

    /// <summary>
    /// 设计器模式构造函数。
    /// </summary>
    public JsonDetailForm() : this(new LogEntry(), SystemFonts.DefaultFont, true)
    {
    }

    /// <summary>
    /// 初始化 JSON 详情窗口。
    /// </summary>
    /// <param name="entry">要显示的日志条目。</param>
    /// <param name="font">字体设置。</param>
    public JsonDetailForm(LogEntry entry, Font font)
        : this(entry, font, false)
    {
    }

    /// <summary>
    /// 私有构造函数，处理初始化逻辑。
    /// </summary>
    private JsonDetailForm(LogEntry entry, Font font, bool designMode)
    {
        _entry = entry;
        _font = new Font(font.FontFamily, font.Size);
        InitializeComponent();
        if (designMode || IsDesignTimeMode())
        {
            return;
        }

        WireComponentEvents();
        if (!designMode)
        {
            LoadData();
        }
    }

    /// <summary>
    /// 连接组件事件。
    /// </summary>
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
        Shown += (s, e) => BeginInvoke(new Action(ApplyInitialSplitterLayout));
    }

    /// <summary>
    /// 应用初始分栏布局，设置左右面板比例为 4:6。
    /// </summary>
    private void ApplyInitialSplitterLayout()
    {
        int total = _split.ClientSize.Width - _split.SplitterWidth;
        int minPanel1 = _split.Panel1MinSize;
        int minPanel2 = _split.Panel2MinSize;

        if (total <= 0 || total < minPanel1 + minPanel2)
        {
            return;
        }

        int maxDistance = total - minPanel2;
        int distance = Math.Clamp((int)(total * 0.4), minPanel1, maxDistance);
        _split.SplitterDistance = distance;
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

    private void LoadData()
    {
        _jsonRequest.DisplayJson(_entry.Send ?? "");
        _jsonResponse.DisplayJson(_entry.Content ?? "");
        _rawRequest.Text = JsonFormatter.FormatJson(_entry.Send) ?? _entry.Send ?? "";
        _rawResponse.Text = JsonFormatter.FormatJson(_entry.Content) ?? _entry.Content ?? "";
    }

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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.W))
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font?.Dispose();
        base.Dispose(disposing);
    }
}
