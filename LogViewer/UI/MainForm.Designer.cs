namespace LogViewer.UI;

public partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private ToolStripMenuItem _settingsMenuItem = null!;
    private Panel _logPanel = null!;
    private Panel _networkTabContainer = null!;
    private Panel _networkActionBar = null!;
    private Panel _systemTabContainer = null!;
    private Panel _detailPanel = null!;

    private void InitializeComponent()
    {
        var toolsMenu = new ToolStripMenuItem();

        _menuStrip = new MenuStrip();
        _settingsMenuItem = new ToolStripMenuItem();
        _toolStrip = new ToolStrip();
        _btnAdbReverse = new ToolStripDropDownButton();
        _lblStatus = new ToolStripLabel();
        _outerSplit = new SplitContainer();
        _devicePanel = new DevicePanel();
        _innerSplit = new SplitContainer();
        _logPanel = new Panel();
        _tabLogType = new TabControl();
        _tabNetwork = new TabPage();
        _networkTabContainer = new Panel();
        _lstNetworkLogs = new ListView();
        _networkActionBar = new Panel();
        _btnScrollToTop = new Button();
        _btnScrollToBottom = new Button();
        _lblLogCount = new Label();
        _pnlNetworkFilter = new Panel();
        _txtNetworkKeyword = new TextBox();
        _cmbMethod = new ComboBox();
        _cmbStatusCode = new ComboBox();
        _tabSystem = new TabPage();
        _systemTabContainer = new Panel();
        _lstSystemLogs = new ListView();
        _systemActionBar = new Panel();
        _pnlSystemFilter = new Panel();
        _txtSystemKeyword = new TextBox();
        _cmbLogLevel = new ComboBox();
        _cmbLogTag = new ComboBox();
        _btnSystemScrollToTop = new Button();
        _btnSystemScrollToBottom = new Button();
        _btnSystemPauseResume = new Button();
        _lblSystemBacklog = new Label();
        _detailPanel = new Panel();
        _tabDetail = new TabControl();
        _tabHeaders = new TabPage();
        _rawHeaders = new TextBox();
        _jsonHeaders = new JsonTreeView();
        _tabRequestBody = new TabPage();
        _rawRequestBody = new TextBox();
        _jsonRequestBody = new JsonTreeView();
        _tabResponseBody = new TabPage();
        _rawResponseBody = new TextBox();
        _jsonResponseBody = new JsonTreeView();
        _pnlJsonToolbar = new Panel();
        _txtJsonSearch = new TextBox();
        _btnJsonSearch = new Button();
        _btnExpandAll = new Button();
        _btnCollapseAll = new Button();
        _btnCollapseTo2 = new Button();
        _btnToggleView = new Button();
        _pnlBottomBar = new FlowLayoutPanel();
        _btnClear = new Button();
        _btnExportJson = new Button();
        _btnExportTxt = new Button();
        _statusStrip = new StatusStrip();
        _lblServerStatus = new ToolStripStatusLabel();
        _lblDeviceCountStatus = new ToolStripStatusLabel();
        _lblAdbStatus = new ToolStripStatusLabel();
        _lblLogcatStatus = new ToolStripStatusLabel();

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
        _tabNetwork.SuspendLayout();
        _networkTabContainer.SuspendLayout();
        _networkActionBar.SuspendLayout();
        _pnlNetworkFilter.SuspendLayout();
        _tabSystem.SuspendLayout();
        _systemTabContainer.SuspendLayout();
        _pnlSystemFilter.SuspendLayout();
        _detailPanel.SuspendLayout();
        _tabDetail.SuspendLayout();
        _tabHeaders.SuspendLayout();
        _tabRequestBody.SuspendLayout();
        _tabResponseBody.SuspendLayout();
        _pnlJsonToolbar.SuspendLayout();
        _pnlBottomBar.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();

        _menuStrip.Items.AddRange(new ToolStripItem[] { toolsMenu });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1264, 25);
        _menuStrip.TabIndex = 2;

        toolsMenu.DropDownItems.AddRange(new ToolStripItem[] { _settingsMenuItem });
        toolsMenu.Name = "toolsMenu";
        toolsMenu.Size = new Size(52, 21);
        toolsMenu.Text = "&Tools";

        _settingsMenuItem.Name = "_settingsMenuItem";
        _settingsMenuItem.Size = new Size(143, 22);
        _settingsMenuItem.Text = "&Settings...";

        _toolStrip.Items.AddRange(new ToolStripItem[] { _btnAdbReverse, _lblStatus });
        _toolStrip.Location = new Point(0, 25);
        _toolStrip.Name = "_toolStrip";
        _toolStrip.Size = new Size(1264, 25);
        _toolStrip.TabIndex = 1;

        _btnAdbReverse.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _btnAdbReverse.Name = "_btnAdbReverse";
        _btnAdbReverse.Size = new Size(96, 22);
        _btnAdbReverse.Text = "ADB Reverse";

        _lblStatus.ForeColor = Color.Green;
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(67, 22);
        _lblStatus.Text = "● Running";

        _outerSplit.Dock = DockStyle.Fill;
        _outerSplit.Location = new Point(0, 50);
        _outerSplit.Name = "_outerSplit";
        _outerSplit.Panel1.Controls.Add(_devicePanel);
        _outerSplit.Panel1MinSize = 60;
        _outerSplit.Panel2.Controls.Add(_innerSplit);
        _outerSplit.Size = new Size(1264, 604);
        _outerSplit.SplitterDistance = 100;
        _outerSplit.TabIndex = 0;

        _devicePanel.Dock = DockStyle.Fill;
        _devicePanel.Location = new Point(0, 0);
        _devicePanel.Name = "_devicePanel";
        _devicePanel.Size = new Size(100, 604);
        _devicePanel.TabIndex = 0;

        _innerSplit.Dock = DockStyle.Fill;
        _innerSplit.Location = new Point(0, 0);
        _innerSplit.Name = "_innerSplit";
        _innerSplit.Panel1.Controls.Add(_logPanel);
        _innerSplit.Panel1MinSize = 250;
        _innerSplit.Panel2.Controls.Add(_detailPanel);
        _innerSplit.Size = new Size(1160, 604);
        _innerSplit.SplitterDistance = 750;
        _innerSplit.TabIndex = 0;

        _logPanel.Controls.Add(_tabLogType);
        _logPanel.Dock = DockStyle.Fill;
        _logPanel.Location = new Point(0, 0);
        _logPanel.Name = "_logPanel";
        _logPanel.Size = new Size(935, 604);
        _logPanel.TabIndex = 0;

        _tabLogType.Controls.Add(_tabNetwork);
        _tabLogType.Controls.Add(_tabSystem);
        _tabLogType.Dock = DockStyle.Fill;
        _tabLogType.Location = new Point(0, 0);
        _tabLogType.Name = "_tabLogType";
        _tabLogType.SelectedIndex = 0;
        _tabLogType.Size = new Size(935, 604);
        _tabLogType.TabIndex = 0;

        _tabNetwork.Controls.Add(_networkTabContainer);
        _tabNetwork.Location = new Point(4, 23);
        _tabNetwork.Name = "_tabNetwork";
        _tabNetwork.Padding = new Padding(3);
        _tabNetwork.Size = new Size(927, 577);
        _tabNetwork.TabIndex = 0;
        _tabNetwork.Text = "Network Logs";
        _tabNetwork.UseVisualStyleBackColor = true;

        _networkTabContainer.Controls.Add(_lstNetworkLogs);
        _networkTabContainer.Controls.Add(_networkActionBar);
        _networkTabContainer.Controls.Add(_pnlNetworkFilter);
        _networkTabContainer.Dock = DockStyle.Fill;
        _networkTabContainer.Location = new Point(3, 3);
        _networkTabContainer.Name = "_networkTabContainer";
        _networkTabContainer.Size = new Size(921, 571);
        _networkTabContainer.TabIndex = 0;

        _lstNetworkLogs.Dock = DockStyle.Fill;
        _lstNetworkLogs.FullRowSelect = true;
        _lstNetworkLogs.GridLines = true;
        _lstNetworkLogs.Location = new Point(0, 56);
        _lstNetworkLogs.MultiSelect = false;
        _lstNetworkLogs.Name = "_lstNetworkLogs";
        _lstNetworkLogs.ShowItemToolTips = false;
        _lstNetworkLogs.Size = new Size(921, 515);
        _lstNetworkLogs.TabIndex = 0;
        _lstNetworkLogs.UseCompatibleStateImageBehavior = false;
        _lstNetworkLogs.View = View.Details;
        _lstNetworkLogs.VirtualMode = true;

        _networkActionBar.Controls.Add(_btnScrollToBottom);
        _networkActionBar.Controls.Add(_btnScrollToTop);
        _networkActionBar.Controls.Add(_lblLogCount);
        _networkActionBar.Dock = DockStyle.Top;
        _networkActionBar.Location = new Point(0, 32);
        _networkActionBar.Name = "_networkActionBar";
        _networkActionBar.Size = new Size(921, 24);
        _networkActionBar.TabIndex = 1;

        _btnScrollToTop.AutoSize = true;
        _btnScrollToTop.Dock = DockStyle.Left;
        _btnScrollToTop.FlatStyle = FlatStyle.Flat;
        _btnScrollToTop.Font = new Font("Consolas", 8F);
        _btnScrollToTop.Location = new Point(0, 0);
        _btnScrollToTop.Name = "_btnScrollToTop";
        _btnScrollToTop.Size = new Size(103, 24);
        _btnScrollToTop.TabIndex = 0;
        _btnScrollToTop.Text = "↑ Scroll to Top";

        _btnScrollToBottom.AutoSize = true;
        _btnScrollToBottom.Dock = DockStyle.Left;
        _btnScrollToBottom.FlatStyle = FlatStyle.Flat;
        _btnScrollToBottom.Font = new Font("Consolas", 8F);
        _btnScrollToBottom.Location = new Point(103, 0);
        _btnScrollToBottom.Name = "_btnScrollToBottom";
        _btnScrollToBottom.Size = new Size(127, 24);
        _btnScrollToBottom.TabIndex = 1;
        _btnScrollToBottom.Text = "⬇ Scroll to Bottom";

        _lblLogCount.Dock = DockStyle.Right;
        _lblLogCount.Location = new Point(771, 0);
        _lblLogCount.Name = "_lblLogCount";
        _lblLogCount.Size = new Size(150, 24);
        _lblLogCount.TabIndex = 1;
        _lblLogCount.Text = "Logs: 0/0";
        _lblLogCount.TextAlign = ContentAlignment.MiddleRight;

        _pnlNetworkFilter.BackColor = SystemColors.Control;
        _pnlNetworkFilter.Controls.Add(_txtNetworkKeyword);
        _pnlNetworkFilter.Controls.Add(_cmbMethod);
        _pnlNetworkFilter.Controls.Add(_cmbStatusCode);
        _pnlNetworkFilter.Dock = DockStyle.Top;
        _pnlNetworkFilter.Location = new Point(0, 0);
        _pnlNetworkFilter.Name = "_pnlNetworkFilter";
        _pnlNetworkFilter.Size = new Size(921, 32);
        _pnlNetworkFilter.TabIndex = 2;

        _txtNetworkKeyword.Location = new Point(4, 5);
        _txtNetworkKeyword.Name = "_txtNetworkKeyword";
        _txtNetworkKeyword.PlaceholderText = "Keyword...";
        _txtNetworkKeyword.Size = new Size(220, 22);
        _txtNetworkKeyword.TabIndex = 1;

        _cmbMethod.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMethod.Items.AddRange(new object[] { "All", "GET", "POST", "PUT", "DELETE", "PATCH" });
        _cmbMethod.Location = new Point(285, 5);
        _cmbMethod.Name = "_cmbMethod";
        _cmbMethod.Size = new Size(70, 22);
        _cmbMethod.TabIndex = 2;

        _cmbStatusCode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbStatusCode.Items.AddRange(new object[] { "All", "2xx", "3xx", "4xx", "5xx", "0" });
        _cmbStatusCode.Location = new Point(360, 5);
        _cmbStatusCode.Name = "_cmbStatusCode";
        _cmbStatusCode.Size = new Size(70, 22);
        _cmbStatusCode.TabIndex = 3;

        _tabSystem.Controls.Add(_systemTabContainer);
        _tabSystem.Location = new Point(4, 23);
        _tabSystem.Name = "_tabSystem";
        _tabSystem.Padding = new Padding(3);
        _tabSystem.Size = new Size(927, 577);
        _tabSystem.TabIndex = 1;
        _tabSystem.Text = "System Logs";
        _tabSystem.UseVisualStyleBackColor = true;

        _systemTabContainer.Controls.Add(_lstSystemLogs);
        _systemTabContainer.Controls.Add(_systemActionBar);
        _systemTabContainer.Controls.Add(_pnlSystemFilter);
        _systemTabContainer.Dock = DockStyle.Fill;
        _systemTabContainer.Location = new Point(3, 3);
        _systemTabContainer.Name = "_systemTabContainer";
        _systemTabContainer.Size = new Size(921, 571);
        _systemTabContainer.TabIndex = 0;

        _lstSystemLogs.Dock = DockStyle.Fill;
        _lstSystemLogs.FullRowSelect = true;
        _lstSystemLogs.GridLines = true;
        _lstSystemLogs.Location = new Point(0, 52);
        _lstSystemLogs.MultiSelect = false;
        _lstSystemLogs.Name = "_lstSystemLogs";
        _lstSystemLogs.ShowItemToolTips = false;
        _lstSystemLogs.Size = new Size(921, 519);
        _lstSystemLogs.TabIndex = 0;
        _lstSystemLogs.UseCompatibleStateImageBehavior = false;
        _lstSystemLogs.View = View.Details;
        _lstSystemLogs.VirtualMode = true;

        _systemActionBar.Controls.Add(_lblSystemBacklog);
        _systemActionBar.Controls.Add(_btnSystemPauseResume);
        _systemActionBar.Controls.Add(_btnSystemScrollToBottom);
        _systemActionBar.Controls.Add(_btnSystemScrollToTop);
        _systemActionBar.Dock = DockStyle.Top;
        _systemActionBar.Location = new Point(0, 28);
        _systemActionBar.Name = "_systemActionBar";
        _systemActionBar.Size = new Size(921, 24);
        _systemActionBar.TabIndex = 2;

        _pnlSystemFilter.Controls.Add(_txtSystemKeyword);
        _pnlSystemFilter.Controls.Add(_cmbLogLevel);
        _pnlSystemFilter.Controls.Add(_cmbLogTag);
        _pnlSystemFilter.Dock = DockStyle.Top;
        _pnlSystemFilter.Location = new Point(0, 0);
        _pnlSystemFilter.Name = "_pnlSystemFilter";
        _pnlSystemFilter.Size = new Size(921, 28);
        _pnlSystemFilter.TabIndex = 1;

        _txtSystemKeyword.Location = new Point(0, 3);
        _txtSystemKeyword.Name = "_txtSystemKeyword";
        _txtSystemKeyword.PlaceholderText = "Keyword...";
        _txtSystemKeyword.Size = new Size(120, 22);
        _txtSystemKeyword.TabIndex = 0;

        _cmbLogLevel.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbLogLevel.Items.AddRange(new object[] { "All", "V", "D", "I", "W", "E", "F" });
        _cmbLogLevel.Location = new Point(125, 2);
        _cmbLogLevel.Name = "_cmbLogLevel";
        _cmbLogLevel.Size = new Size(60, 22);
        _cmbLogLevel.TabIndex = 1;

        _cmbLogTag.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbLogTag.Items.AddRange(new object[] { "All" });
        _cmbLogTag.Location = new Point(190, 2);
        _cmbLogTag.Name = "_cmbLogTag";
        _cmbLogTag.Size = new Size(120, 22);
        _cmbLogTag.TabIndex = 2;

        _btnSystemScrollToTop.AutoSize = true;
        _btnSystemScrollToTop.Dock = DockStyle.Left;
        _btnSystemScrollToTop.FlatStyle = FlatStyle.Flat;
        _btnSystemScrollToTop.Font = new Font("Consolas", 8F);
        _btnSystemScrollToTop.Location = new Point(0, 0);
        _btnSystemScrollToTop.Name = "_btnSystemScrollToTop";
        _btnSystemScrollToTop.Size = new Size(103, 24);
        _btnSystemScrollToTop.TabIndex = 0;
        _btnSystemScrollToTop.Text = "↑ Scroll to Top";

        _btnSystemScrollToBottom.AutoSize = true;
        _btnSystemScrollToBottom.Dock = DockStyle.Left;
        _btnSystemScrollToBottom.FlatStyle = FlatStyle.Flat;
        _btnSystemScrollToBottom.Font = new Font("Consolas", 8F);
        _btnSystemScrollToBottom.Location = new Point(103, 0);
        _btnSystemScrollToBottom.Name = "_btnSystemScrollToBottom";
        _btnSystemScrollToBottom.Size = new Size(127, 24);
        _btnSystemScrollToBottom.TabIndex = 1;
        _btnSystemScrollToBottom.Text = "⬇ Scroll to Bottom";

        _btnSystemPauseResume.AutoSize = true;
        _btnSystemPauseResume.Dock = DockStyle.Left;
        _btnSystemPauseResume.FlatStyle = FlatStyle.Flat;
        _btnSystemPauseResume.Font = new Font("Consolas", 8F);
        _btnSystemPauseResume.Location = new Point(230, 0);
        _btnSystemPauseResume.Name = "_btnSystemPauseResume";
        _btnSystemPauseResume.Size = new Size(61, 24);
        _btnSystemPauseResume.TabIndex = 2;
        _btnSystemPauseResume.Text = "Pause";

        _lblSystemBacklog.Dock = DockStyle.Right;
        _lblSystemBacklog.Location = new Point(751, 0);
        _lblSystemBacklog.Name = "_lblSystemBacklog";
        _lblSystemBacklog.Size = new Size(170, 24);
        _lblSystemBacklog.TabIndex = 3;
        _lblSystemBacklog.TextAlign = ContentAlignment.MiddleRight;
        _lblSystemBacklog.Visible = false;

        _detailPanel.Controls.Add(_tabDetail);
        _detailPanel.Controls.Add(_pnlJsonToolbar);
        _detailPanel.Dock = DockStyle.Fill;
        _detailPanel.Location = new Point(0, 0);
        _detailPanel.Name = "_detailPanel";
        _detailPanel.Size = new Size(221, 604);
        _detailPanel.TabIndex = 0;

        _tabDetail.Controls.Add(_tabHeaders);
        _tabDetail.Controls.Add(_tabRequestBody);
        _tabDetail.Controls.Add(_tabResponseBody);
        _tabDetail.Dock = DockStyle.Fill;
        _tabDetail.Location = new Point(0, 28);
        _tabDetail.Name = "_tabDetail";
        _tabDetail.SelectedIndex = 0;
        _tabDetail.Size = new Size(221, 576);
        _tabDetail.TabIndex = 0;

        _tabHeaders.Controls.Add(_rawHeaders);
        _tabHeaders.Controls.Add(_jsonHeaders);
        _tabHeaders.Location = new Point(4, 23);
        _tabHeaders.Name = "_tabHeaders";
        _tabHeaders.Padding = new Padding(3);
        _tabHeaders.Size = new Size(213, 549);
        _tabHeaders.TabIndex = 0;
        _tabHeaders.Text = "Headers";
        _tabHeaders.UseVisualStyleBackColor = true;

        ConfigureRawTextBox(_rawHeaders);
        _rawHeaders.Name = "_rawHeaders";

        _jsonHeaders.Dock = DockStyle.Fill;
        _jsonHeaders.Location = new Point(3, 3);
        _jsonHeaders.Name = "_jsonHeaders";
        _jsonHeaders.Size = new Size(207, 543);
        _jsonHeaders.TabIndex = 1;

        _tabRequestBody.Controls.Add(_rawRequestBody);
        _tabRequestBody.Controls.Add(_jsonRequestBody);
        _tabRequestBody.Location = new Point(4, 23);
        _tabRequestBody.Name = "_tabRequestBody";
        _tabRequestBody.Padding = new Padding(3);
        _tabRequestBody.Size = new Size(213, 549);
        _tabRequestBody.TabIndex = 1;
        _tabRequestBody.Text = "Request Body";
        _tabRequestBody.UseVisualStyleBackColor = true;

        ConfigureRawTextBox(_rawRequestBody);
        _rawRequestBody.Name = "_rawRequestBody";

        _jsonRequestBody.Dock = DockStyle.Fill;
        _jsonRequestBody.Location = new Point(3, 3);
        _jsonRequestBody.Name = "_jsonRequestBody";
        _jsonRequestBody.Size = new Size(207, 543);
        _jsonRequestBody.TabIndex = 1;

        _tabResponseBody.Controls.Add(_rawResponseBody);
        _tabResponseBody.Controls.Add(_jsonResponseBody);
        _tabResponseBody.Location = new Point(4, 23);
        _tabResponseBody.Name = "_tabResponseBody";
        _tabResponseBody.Padding = new Padding(3);
        _tabResponseBody.Size = new Size(213, 549);
        _tabResponseBody.TabIndex = 2;
        _tabResponseBody.Text = "Response Body";
        _tabResponseBody.UseVisualStyleBackColor = true;

        ConfigureRawTextBox(_rawResponseBody);
        _rawResponseBody.Name = "_rawResponseBody";

        _jsonResponseBody.Dock = DockStyle.Fill;
        _jsonResponseBody.Location = new Point(3, 3);
        _jsonResponseBody.Name = "_jsonResponseBody";
        _jsonResponseBody.Size = new Size(207, 543);
        _jsonResponseBody.TabIndex = 1;

        _pnlJsonToolbar.Controls.Add(_txtJsonSearch);
        _pnlJsonToolbar.Controls.Add(_btnJsonSearch);
        _pnlJsonToolbar.Controls.Add(_btnExpandAll);
        _pnlJsonToolbar.Controls.Add(_btnCollapseAll);
        _pnlJsonToolbar.Controls.Add(_btnCollapseTo2);
        _pnlJsonToolbar.Controls.Add(_btnToggleView);
        _pnlJsonToolbar.Dock = DockStyle.Top;
        _pnlJsonToolbar.Location = new Point(0, 0);
        _pnlJsonToolbar.Name = "_pnlJsonToolbar";
        _pnlJsonToolbar.Size = new Size(221, 28);
        _pnlJsonToolbar.TabIndex = 1;

        _txtJsonSearch.Location = new Point(0, 3);
        _txtJsonSearch.Name = "_txtJsonSearch";
        _txtJsonSearch.PlaceholderText = "Search JSON...";
        _txtJsonSearch.Size = new Size(120, 22);
        _txtJsonSearch.TabIndex = 0;

        _btnJsonSearch.FlatStyle = FlatStyle.Flat;
        _btnJsonSearch.Location = new Point(123, 2);
        _btnJsonSearch.Name = "_btnJsonSearch";
        _btnJsonSearch.Size = new Size(24, 23);
        _btnJsonSearch.TabIndex = 1;
        _btnJsonSearch.Text = "▶";

        _btnExpandAll.Location = new Point(155, 2);
        _btnExpandAll.Name = "_btnExpandAll";
        _btnExpandAll.Size = new Size(55, 23);
        _btnExpandAll.TabIndex = 2;
        _btnExpandAll.Text = "Expand";

        _btnCollapseAll.Location = new Point(212, 2);
        _btnCollapseAll.Name = "_btnCollapseAll";
        _btnCollapseAll.Size = new Size(60, 23);
        _btnCollapseAll.TabIndex = 3;
        _btnCollapseAll.Text = "Collapse";

        _btnCollapseTo2.Location = new Point(274, 2);
        _btnCollapseTo2.Name = "_btnCollapseTo2";
        _btnCollapseTo2.Size = new Size(40, 23);
        _btnCollapseTo2.TabIndex = 4;
        _btnCollapseTo2.Text = "Lvl2";

        _btnToggleView.Location = new Point(320, 2);
        _btnToggleView.Name = "_btnToggleView";
        _btnToggleView.Size = new Size(42, 23);
        _btnToggleView.TabIndex = 5;
        _btnToggleView.Text = "Raw";

        _pnlBottomBar.Controls.Add(_btnClear);
        _pnlBottomBar.Controls.Add(_btnExportJson);
        _pnlBottomBar.Controls.Add(_btnExportTxt);
        _pnlBottomBar.Dock = DockStyle.Bottom;
        _pnlBottomBar.FlowDirection = FlowDirection.LeftToRight;
        _pnlBottomBar.Location = new Point(0, 654);
        _pnlBottomBar.Name = "_pnlBottomBar";
        _pnlBottomBar.Padding = new Padding(5, 3, 5, 3);
        _pnlBottomBar.Size = new Size(1264, 35);
        _pnlBottomBar.TabIndex = 3;

        _btnClear.AutoSize = true;
        _btnClear.Name = "_btnClear";
        _btnClear.Size = new Size(75, 27);
        _btnClear.TabIndex = 0;
        _btnClear.Text = "Clear";

        _btnExportJson.AutoSize = true;
        _btnExportJson.Name = "_btnExportJson";
        _btnExportJson.Size = new Size(94, 27);
        _btnExportJson.TabIndex = 1;
        _btnExportJson.Text = "Export JSON";

        _btnExportTxt.AutoSize = true;
        _btnExportTxt.Name = "_btnExportTxt";
        _btnExportTxt.Size = new Size(87, 27);
        _btnExportTxt.TabIndex = 2;
        _btnExportTxt.Text = "Export TXT";

        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblServerStatus, _lblDeviceCountStatus, _lblAdbStatus, _lblLogcatStatus });
        _statusStrip.Location = new Point(0, 689);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1264, 22);
        _statusStrip.TabIndex = 4;

        _lblServerStatus.Name = "_lblServerStatus";
        _lblServerStatus.Size = new Size(102, 17);
        _lblServerStatus.Text = "Server: Stopped";

        _lblDeviceCountStatus.Name = "_lblDeviceCountStatus";
        _lblDeviceCountStatus.Size = new Size(66, 17);
        _lblDeviceCountStatus.Text = "Devices: 0";

        _lblAdbStatus.Name = "_lblAdbStatus";
        _lblAdbStatus.Size = new Size(117, 17);
        _lblAdbStatus.Text = "ADB: Not detected";

        _lblLogcatStatus.Name = "_lblLogcatStatus";
        _lblLogcatStatus.Size = new Size(61, 17);
        _lblLogcatStatus.Text = "Logcat: 0";

        AutoScaleDimensions = new SizeF(7F, 14F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1264, 711);
        Controls.Add(_outerSplit);
        Controls.Add(_toolStrip);
        Controls.Add(_menuStrip);
        Controls.Add(_pnlBottomBar);
        Controls.Add(_statusStrip);
        Font = new Font("Consolas", 9F);
        MainMenuStrip = _menuStrip;
        MinimumSize = new Size(900, 500);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Network Log Viewer";

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
        _tabNetwork.ResumeLayout(false);
        _networkTabContainer.ResumeLayout(false);
        _networkActionBar.ResumeLayout(false);
        _networkActionBar.PerformLayout();
        _pnlNetworkFilter.ResumeLayout(false);
        _pnlNetworkFilter.PerformLayout();
        _tabSystem.ResumeLayout(false);
        _systemTabContainer.ResumeLayout(false);
        _pnlSystemFilter.ResumeLayout(false);
        _pnlSystemFilter.PerformLayout();
        _detailPanel.ResumeLayout(false);
        _tabDetail.ResumeLayout(false);
        _tabHeaders.ResumeLayout(false);
        _tabHeaders.PerformLayout();
        _tabRequestBody.ResumeLayout(false);
        _tabRequestBody.PerformLayout();
        _tabResponseBody.ResumeLayout(false);
        _tabResponseBody.PerformLayout();
        _pnlJsonToolbar.ResumeLayout(false);
        _pnlJsonToolbar.PerformLayout();
        _pnlBottomBar.ResumeLayout(false);
        _pnlBottomBar.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
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
