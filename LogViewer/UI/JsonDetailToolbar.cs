using LogViewer.Static;

namespace LogViewer.UI;

public partial class JsonDetailToolbar : UserControl
{
    private bool _detailViewIsRaw;

    public event EventHandler? SearchClicked;
    public event EventHandler? ExpandAllClicked;
    public event EventHandler? CollapseAllClicked;
    public event EventHandler? CollapseTo2Clicked;
    public event EventHandler? ViewToggled;

    public bool IsRawMode => _detailViewIsRaw;

    public string SearchText => _txtJsonSearch.Text;

    public JsonDetailToolbar()
    {
        InitializeComponent();
    }

    public void ApplyLanguage()
    {
        _txtJsonSearch.PlaceholderText = Language.SearchJsonPlaceholder;
        _btnExpandAll.Text = Language.Expand;
        _btnCollapseAll.Text = Language.Collapse;
        _btnCollapseTo2.Text = Language.CollapseLevel2;
        _btnToggleView.Text = _detailViewIsRaw ? Language.Tree : Language.Raw;
    }

    public void SetRawMode(bool isRaw)
    {
        _detailViewIsRaw = isRaw;
        _btnToggleView.Text = isRaw ? Language.Tree : Language.Raw;
        _btnExpandAll.Enabled = !isRaw;
        _btnCollapseAll.Enabled = !isRaw;
        _btnCollapseTo2.Enabled = !isRaw;
        _txtJsonSearch.Enabled = !isRaw;
        _btnJsonSearch.Enabled = !isRaw;
    }

    private void OnBtnJsonSearchClick(object? sender, EventArgs e) => SearchClicked?.Invoke(this, e);

    private void OnBtnExpandAllClick(object? sender, EventArgs e) => ExpandAllClicked?.Invoke(this, e);

    private void OnBtnCollapseAllClick(object? sender, EventArgs e) => CollapseAllClicked?.Invoke(this, e);

    private void OnBtnCollapseTo2Click(object? sender, EventArgs e) => CollapseTo2Clicked?.Invoke(this, e);

    private void OnBtnToggleViewClick(object? sender, EventArgs e)
    {
        SetRawMode(!_detailViewIsRaw);
        ViewToggled?.Invoke(this, e);
    }
}
