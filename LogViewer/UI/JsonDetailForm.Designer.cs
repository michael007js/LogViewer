using LogViewer.Static;

namespace LogViewer.UI;

public partial class JsonDetailForm
{
    private Panel _requestContainer = null!;
    private Panel _requestToolbar = null!;
    private Label _lblRequestTitle = null!;
    private Panel _responseContainer = null!;
    private Panel _responseToolbar = null!;
    private Label _lblResponseTitle = null!;

    private void InitializeComponent()
    {
        _split = new SplitContainer();
        _requestContainer = new Panel();
        _requestToolbar = new Panel();
        _lblRequestTitle = new Label();
        _btnToggleRequest = new Button();
        _btnExpandReq = new Button();
        _btnCollapseReq = new Button();
        _btnLvl2Req = new Button();
        _txtSearchReq = new TextBox();
        _btnSearchReq = new Button();
        _jsonRequest = new JsonTreeView();
        _rawRequest = new TextBox();
        _responseContainer = new Panel();
        _responseToolbar = new Panel();
        _lblResponseTitle = new Label();
        _btnToggleResponse = new Button();
        _btnExpandRes = new Button();
        _btnCollapseRes = new Button();
        _btnLvl2Res = new Button();
        _txtSearchRes = new TextBox();
        _btnSearchRes = new Button();
        _jsonResponse = new JsonTreeView();
        _rawResponse = new TextBox();

        SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();

        Text = string.Format(Language.JsonDetailTitle, _entry.Method ?? string.Empty, _entry.UrlPath, _entry.Code, _entry.Duration);
        Size = new Size(1000, 600);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Consolas", 9F);
        KeyPreview = true;

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.Panel1MinSize = 150;
        _split.SplitterWidth = 4;

        _requestContainer.Dock = DockStyle.Fill;
        _requestToolbar.Dock = DockStyle.Top;
        _requestToolbar.Height = 26;
        _lblRequestTitle.Text = Language.RequestBody;
        _lblRequestTitle.Location = new Point(0, 4);
        _lblRequestTitle.AutoSize = true;
        _lblRequestTitle.Font = new Font("Consolas", 9F, FontStyle.Bold);
        _btnToggleRequest.Text = Language.Raw;
        _btnToggleRequest.SetBounds(100, 2, 42, 23);
        _btnExpandReq.Text = Language.Expand;
        _btnExpandReq.SetBounds(148, 2, 50, 23);
        _btnCollapseReq.Text = Language.Collapse;
        _btnCollapseReq.SetBounds(200, 2, 55, 23);
        _btnLvl2Req.Text = Language.CollapseLevel2;
        _btnLvl2Req.SetBounds(257, 2, 36, 23);
        _txtSearchReq.SetBounds(300, 3, 80, 23);
        _txtSearchReq.PlaceholderText = Language.SearchPlaceholder;
        _btnSearchReq.Text = "\u25B6";
        _btnSearchReq.SetBounds(382, 2, 22, 23);
        _requestToolbar.Controls.Add(_lblRequestTitle);
        _requestToolbar.Controls.Add(_btnToggleRequest);
        _requestToolbar.Controls.Add(_btnExpandReq);
        _requestToolbar.Controls.Add(_btnCollapseReq);
        _requestToolbar.Controls.Add(_btnLvl2Req);
        _requestToolbar.Controls.Add(_txtSearchReq);
        _requestToolbar.Controls.Add(_btnSearchReq);
        _jsonRequest.Dock = DockStyle.Fill;
        ConfigureRawTextBox(_rawRequest);
        _requestContainer.Controls.Add(_rawRequest);
        _requestContainer.Controls.Add(_jsonRequest);
        _requestContainer.Controls.Add(_requestToolbar);
        _split.Panel1.Controls.Add(_requestContainer);

        _responseContainer.Dock = DockStyle.Fill;
        _responseToolbar.Dock = DockStyle.Top;
        _responseToolbar.Height = 26;
        _lblResponseTitle.Text = Language.ResponseBody;
        _lblResponseTitle.Location = new Point(0, 4);
        _lblResponseTitle.AutoSize = true;
        _lblResponseTitle.Font = new Font("Consolas", 9F, FontStyle.Bold);
        _btnToggleResponse.Text = Language.Raw;
        _btnToggleResponse.SetBounds(110, 2, 42, 23);
        _btnExpandRes.Text = Language.Expand;
        _btnExpandRes.SetBounds(158, 2, 50, 23);
        _btnCollapseRes.Text = Language.Collapse;
        _btnCollapseRes.SetBounds(210, 2, 55, 23);
        _btnLvl2Res.Text = Language.CollapseLevel2;
        _btnLvl2Res.SetBounds(267, 2, 36, 23);
        _txtSearchRes.SetBounds(310, 3, 80, 23);
        _txtSearchRes.PlaceholderText = Language.SearchPlaceholder;
        _btnSearchRes.Text = "\u25B6";
        _btnSearchRes.SetBounds(392, 2, 22, 23);
        _responseToolbar.Controls.Add(_lblResponseTitle);
        _responseToolbar.Controls.Add(_btnToggleResponse);
        _responseToolbar.Controls.Add(_btnExpandRes);
        _responseToolbar.Controls.Add(_btnCollapseRes);
        _responseToolbar.Controls.Add(_btnLvl2Res);
        _responseToolbar.Controls.Add(_txtSearchRes);
        _responseToolbar.Controls.Add(_btnSearchRes);
        _jsonResponse.Dock = DockStyle.Fill;
        ConfigureRawTextBox(_rawResponse);
        _responseContainer.Controls.Add(_rawResponse);
        _responseContainer.Controls.Add(_jsonResponse);
        _responseContainer.Controls.Add(_responseToolbar);
        _split.Panel2.Controls.Add(_responseContainer);

        Controls.Add(_split);

        _split.Panel2.ResumeLayout(false);
        _split.Panel1.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        ResumeLayout(false);
    }

    private static void ConfigureRawTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ReadOnly = true;
        textBox.WordWrap = false;
        textBox.ScrollBars = ScrollBars.Both;
        textBox.BackColor = Color.White;
        textBox.Visible = false;
    }
}
