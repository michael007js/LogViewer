using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class JsonDetailForm : Form
{
    private readonly LogEntry _entry;
    private readonly Font _font;

    private SplitContainer _split = null!;
    private JsonTreeView _jsonRequest = null!;
    private JsonTreeView _jsonResponse = null!;
    private TextBox _rawRequest = null!;
    private TextBox _rawResponse = null!;

    private Button _btnToggleRequest = null!;
    private Button _btnToggleResponse = null!;
    private Button _btnExpandReq = null!;
    private Button _btnCollapseReq = null!;
    private Button _btnLvl2Req = null!;
    private TextBox _txtSearchReq = null!;
    private Button _btnSearchReq = null!;

    private Button _btnExpandRes = null!;
    private Button _btnCollapseRes = null!;
    private Button _btnLvl2Res = null!;
    private TextBox _txtSearchRes = null!;
    private Button _btnSearchRes = null!;

    private bool _requestIsRaw;
    private bool _responseIsRaw;

    public JsonDetailForm() : this(new LogEntry(), SystemFonts.DefaultFont, true)
    {
    }

    public JsonDetailForm(LogEntry entry, Font font)
        : this(entry, font, false)
    {
    }

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
