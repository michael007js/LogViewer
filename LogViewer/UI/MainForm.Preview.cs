using LogViewer.Static;

namespace LogViewer.UI;

/// <summary>
/// MainForm 的 JSON 预览部分，包含 JSON 详情面板的初始化、视图切换和详情显示逻辑。
/// </summary>
public partial class MainForm
{
    private System.Windows.Forms.Panel _pnlJsonToolbar;

    private System.Windows.Forms.TextBox _txtJsonSearch;

    private System.Windows.Forms.Button _btnJsonSearch;

    private System.Windows.Forms.Button _btnExpandAll;

    private System.Windows.Forms.Button _btnCollapseAll;

    private System.Windows.Forms.Button _btnCollapseTo2;

    private System.Windows.Forms.Button _btnToggleView;

    /// <summary>详情视图是否为原始文本模式。</summary>
    private bool _detailViewIsRaw;

    /// <summary>
    /// 初始化运行时的 JSON 树视图控件。
    /// </summary>
    private void InitializeJsonTreeViewsRuntime()
    {
        _jsonHeadersView = CreateRuntimeJsonTreeView(_jsonHeaders, nameof(_jsonHeadersView));
        _jsonRequestBodyView = CreateRuntimeJsonTreeView(_jsonRequestBody, nameof(_jsonRequestBodyView));
        _jsonResponseBodyView = CreateRuntimeJsonTreeView(_jsonResponseBody, nameof(_jsonResponseBodyView));
    }

    /// <summary>
    /// 创建运行时的 JSON 树视图控件并添加到宿主面板。
    /// </summary>
    private static JsonTreeView CreateRuntimeJsonTreeView(Control host, string name)
    {
        var view = new JsonTreeView
        {
            Dock = DockStyle.Fill,
            Name = name,
            Margin = Padding.Empty
        };
        host.Controls.Add(view);
        view.BringToFront();
        return view;
    }

    /// <summary>
    /// 获取当前选中的 JSON 树视图控件。
    /// </summary>
    private JsonTreeView? GetActiveJsonView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _jsonHeadersView;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _jsonRequestBodyView;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _jsonResponseBodyView;
        return null;
    }

    /// <summary>
    /// 获取当前选中的原始文本视图控件。
    /// </summary>
    private TextBox? GetActiveRawView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _rawHeaders;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _rawRequestBody;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _rawResponseBody;
        return null;
    }

    /// <summary>
    /// 切换详情视图模式（原始文本/JSON 树）。
    /// </summary>
    private void OnToggleDetailView(object? sender, EventArgs e)
    {
        _detailViewIsRaw = !_detailViewIsRaw;
        _btnToggleView.Text = _detailViewIsRaw ? Language.Tree : Language.Raw;
        _btnExpandAll.Enabled = !_detailViewIsRaw;
        _btnCollapseAll.Enabled = !_detailViewIsRaw;
        _btnCollapseTo2.Enabled = !_detailViewIsRaw;
        _txtJsonSearch.Enabled = !_detailViewIsRaw;
        _btnJsonSearch.Enabled = !_detailViewIsRaw;
        SyncDetailViewVisibility();
    }

    /// <summary>
    /// 同步详情视图的可见性，根据当前模式显示对应的控件。
    /// </summary>
    private void SyncDetailViewVisibility()
    {
        if (_jsonHeadersView != null) _jsonHeadersView.Visible = !_detailViewIsRaw;
        _rawHeaders.Visible = _detailViewIsRaw;
        if (_jsonRequestBodyView != null) _jsonRequestBodyView.Visible = !_detailViewIsRaw;
        _rawRequestBody.Visible = _detailViewIsRaw;
        if (_jsonResponseBodyView != null) _jsonResponseBodyView.Visible = !_detailViewIsRaw;
        _rawResponseBody.Visible = _detailViewIsRaw;
    }
}