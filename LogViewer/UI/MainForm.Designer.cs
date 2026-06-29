namespace LogViewer.UI;

public partial class MainForm
{
    private System.Windows.Forms.ToolStripMenuItem _toolsMenuItem;
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.ToolStripMenuItem _settingsMenuItem;
    private System.Windows.Forms.Panel _logPanel;
    private System.Windows.Forms.Panel _detailPanel;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _toolsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        _settingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        _menuStrip = new System.Windows.Forms.MenuStrip();
        _toolStrip = new System.Windows.Forms.ToolStrip();
        _btnAdbReverse = new System.Windows.Forms.ToolStripDropDownButton();
        _lblStatus = new System.Windows.Forms.ToolStripLabel();
        _outerSplit = new System.Windows.Forms.SplitContainer();
        _devicePanel = new LogViewer.UI.DevicePanel();
        _innerSplit = new System.Windows.Forms.SplitContainer();
        _logPanel = new System.Windows.Forms.Panel();
        _tabLogType = new System.Windows.Forms.TabControl();
        _tabNetwork = new System.Windows.Forms.TabPage();
        _tabNormal = new System.Windows.Forms.TabPage();
        _tabSystem = new System.Windows.Forms.TabPage();
        _detailPanel = new System.Windows.Forms.Panel();
        _tabDetail = new System.Windows.Forms.TabControl();
        _tabHeaders = new System.Windows.Forms.TabPage();
        _rawHeaders = new System.Windows.Forms.TextBox();
        _jsonHeaders = new System.Windows.Forms.Panel();
        _tabRequestBody = new System.Windows.Forms.TabPage();
        _rawRequestBody = new System.Windows.Forms.TextBox();
        _jsonRequestBody = new System.Windows.Forms.Panel();
        _tabResponseBody = new System.Windows.Forms.TabPage();
        _rawResponseBody = new System.Windows.Forms.TextBox();
        _jsonResponseBody = new System.Windows.Forms.Panel();
        _jsonDetailToolbar = new LogViewer.UI.JsonDetailToolbar();
        _pnlBottomBar = new System.Windows.Forms.FlowLayoutPanel();
        _btnClear = new System.Windows.Forms.Button();
        _btnExportJson = new System.Windows.Forms.Button();
        _btnExportTxt = new System.Windows.Forms.Button();
        _statusStrip = new System.Windows.Forms.StatusStrip();
        _lblServerStatus = new System.Windows.Forms.ToolStripStatusLabel();
        _lblDeviceCountStatus = new System.Windows.Forms.ToolStripStatusLabel();
        _lblAdbStatus = new System.Windows.Forms.ToolStripStatusLabel();
        _lblLogcatStatus = new System.Windows.Forms.ToolStripStatusLabel();
        _menuStrip.SuspendLayout();
        _toolStrip.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_outerSplit).BeginInit();
        _outerSplit.Panel1.SuspendLayout();
        _outerSplit.Panel2.SuspendLayout();
        _outerSplit.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_innerSplit).BeginInit();
        _innerSplit.Panel1.SuspendLayout();
        _innerSplit.Panel2.SuspendLayout();
        _innerSplit.SuspendLayout();
        _logPanel.SuspendLayout();
        _tabLogType.SuspendLayout();
        _detailPanel.SuspendLayout();
        _tabDetail.SuspendLayout();
        _tabHeaders.SuspendLayout();
        _tabRequestBody.SuspendLayout();
        _tabResponseBody.SuspendLayout();
        _pnlBottomBar.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _toolsMenuItem
        // 
        _toolsMenuItem.AccessibleName = "_toolsMenuItem";
        _toolsMenuItem.DoubleClickEnabled = true;
        _toolsMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { _settingsMenuItem });
        _toolsMenuItem.Name = "_toolsMenuItem";
        _toolsMenuItem.Size = new System.Drawing.Size(52, 21);
        _toolsMenuItem.Text = "&Tools";
        // 
        // _settingsMenuItem
        // 
        _settingsMenuItem.Name = "_settingsMenuItem";
        _settingsMenuItem.Size = new System.Drawing.Size(131, 22);
        _settingsMenuItem.Text = "&Settings...";
        // 
        // _menuStrip
        // 
        _menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { _toolsMenuItem });
        _menuStrip.Location = new System.Drawing.Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new System.Drawing.Size(1264, 25);
        _menuStrip.TabIndex = 2;
        // 
        // _toolStrip
        // 
        _toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { _btnAdbReverse, _lblStatus });
        _toolStrip.Location = new System.Drawing.Point(0, 25);
        _toolStrip.Name = "_toolStrip";
        _toolStrip.Size = new System.Drawing.Size(1264, 25);
        _toolStrip.TabIndex = 1;
        // 
        // _btnAdbReverse
        // 
        _btnAdbReverse.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
        _btnAdbReverse.Name = "_btnAdbReverse";
        _btnAdbReverse.Size = new System.Drawing.Size(96, 22);
        _btnAdbReverse.Text = "ADB Reverse";
        // 
        // _lblStatus
        // 
        _lblStatus.ForeColor = System.Drawing.Color.Green;
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new System.Drawing.Size(67, 22);
        _lblStatus.Text = "● Running";
        // 
        // _outerSplit
        // 
        _outerSplit.Dock = System.Windows.Forms.DockStyle.Fill;
        _outerSplit.Location = new System.Drawing.Point(0, 50);
        _outerSplit.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _outerSplit.Name = "_outerSplit";
        // 
        // _outerSplit.Panel1
        // 
        _outerSplit.Panel1.Controls.Add(_devicePanel);
        _outerSplit.Panel1MinSize = 60;
        // 
        // _outerSplit.Panel2
        // 
        _outerSplit.Panel2.Controls.Add(_innerSplit);
        _outerSplit.Size = new System.Drawing.Size(1264, 372);
        _outerSplit.SplitterDistance = 273;
        _outerSplit.TabIndex = 0;
        // 
        // _devicePanel
        // 
        _devicePanel.BackColor = System.Drawing.SystemColors.Control;
        _devicePanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _devicePanel.Location = new System.Drawing.Point(0, 0);
        _devicePanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _devicePanel.Name = "_devicePanel";
        _devicePanel.Padding = new System.Windows.Forms.Padding(8, 2, 8, 2);
        _devicePanel.Size = new System.Drawing.Size(273, 372);
        _devicePanel.TabIndex = 0;
        // 
        // _innerSplit
        // 
        _innerSplit.Dock = System.Windows.Forms.DockStyle.Fill;
        _innerSplit.Location = new System.Drawing.Point(0, 0);
        _innerSplit.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _innerSplit.Name = "_innerSplit";
        // 
        // _innerSplit.Panel1
        // 
        _innerSplit.Panel1.Controls.Add(_logPanel);
        _innerSplit.Panel1MinSize = 250;
        // 
        // _innerSplit.Panel2
        // 
        _innerSplit.Panel2.Controls.Add(_detailPanel);
        _innerSplit.Size = new System.Drawing.Size(987, 372);
        _innerSplit.SplitterDistance = 614;
        _innerSplit.TabIndex = 0;
        // 
        // _logPanel
        // 
        _logPanel.Controls.Add(_tabLogType);
        _logPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _logPanel.Location = new System.Drawing.Point(0, 0);
        _logPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _logPanel.Name = "_logPanel";
        _logPanel.Size = new System.Drawing.Size(614, 372);
        _logPanel.TabIndex = 0;
        // 
        // _tabLogType
        // 
        _tabLogType.Controls.Add(_tabNetwork);
        _tabLogType.Controls.Add(_tabNormal);
        _tabLogType.Controls.Add(_tabSystem);
        _tabLogType.Dock = System.Windows.Forms.DockStyle.Fill;
        _tabLogType.Location = new System.Drawing.Point(0, 0);
        _tabLogType.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabLogType.Name = "_tabLogType";
        _tabLogType.SelectedIndex = 0;
        _tabLogType.Size = new System.Drawing.Size(614, 372);
        _tabLogType.TabIndex = 0;
        // 
        // _tabNetwork
        // 
        _tabNetwork.Location = new System.Drawing.Point(4, 23);
        _tabNetwork.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNetwork.Name = "_tabNetwork";
        _tabNetwork.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNetwork.Size = new System.Drawing.Size(606, 345);
        _tabNetwork.TabIndex = 0;
        _tabNetwork.Text = "Network Logs";
        _tabNetwork.UseVisualStyleBackColor = true;
        // 
        // _tabNormal
        // 
        _tabNormal.Location = new System.Drawing.Point(4, 26);
        _tabNormal.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNormal.Name = "_tabNormal";
        _tabNormal.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNormal.Size = new System.Drawing.Size(606, 273);
        _tabNormal.TabIndex = 1;
        _tabNormal.Text = "Normal Logs";
        _tabNormal.UseVisualStyleBackColor = true;
        // 
        // _tabSystem
        // 
        _tabSystem.Location = new System.Drawing.Point(4, 26);
        _tabSystem.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabSystem.Name = "_tabSystem";
        _tabSystem.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabSystem.Size = new System.Drawing.Size(606, 273);
        _tabSystem.TabIndex = 1;
        _tabSystem.Text = "System Logs";
        _tabSystem.UseVisualStyleBackColor = true;
        // 
        // _detailPanel
        // 
        _detailPanel.Controls.Add(_tabDetail);
        _detailPanel.Controls.Add(_jsonDetailToolbar);
        _detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _detailPanel.Location = new System.Drawing.Point(0, 0);
        _detailPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _detailPanel.Name = "_detailPanel";
        _detailPanel.Size = new System.Drawing.Size(369, 372);
        _detailPanel.TabIndex = 0;
        // 
        // _tabDetail
        // 
        _tabDetail.Controls.Add(_tabHeaders);
        _tabDetail.Controls.Add(_tabRequestBody);
        _tabDetail.Controls.Add(_tabResponseBody);
        _tabDetail.Dock = System.Windows.Forms.DockStyle.Fill;
        _tabDetail.Location = new System.Drawing.Point(0, 37);
        _tabDetail.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabDetail.Name = "_tabDetail";
        _tabDetail.SelectedIndex = 0;
        _tabDetail.Size = new System.Drawing.Size(369, 335);
        _tabDetail.TabIndex = 0;
        // 
        // _tabHeaders
        // 
        _tabHeaders.Controls.Add(_rawHeaders);
        _tabHeaders.Controls.Add(_jsonHeaders);
        _tabHeaders.Location = new System.Drawing.Point(4, 23);
        _tabHeaders.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabHeaders.Name = "_tabHeaders";
        _tabHeaders.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabHeaders.Size = new System.Drawing.Size(361, 308);
        _tabHeaders.TabIndex = 0;
        _tabHeaders.Text = "Headers";
        _tabHeaders.UseVisualStyleBackColor = true;
        // 
        // _rawHeaders
        // 
        _rawHeaders.BackColor = System.Drawing.Color.White;
        _rawHeaders.Dock = System.Windows.Forms.DockStyle.Fill;
        _rawHeaders.Location = new System.Drawing.Point(3, 2);
        _rawHeaders.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _rawHeaders.Multiline = true;
        _rawHeaders.Name = "_rawHeaders";
        _rawHeaders.ReadOnly = true;
        _rawHeaders.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _rawHeaders.Size = new System.Drawing.Size(355, 304);
        _rawHeaders.TabIndex = 0;
        _rawHeaders.Visible = false;
        _rawHeaders.WordWrap = false;
        // 
        // _jsonHeaders
        // 
        _jsonHeaders.Dock = System.Windows.Forms.DockStyle.Fill;
        _jsonHeaders.Location = new System.Drawing.Point(3, 2);
        _jsonHeaders.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _jsonHeaders.Name = "_jsonHeaders";
        _jsonHeaders.Size = new System.Drawing.Size(355, 304);
        _jsonHeaders.TabIndex = 1;
        // 
        // _tabRequestBody
        // 
        _tabRequestBody.Controls.Add(_rawRequestBody);
        _tabRequestBody.Controls.Add(_jsonRequestBody);
        _tabRequestBody.Location = new System.Drawing.Point(4, 26);
        _tabRequestBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabRequestBody.Name = "_tabRequestBody";
        _tabRequestBody.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabRequestBody.Size = new System.Drawing.Size(361, 257);
        _tabRequestBody.TabIndex = 1;
        _tabRequestBody.Text = "Request Body";
        _tabRequestBody.UseVisualStyleBackColor = true;
        // 
        // _rawRequestBody
        // 
        _rawRequestBody.BackColor = System.Drawing.Color.White;
        _rawRequestBody.Dock = System.Windows.Forms.DockStyle.Fill;
        _rawRequestBody.Location = new System.Drawing.Point(3, 2);
        _rawRequestBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _rawRequestBody.Multiline = true;
        _rawRequestBody.Name = "_rawRequestBody";
        _rawRequestBody.ReadOnly = true;
        _rawRequestBody.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _rawRequestBody.Size = new System.Drawing.Size(355, 253);
        _rawRequestBody.TabIndex = 0;
        _rawRequestBody.Visible = false;
        _rawRequestBody.WordWrap = false;
        // 
        // _jsonRequestBody
        // 
        _jsonRequestBody.Dock = System.Windows.Forms.DockStyle.Fill;
        _jsonRequestBody.Location = new System.Drawing.Point(3, 2);
        _jsonRequestBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _jsonRequestBody.Name = "_jsonRequestBody";
        _jsonRequestBody.Size = new System.Drawing.Size(355, 253);
        _jsonRequestBody.TabIndex = 1;
        // 
        // _tabResponseBody
        // 
        _tabResponseBody.Controls.Add(_rawResponseBody);
        _tabResponseBody.Controls.Add(_jsonResponseBody);
        _tabResponseBody.Location = new System.Drawing.Point(4, 26);
        _tabResponseBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabResponseBody.Name = "_tabResponseBody";
        _tabResponseBody.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabResponseBody.Size = new System.Drawing.Size(361, 257);
        _tabResponseBody.TabIndex = 2;
        _tabResponseBody.Text = "Response Body";
        _tabResponseBody.UseVisualStyleBackColor = true;
        // 
        // _rawResponseBody
        // 
        _rawResponseBody.BackColor = System.Drawing.Color.White;
        _rawResponseBody.Dock = System.Windows.Forms.DockStyle.Fill;
        _rawResponseBody.Location = new System.Drawing.Point(3, 2);
        _rawResponseBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _rawResponseBody.Multiline = true;
        _rawResponseBody.Name = "_rawResponseBody";
        _rawResponseBody.ReadOnly = true;
        _rawResponseBody.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _rawResponseBody.Size = new System.Drawing.Size(355, 253);
        _rawResponseBody.TabIndex = 0;
        _rawResponseBody.Visible = false;
        _rawResponseBody.WordWrap = false;
        // 
        // _jsonResponseBody
        // 
        _jsonResponseBody.Dock = System.Windows.Forms.DockStyle.Fill;
        _jsonResponseBody.Location = new System.Drawing.Point(3, 2);
        _jsonResponseBody.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _jsonResponseBody.Name = "_jsonResponseBody";
        _jsonResponseBody.Size = new System.Drawing.Size(355, 253);
        _jsonResponseBody.TabIndex = 1;
        // 
        // _jsonDetailToolbar
        // 
        _jsonDetailToolbar.Dock = System.Windows.Forms.DockStyle.Top;
        _jsonDetailToolbar.Location = new System.Drawing.Point(0, 0);
        _jsonDetailToolbar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _jsonDetailToolbar.Name = "_jsonDetailToolbar";
        _jsonDetailToolbar.Size = new System.Drawing.Size(369, 37);
        _jsonDetailToolbar.TabIndex = 1;
        // 
        // _pnlBottomBar
        // 
        _pnlBottomBar.Controls.Add(_btnClear);
        _pnlBottomBar.Controls.Add(_btnExportJson);
        _pnlBottomBar.Controls.Add(_btnExportTxt);
        _pnlBottomBar.Dock = System.Windows.Forms.DockStyle.Bottom;
        _pnlBottomBar.Location = new System.Drawing.Point(0, 422);
        _pnlBottomBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _pnlBottomBar.Name = "_pnlBottomBar";
        _pnlBottomBar.Padding = new System.Windows.Forms.Padding(5, 2, 5, 2);
        _pnlBottomBar.Size = new System.Drawing.Size(1264, 17);
        _pnlBottomBar.TabIndex = 3;
        // 
        // _btnClear
        // 
        _btnClear.AutoSize = true;
        _btnClear.Location = new System.Drawing.Point(8, 4);
        _btnClear.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnClear.Name = "_btnClear";
        _btnClear.Size = new System.Drawing.Size(75, 24);
        _btnClear.TabIndex = 0;
        _btnClear.Text = "Clear";
        // 
        // _btnExportJson
        // 
        _btnExportJson.AutoSize = true;
        _btnExportJson.Location = new System.Drawing.Point(89, 4);
        _btnExportJson.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnExportJson.Name = "_btnExportJson";
        _btnExportJson.Size = new System.Drawing.Size(94, 24);
        _btnExportJson.TabIndex = 1;
        _btnExportJson.Text = "Export JSON";
        // 
        // _btnExportTxt
        // 
        _btnExportTxt.AutoSize = true;
        _btnExportTxt.Location = new System.Drawing.Point(189, 4);
        _btnExportTxt.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnExportTxt.Name = "_btnExportTxt";
        _btnExportTxt.Size = new System.Drawing.Size(87, 24);
        _btnExportTxt.TabIndex = 2;
        _btnExportTxt.Text = "Export TXT";
        // 
        // _statusStrip
        // 
        _statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { _lblServerStatus, _lblDeviceCountStatus, _lblAdbStatus, _lblLogcatStatus });
        _statusStrip.Location = new System.Drawing.Point(0, 439);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new System.Drawing.Size(1264, 22);
        _statusStrip.TabIndex = 4;
        // 
        // _lblServerStatus
        // 
        _lblServerStatus.Name = "_lblServerStatus";
        _lblServerStatus.Size = new System.Drawing.Size(102, 17);
        _lblServerStatus.Text = "Server: Stopped";
        // 
        // _lblDeviceCountStatus
        // 
        _lblDeviceCountStatus.Name = "_lblDeviceCountStatus";
        _lblDeviceCountStatus.Size = new System.Drawing.Size(66, 17);
        _lblDeviceCountStatus.Text = "Devices: 0";
        // 
        // _lblAdbStatus
        // 
        _lblAdbStatus.Name = "_lblAdbStatus";
        _lblAdbStatus.Size = new System.Drawing.Size(117, 17);
        _lblAdbStatus.Text = "ADB: Not detected";
        // 
        // _lblLogcatStatus
        // 
        _lblLogcatStatus.Name = "_lblLogcatStatus";
        _lblLogcatStatus.Size = new System.Drawing.Size(61, 17);
        _lblLogcatStatus.Text = "Logcat: 0";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        BackColor = System.Drawing.SystemColors.Control;
        ClientSize = new System.Drawing.Size(1264, 461);
        Controls.Add(_outerSplit);
        Controls.Add(_toolStrip);
        Controls.Add(_menuStrip);
        Controls.Add(_pnlBottomBar);
        Controls.Add(_statusStrip);
        Font = new System.Drawing.Font("Consolas", 9F);
        Location = new System.Drawing.Point(15, 15);
        MinimumSize = new System.Drawing.Size(900, 500);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        _toolStrip.ResumeLayout(false);
        _toolStrip.PerformLayout();
        _outerSplit.Panel1.ResumeLayout(false);
        _outerSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_outerSplit).EndInit();
        _outerSplit.ResumeLayout(false);
        _innerSplit.Panel1.ResumeLayout(false);
        _innerSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_innerSplit).EndInit();
        _innerSplit.ResumeLayout(false);
        _logPanel.ResumeLayout(false);
        _tabLogType.ResumeLayout(false);
        _detailPanel.ResumeLayout(false);
        _tabDetail.ResumeLayout(false);
        _tabHeaders.ResumeLayout(false);
        _tabHeaders.PerformLayout();
        _tabRequestBody.ResumeLayout(false);
        _tabRequestBody.PerformLayout();
        _tabResponseBody.ResumeLayout(false);
        _tabResponseBody.PerformLayout();
        _pnlBottomBar.ResumeLayout(false);
        _pnlBottomBar.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
