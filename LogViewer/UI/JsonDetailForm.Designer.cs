using LogViewer.Static;

namespace LogViewer.UI;

public partial class JsonDetailForm
{
    private System.Windows.Forms.Panel _requestContainer;
    private System.Windows.Forms.Panel _requestToolbar;
    private Label _lblRequestTitle = null!;
    private System.Windows.Forms.Panel _responseContainer;
    private System.Windows.Forms.Panel _responseToolbar;
    private Label _lblResponseTitle = null!;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _split = new System.Windows.Forms.SplitContainer();
        _requestContainer = new System.Windows.Forms.Panel();
        _rawRequest = new System.Windows.Forms.TextBox();
        _jsonRequest = new LogViewer.UI.JsonTreeView();
        _requestToolbar = new System.Windows.Forms.Panel();
        _lblRequestTitle = new System.Windows.Forms.Label();
        _btnToggleRequest = new System.Windows.Forms.Button();
        _btnExpandReq = new System.Windows.Forms.Button();
        _btnCollapseReq = new System.Windows.Forms.Button();
        _btnLvl2Req = new System.Windows.Forms.Button();
        _txtSearchReq = new System.Windows.Forms.TextBox();
        _btnSearchReq = new System.Windows.Forms.Button();
        _responseContainer = new System.Windows.Forms.Panel();
        _rawResponse = new System.Windows.Forms.TextBox();
        _jsonResponse = new LogViewer.UI.JsonTreeView();
        _responseToolbar = new System.Windows.Forms.Panel();
        _lblResponseTitle = new System.Windows.Forms.Label();
        _btnToggleResponse = new System.Windows.Forms.Button();
        _btnExpandRes = new System.Windows.Forms.Button();
        _btnCollapseRes = new System.Windows.Forms.Button();
        _btnLvl2Res = new System.Windows.Forms.Button();
        _txtSearchRes = new System.Windows.Forms.TextBox();
        _btnSearchRes = new System.Windows.Forms.Button();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        _requestContainer.SuspendLayout();
        _requestToolbar.SuspendLayout();
        _responseContainer.SuspendLayout();
        _responseToolbar.SuspendLayout();
        SuspendLayout();
        // 
        // _split
        // 
        _split.Dock = System.Windows.Forms.DockStyle.Fill;
        _split.Location = new System.Drawing.Point(0, 0);
        _split.Name = "_split";
        // 
        // _split.Panel1
        // 
        _split.Panel1.Controls.Add(_requestContainer);
        _split.Panel1MinSize = 150;
        // 
        // _split.Panel2
        // 
        _split.Panel2.Controls.Add(_responseContainer);
        _split.Size = new System.Drawing.Size(984, 561);
        _split.SplitterDistance = 432;
        _split.TabIndex = 0;
        // 
        // _requestContainer
        // 
        _requestContainer.Controls.Add(_rawRequest);
        _requestContainer.Controls.Add(_jsonRequest);
        _requestContainer.Controls.Add(_requestToolbar);
        _requestContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _requestContainer.Location = new System.Drawing.Point(0, 0);
        _requestContainer.Name = "_requestContainer";
        _requestContainer.Size = new System.Drawing.Size(432, 561);
        _requestContainer.TabIndex = 0;
        // 
        // _rawRequest
        // 
        _rawRequest.BackColor = System.Drawing.SystemColors.Window;
        _rawRequest.Dock = System.Windows.Forms.DockStyle.Fill;
        _rawRequest.Location = new System.Drawing.Point(0, 26);
        _rawRequest.Multiline = true;
        _rawRequest.Name = "_rawRequest";
        _rawRequest.ReadOnly = true;
        _rawRequest.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _rawRequest.Size = new System.Drawing.Size(432, 535);
        _rawRequest.TabIndex = 0;
        _rawRequest.Visible = false;
        _rawRequest.WordWrap = false;
        // 
        // _jsonRequest
        // 
        _jsonRequest.BackColor = System.Drawing.SystemColors.Window;
        _jsonRequest.Dock = System.Windows.Forms.DockStyle.Fill;
        _jsonRequest.Font = new System.Drawing.Font("Consolas", 11F);
        _jsonRequest.Location = new System.Drawing.Point(0, 26);
        _jsonRequest.Name = "_jsonRequest";
        _jsonRequest.Size = new System.Drawing.Size(432, 535);
        _jsonRequest.TabIndex = 1;
        // 
        // _requestToolbar
        // 
        _requestToolbar.Controls.Add(_lblRequestTitle);
        _requestToolbar.Controls.Add(_btnToggleRequest);
        _requestToolbar.Controls.Add(_btnExpandReq);
        _requestToolbar.Controls.Add(_btnCollapseReq);
        _requestToolbar.Controls.Add(_btnLvl2Req);
        _requestToolbar.Controls.Add(_txtSearchReq);
        _requestToolbar.Controls.Add(_btnSearchReq);
        _requestToolbar.Dock = System.Windows.Forms.DockStyle.Top;
        _requestToolbar.Location = new System.Drawing.Point(0, 0);
        _requestToolbar.Name = "_requestToolbar";
        _requestToolbar.Size = new System.Drawing.Size(432, 26);
        _requestToolbar.TabIndex = 2;
        // 
        // _lblRequestTitle
        // 
        _lblRequestTitle.AutoSize = true;
        _lblRequestTitle.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold);
        _lblRequestTitle.Location = new System.Drawing.Point(0, 4);
        _lblRequestTitle.Name = "_lblRequestTitle";
        _lblRequestTitle.Size = new System.Drawing.Size(52, 14);
        _lblRequestTitle.TabIndex = 0;
        _lblRequestTitle.Text = "请求体";
        // 
        // _btnToggleRequest
        // 
        _btnToggleRequest.Location = new System.Drawing.Point(100, 2);
        _btnToggleRequest.Name = "_btnToggleRequest";
        _btnToggleRequest.Size = new System.Drawing.Size(42, 23);
        _btnToggleRequest.TabIndex = 1;
        _btnToggleRequest.Text = "原文";
        // 
        // _btnExpandReq
        // 
        _btnExpandReq.Location = new System.Drawing.Point(148, 2);
        _btnExpandReq.Name = "_btnExpandReq";
        _btnExpandReq.Size = new System.Drawing.Size(50, 23);
        _btnExpandReq.TabIndex = 2;
        _btnExpandReq.Text = "展开";
        // 
        // _btnCollapseReq
        // 
        _btnCollapseReq.Location = new System.Drawing.Point(200, 2);
        _btnCollapseReq.Name = "_btnCollapseReq";
        _btnCollapseReq.Size = new System.Drawing.Size(55, 23);
        _btnCollapseReq.TabIndex = 3;
        _btnCollapseReq.Text = "折叠";
        // 
        // _btnLvl2Req
        // 
        _btnLvl2Req.Location = new System.Drawing.Point(257, 2);
        _btnLvl2Req.Name = "_btnLvl2Req";
        _btnLvl2Req.Size = new System.Drawing.Size(56, 23);
        _btnLvl2Req.TabIndex = 4;
        _btnLvl2Req.Text = "二级";
        // 
        // _txtSearchReq
        // 
        _txtSearchReq.Location = new System.Drawing.Point(320, 3);
        _txtSearchReq.Name = "_txtSearchReq";
        _txtSearchReq.PlaceholderText = "搜索...";
        _txtSearchReq.Size = new System.Drawing.Size(80, 22);
        _txtSearchReq.TabIndex = 5;
        // 
        // _btnSearchReq
        // 
        _btnSearchReq.Location = new System.Drawing.Point(402, 2);
        _btnSearchReq.Name = "_btnSearchReq";
        _btnSearchReq.Size = new System.Drawing.Size(22, 23);
        _btnSearchReq.TabIndex = 6;
        _btnSearchReq.Text = "▶";
        // 
        // _responseContainer
        // 
        _responseContainer.Controls.Add(_rawResponse);
        _responseContainer.Controls.Add(_jsonResponse);
        _responseContainer.Controls.Add(_responseToolbar);
        _responseContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _responseContainer.Location = new System.Drawing.Point(0, 0);
        _responseContainer.Name = "_responseContainer";
        _responseContainer.Size = new System.Drawing.Size(548, 561);
        _responseContainer.TabIndex = 0;
        // 
        // _rawResponse
        // 
        _rawResponse.BackColor = System.Drawing.SystemColors.Window;
        _rawResponse.Dock = System.Windows.Forms.DockStyle.Fill;
        _rawResponse.Location = new System.Drawing.Point(0, 26);
        _rawResponse.Multiline = true;
        _rawResponse.Name = "_rawResponse";
        _rawResponse.ReadOnly = true;
        _rawResponse.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _rawResponse.Size = new System.Drawing.Size(548, 535);
        _rawResponse.TabIndex = 0;
        _rawResponse.Visible = false;
        _rawResponse.WordWrap = false;
        // 
        // _jsonResponse
        // 
        _jsonResponse.BackColor = System.Drawing.SystemColors.Window;
        _jsonResponse.Dock = System.Windows.Forms.DockStyle.Fill;
        _jsonResponse.Font = new System.Drawing.Font("Consolas", 11F);
        _jsonResponse.Location = new System.Drawing.Point(0, 26);
        _jsonResponse.Name = "_jsonResponse";
        _jsonResponse.Size = new System.Drawing.Size(548, 535);
        _jsonResponse.TabIndex = 1;
        // 
        // _responseToolbar
        // 
        _responseToolbar.Controls.Add(_lblResponseTitle);
        _responseToolbar.Controls.Add(_btnToggleResponse);
        _responseToolbar.Controls.Add(_btnExpandRes);
        _responseToolbar.Controls.Add(_btnCollapseRes);
        _responseToolbar.Controls.Add(_btnLvl2Res);
        _responseToolbar.Controls.Add(_txtSearchRes);
        _responseToolbar.Controls.Add(_btnSearchRes);
        _responseToolbar.Dock = System.Windows.Forms.DockStyle.Top;
        _responseToolbar.Location = new System.Drawing.Point(0, 0);
        _responseToolbar.Name = "_responseToolbar";
        _responseToolbar.Size = new System.Drawing.Size(548, 26);
        _responseToolbar.TabIndex = 2;
        // 
        // _lblResponseTitle
        // 
        _lblResponseTitle.AutoSize = true;
        _lblResponseTitle.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold);
        _lblResponseTitle.Location = new System.Drawing.Point(0, 4);
        _lblResponseTitle.Name = "_lblResponseTitle";
        _lblResponseTitle.Size = new System.Drawing.Size(52, 14);
        _lblResponseTitle.TabIndex = 0;
        _lblResponseTitle.Text = "响应体";
        // 
        // _btnToggleResponse
        // 
        _btnToggleResponse.Location = new System.Drawing.Point(110, 2);
        _btnToggleResponse.Name = "_btnToggleResponse";
        _btnToggleResponse.Size = new System.Drawing.Size(42, 23);
        _btnToggleResponse.TabIndex = 1;
        _btnToggleResponse.Text = "原文";
        // 
        // _btnExpandRes
        // 
        _btnExpandRes.Location = new System.Drawing.Point(158, 2);
        _btnExpandRes.Name = "_btnExpandRes";
        _btnExpandRes.Size = new System.Drawing.Size(50, 23);
        _btnExpandRes.TabIndex = 2;
        _btnExpandRes.Text = "展开";
        // 
        // _btnCollapseRes
        // 
        _btnCollapseRes.Location = new System.Drawing.Point(210, 2);
        _btnCollapseRes.Name = "_btnCollapseRes";
        _btnCollapseRes.Size = new System.Drawing.Size(55, 23);
        _btnCollapseRes.TabIndex = 3;
        _btnCollapseRes.Text = "折叠";
        // 
        // _btnLvl2Res
        // 
        _btnLvl2Res.Location = new System.Drawing.Point(267, 2);
        _btnLvl2Res.Name = "_btnLvl2Res";
        _btnLvl2Res.Size = new System.Drawing.Size(56, 23);
        _btnLvl2Res.TabIndex = 4;
        _btnLvl2Res.Text = "二级";
        // 
        // _txtSearchRes
        // 
        _txtSearchRes.Location = new System.Drawing.Point(320, 3);
        _txtSearchRes.Name = "_txtSearchRes";
        _txtSearchRes.PlaceholderText = "搜索...";
        _txtSearchRes.Size = new System.Drawing.Size(80, 22);
        _txtSearchRes.TabIndex = 5;
        // 
        // _btnSearchRes
        // 
        _btnSearchRes.Location = new System.Drawing.Point(402, 2);
        _btnSearchRes.Name = "_btnSearchRes";
        _btnSearchRes.Size = new System.Drawing.Size(22, 23);
        _btnSearchRes.TabIndex = 6;
        _btnSearchRes.Text = "▶";
        // 
        // JsonDetailForm
        // 
        ClientSize = new System.Drawing.Size(984, 561);
        Controls.Add(_split);
        Font = new System.Drawing.Font("Consolas", 9F);
        KeyPreview = true;
        MinimumSize = new System.Drawing.Size(600, 400);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        _split.Panel1.ResumeLayout(false);
        _split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        _requestContainer.ResumeLayout(false);
        _requestContainer.PerformLayout();
        _requestToolbar.ResumeLayout(false);
        _requestToolbar.PerformLayout();
        _responseContainer.ResumeLayout(false);
        _responseContainer.PerformLayout();
        _responseToolbar.ResumeLayout(false);
        _responseToolbar.PerformLayout();
        ResumeLayout(false);
    }

}
