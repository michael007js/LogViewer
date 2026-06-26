namespace LogViewer.UI;

public partial class MainForm
{
    private System.Windows.Forms.ToolStripMenuItem _toolsMenuItem;
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.ToolStripMenuItem _settingsMenuItem;
    private System.Windows.Forms.Panel _logPanel;
    private System.Windows.Forms.Panel _networkTabContainer;
    private System.Windows.Forms.Panel _networkActionBar;
    private System.Windows.Forms.Panel _systemTabContainer;
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
        _networkTabContainer = new System.Windows.Forms.Panel();
        _lstNetworkLogs = new System.Windows.Forms.ListView();
        _networkActionBar = new System.Windows.Forms.Panel();
        _btnScrollToBottom = new System.Windows.Forms.Button();
        _btnScrollToTop = new System.Windows.Forms.Button();
        _lblLogCount = new System.Windows.Forms.Label();
        _networkFilterPanel = new LogViewer.UI.FilterPanel();
        _tabSystem = new System.Windows.Forms.TabPage();
        _systemTabContainer = new System.Windows.Forms.Panel();
        _lstSystemLogs = new System.Windows.Forms.ListView();
        _systemActionBar = new System.Windows.Forms.Panel();
        _lblSystemBacklog = new System.Windows.Forms.Label();
        _btnSystemPauseResume = new System.Windows.Forms.Button();
        _btnSystemScrollToBottom = new System.Windows.Forms.Button();
        _btnSystemScrollToTop = new System.Windows.Forms.Button();
        _systemFilterPanel = new LogViewer.UI.FilterPanel();
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
        _pnlJsonToolbar = new System.Windows.Forms.Panel();
        _txtJsonSearch = new System.Windows.Forms.TextBox();
        _btnJsonSearch = new System.Windows.Forms.Button();
        _btnExpandAll = new System.Windows.Forms.Button();
        _btnCollapseAll = new System.Windows.Forms.Button();
        _btnCollapseTo2 = new System.Windows.Forms.Button();
        _btnToggleView = new System.Windows.Forms.Button();
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
        _tabNetwork.SuspendLayout();
        _networkTabContainer.SuspendLayout();
        _networkActionBar.SuspendLayout();
        _tabSystem.SuspendLayout();
        _systemTabContainer.SuspendLayout();
        _systemActionBar.SuspendLayout();
        _detailPanel.SuspendLayout();
        _tabDetail.SuspendLayout();
        _tabHeaders.SuspendLayout();
        _tabRequestBody.SuspendLayout();
        _tabResponseBody.SuspendLayout();
        _pnlJsonToolbar.SuspendLayout();
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
        _outerSplit.Size = new System.Drawing.Size(1264, 490);
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
        _devicePanel.Size = new System.Drawing.Size(273, 490);
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
        _innerSplit.Size = new System.Drawing.Size(987, 490);
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
        _logPanel.Size = new System.Drawing.Size(614, 490);
        _logPanel.TabIndex = 0;
        // 
        // _tabLogType
        // 
        _tabLogType.Controls.Add(_tabNetwork);
        _tabLogType.Controls.Add(_tabSystem);
        _tabLogType.Dock = System.Windows.Forms.DockStyle.Fill;
        _tabLogType.Location = new System.Drawing.Point(0, 0);
        _tabLogType.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabLogType.Name = "_tabLogType";
        _tabLogType.SelectedIndex = 0;
        _tabLogType.Size = new System.Drawing.Size(614, 490);
        _tabLogType.TabIndex = 0;
        // 
        // _tabNetwork
        // 
        _tabNetwork.Controls.Add(_networkTabContainer);
        _tabNetwork.Location = new System.Drawing.Point(4, 23);
        _tabNetwork.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNetwork.Name = "_tabNetwork";
        _tabNetwork.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabNetwork.Size = new System.Drawing.Size(606, 463);
        _tabNetwork.TabIndex = 0;
        _tabNetwork.Text = "Network Logs";
        _tabNetwork.UseVisualStyleBackColor = true;
        // 
        // _networkTabContainer
        // 
        _networkTabContainer.Controls.Add(_lstNetworkLogs);
        _networkTabContainer.Controls.Add(_networkActionBar);
        _networkTabContainer.Controls.Add(_networkFilterPanel);
        _networkTabContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _networkTabContainer.Location = new System.Drawing.Point(3, 2);
        _networkTabContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _networkTabContainer.Name = "_networkTabContainer";
        _networkTabContainer.Size = new System.Drawing.Size(600, 459);
        _networkTabContainer.TabIndex = 0;
        // 
        // _lstNetworkLogs
        // 
        _lstNetworkLogs.Dock = System.Windows.Forms.DockStyle.Fill;
        _lstNetworkLogs.FullRowSelect = true;
        _lstNetworkLogs.GridLines = true;
        _lstNetworkLogs.Location = new System.Drawing.Point(0, 58);
        _lstNetworkLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _lstNetworkLogs.MultiSelect = false;
        _lstNetworkLogs.Name = "_lstNetworkLogs";
        _lstNetworkLogs.Size = new System.Drawing.Size(600, 401);
        _lstNetworkLogs.TabIndex = 0;
        _lstNetworkLogs.UseCompatibleStateImageBehavior = false;
        _lstNetworkLogs.View = System.Windows.Forms.View.Details;
        _lstNetworkLogs.VirtualMode = true;
        // 
        // _networkActionBar
        // 
        _networkActionBar.Controls.Add(_btnScrollToBottom);
        _networkActionBar.Controls.Add(_btnScrollToTop);
        _networkActionBar.Controls.Add(_lblLogCount);
        _networkActionBar.Dock = System.Windows.Forms.DockStyle.Top;
        _networkActionBar.Location = new System.Drawing.Point(0, 28);
        _networkActionBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _networkActionBar.Name = "_networkActionBar";
        _networkActionBar.Size = new System.Drawing.Size(600, 30);
        _networkActionBar.TabIndex = 1;
        // 
        // _btnScrollToBottom
        // 
        _btnScrollToBottom.AutoSize = true;
        _btnScrollToBottom.Dock = System.Windows.Forms.DockStyle.Left;
        _btnScrollToBottom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnScrollToBottom.Font = new System.Drawing.Font("Consolas", 8F);
        _btnScrollToBottom.Location = new System.Drawing.Point(109, 0);
        _btnScrollToBottom.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnScrollToBottom.Name = "_btnScrollToBottom";
        _btnScrollToBottom.Size = new System.Drawing.Size(127, 30);
        _btnScrollToBottom.TabIndex = 1;
        _btnScrollToBottom.Text = "⬇ Scroll to Bottom";
        // 
        // _btnScrollToTop
        // 
        _btnScrollToTop.AutoSize = true;
        _btnScrollToTop.Dock = System.Windows.Forms.DockStyle.Left;
        _btnScrollToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnScrollToTop.Font = new System.Drawing.Font("Consolas", 8F);
        _btnScrollToTop.Location = new System.Drawing.Point(0, 0);
        _btnScrollToTop.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnScrollToTop.Name = "_btnScrollToTop";
        _btnScrollToTop.Size = new System.Drawing.Size(109, 30);
        _btnScrollToTop.TabIndex = 0;
        _btnScrollToTop.Text = "↑ Scroll to Top";
        // 
        // _lblLogCount
        // 
        _lblLogCount.Dock = System.Windows.Forms.DockStyle.Right;
        _lblLogCount.Location = new System.Drawing.Point(450, 0);
        _lblLogCount.Name = "_lblLogCount";
        _lblLogCount.Size = new System.Drawing.Size(150, 30);
        _lblLogCount.TabIndex = 1;
        _lblLogCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // _networkFilterPanel
        // 
        _networkFilterPanel.Dock = System.Windows.Forms.DockStyle.Top;
        _networkFilterPanel.Filter1SelectedIndex = -1;
        _networkFilterPanel.Filter2SelectedIndex = -1;
        _networkFilterPanel.Font = new System.Drawing.Font("Consolas", 9F);
        _networkFilterPanel.Location = new System.Drawing.Point(0, 0);
        _networkFilterPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _networkFilterPanel.Name = "_networkFilterPanel";
        _networkFilterPanel.NotifyRegexError = false;
        _networkFilterPanel.Size = new System.Drawing.Size(600, 28);
        _networkFilterPanel.TabIndex = 2;
        // 
        // _tabSystem
        // 
        _tabSystem.Controls.Add(_systemTabContainer);
        _tabSystem.Location = new System.Drawing.Point(4, 23);
        _tabSystem.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabSystem.Name = "_tabSystem";
        _tabSystem.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabSystem.Size = new System.Drawing.Size(606, 463);
        _tabSystem.TabIndex = 1;
        _tabSystem.Text = "System Logs";
        _tabSystem.UseVisualStyleBackColor = true;
        // 
        // _systemTabContainer
        // 
        _systemTabContainer.Controls.Add(_lstSystemLogs);
        _systemTabContainer.Controls.Add(_systemActionBar);
        _systemTabContainer.Controls.Add(_systemFilterPanel);
        _systemTabContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _systemTabContainer.Location = new System.Drawing.Point(3, 2);
        _systemTabContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _systemTabContainer.Name = "_systemTabContainer";
        _systemTabContainer.Size = new System.Drawing.Size(600, 459);
        _systemTabContainer.TabIndex = 0;
        // 
        // _lstSystemLogs
        // 
        _lstSystemLogs.Dock = System.Windows.Forms.DockStyle.Fill;
        _lstSystemLogs.FullRowSelect = true;
        _lstSystemLogs.GridLines = true;
        _lstSystemLogs.Location = new System.Drawing.Point(0, 58);
        _lstSystemLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _lstSystemLogs.MultiSelect = false;
        _lstSystemLogs.Name = "_lstSystemLogs";
        _lstSystemLogs.Size = new System.Drawing.Size(600, 401);
        _lstSystemLogs.TabIndex = 0;
        _lstSystemLogs.UseCompatibleStateImageBehavior = false;
        _lstSystemLogs.View = System.Windows.Forms.View.Details;
        _lstSystemLogs.VirtualMode = true;
        // 
        // _systemActionBar
        // 
        _systemActionBar.Controls.Add(_lblSystemBacklog);
        _systemActionBar.Controls.Add(_btnSystemPauseResume);
        _systemActionBar.Controls.Add(_btnSystemScrollToBottom);
        _systemActionBar.Controls.Add(_btnSystemScrollToTop);
        _systemActionBar.Dock = System.Windows.Forms.DockStyle.Top;
        _systemActionBar.Location = new System.Drawing.Point(0, 28);
        _systemActionBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _systemActionBar.Name = "_systemActionBar";
        _systemActionBar.Size = new System.Drawing.Size(600, 30);
        _systemActionBar.TabIndex = 2;
        // 
        // _lblSystemBacklog
        // 
        _lblSystemBacklog.Dock = System.Windows.Forms.DockStyle.Right;
        _lblSystemBacklog.Location = new System.Drawing.Point(430, 0);
        _lblSystemBacklog.Name = "_lblSystemBacklog";
        _lblSystemBacklog.Size = new System.Drawing.Size(170, 30);
        _lblSystemBacklog.TabIndex = 3;
        _lblSystemBacklog.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        _lblSystemBacklog.Visible = false;
        // 
        // _btnSystemPauseResume
        // 
        _btnSystemPauseResume.AutoSize = true;
        _btnSystemPauseResume.Dock = System.Windows.Forms.DockStyle.Left;
        _btnSystemPauseResume.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnSystemPauseResume.Font = new System.Drawing.Font("Consolas", 8F);
        _btnSystemPauseResume.Location = new System.Drawing.Point(236, 0);
        _btnSystemPauseResume.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnSystemPauseResume.Name = "_btnSystemPauseResume";
        _btnSystemPauseResume.Size = new System.Drawing.Size(61, 30);
        _btnSystemPauseResume.TabIndex = 2;
        _btnSystemPauseResume.Text = "Pause";
        // 
        // _btnSystemScrollToBottom
        // 
        _btnSystemScrollToBottom.AutoSize = true;
        _btnSystemScrollToBottom.Dock = System.Windows.Forms.DockStyle.Left;
        _btnSystemScrollToBottom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnSystemScrollToBottom.Font = new System.Drawing.Font("Consolas", 8F);
        _btnSystemScrollToBottom.Location = new System.Drawing.Point(109, 0);
        _btnSystemScrollToBottom.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnSystemScrollToBottom.Name = "_btnSystemScrollToBottom";
        _btnSystemScrollToBottom.Size = new System.Drawing.Size(127, 30);
        _btnSystemScrollToBottom.TabIndex = 1;
        _btnSystemScrollToBottom.Text = "⬇ Scroll to Bottom";
        // 
        // _btnSystemScrollToTop
        // 
        _btnSystemScrollToTop.AutoSize = true;
        _btnSystemScrollToTop.Dock = System.Windows.Forms.DockStyle.Left;
        _btnSystemScrollToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnSystemScrollToTop.Font = new System.Drawing.Font("Consolas", 8F);
        _btnSystemScrollToTop.Location = new System.Drawing.Point(0, 0);
        _btnSystemScrollToTop.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnSystemScrollToTop.Name = "_btnSystemScrollToTop";
        _btnSystemScrollToTop.Size = new System.Drawing.Size(109, 30);
        _btnSystemScrollToTop.TabIndex = 0;
        _btnSystemScrollToTop.Text = "↑ Scroll to Top";
        // 
        // _systemFilterPanel
        // 
        _systemFilterPanel.Dock = System.Windows.Forms.DockStyle.Top;
        _systemFilterPanel.Filter1SelectedIndex = -1;
        _systemFilterPanel.Filter2SelectedIndex = -1;
        _systemFilterPanel.Font = new System.Drawing.Font("Consolas", 9F);
        _systemFilterPanel.Location = new System.Drawing.Point(0, 0);
        _systemFilterPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _systemFilterPanel.Name = "_systemFilterPanel";
        _systemFilterPanel.NotifyRegexError = false;
        _systemFilterPanel.Size = new System.Drawing.Size(600, 28);
        _systemFilterPanel.TabIndex = 1;
        // 
        // _detailPanel
        // 
        _detailPanel.Controls.Add(_tabDetail);
        _detailPanel.Controls.Add(_pnlJsonToolbar);
        _detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _detailPanel.Location = new System.Drawing.Point(0, 0);
        _detailPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _detailPanel.Name = "_detailPanel";
        _detailPanel.Size = new System.Drawing.Size(369, 490);
        _detailPanel.TabIndex = 0;
        // 
        // _tabDetail
        // 
        _tabDetail.Controls.Add(_tabHeaders);
        _tabDetail.Controls.Add(_tabRequestBody);
        _tabDetail.Controls.Add(_tabResponseBody);
        _tabDetail.Dock = System.Windows.Forms.DockStyle.Fill;
        _tabDetail.Location = new System.Drawing.Point(0, 28);
        _tabDetail.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _tabDetail.Name = "_tabDetail";
        _tabDetail.SelectedIndex = 0;
        _tabDetail.Size = new System.Drawing.Size(369, 462);
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
        _tabHeaders.Size = new System.Drawing.Size(361, 435);
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
        _rawHeaders.Size = new System.Drawing.Size(355, 431);
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
        _jsonHeaders.Size = new System.Drawing.Size(355, 431);
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
        _tabRequestBody.Size = new System.Drawing.Size(361, 270);
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
        _rawRequestBody.Size = new System.Drawing.Size(355, 266);
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
        _jsonRequestBody.Size = new System.Drawing.Size(355, 266);
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
        _tabResponseBody.Size = new System.Drawing.Size(361, 270);
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
        _rawResponseBody.Size = new System.Drawing.Size(355, 266);
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
        _jsonResponseBody.Size = new System.Drawing.Size(355, 266);
        _jsonResponseBody.TabIndex = 1;
        // 
        // _pnlJsonToolbar
        // 
        _pnlJsonToolbar.Controls.Add(_txtJsonSearch);
        _pnlJsonToolbar.Controls.Add(_btnJsonSearch);
        _pnlJsonToolbar.Controls.Add(_btnExpandAll);
        _pnlJsonToolbar.Controls.Add(_btnCollapseAll);
        _pnlJsonToolbar.Controls.Add(_btnCollapseTo2);
        _pnlJsonToolbar.Controls.Add(_btnToggleView);
        _pnlJsonToolbar.Dock = System.Windows.Forms.DockStyle.Top;
        _pnlJsonToolbar.Location = new System.Drawing.Point(0, 0);
        _pnlJsonToolbar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _pnlJsonToolbar.Name = "_pnlJsonToolbar";
        _pnlJsonToolbar.Size = new System.Drawing.Size(369, 28);
        _pnlJsonToolbar.TabIndex = 1;
        // 
        // _txtJsonSearch
        // 
        _txtJsonSearch.Location = new System.Drawing.Point(0, 2);
        _txtJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _txtJsonSearch.Name = "_txtJsonSearch";
        _txtJsonSearch.PlaceholderText = "Search JSON...";
        _txtJsonSearch.Size = new System.Drawing.Size(120, 22);
        _txtJsonSearch.TabIndex = 0;
        // 
        // _btnJsonSearch
        // 
        _btnJsonSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnJsonSearch.Location = new System.Drawing.Point(123, 2);
        _btnJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnJsonSearch.Name = "_btnJsonSearch";
        _btnJsonSearch.Size = new System.Drawing.Size(24, 20);
        _btnJsonSearch.TabIndex = 1;
        _btnJsonSearch.Text = "▶";
        // 
        // _btnExpandAll
        // 
        _btnExpandAll.Location = new System.Drawing.Point(155, 2);
        _btnExpandAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnExpandAll.Name = "_btnExpandAll";
        _btnExpandAll.Size = new System.Drawing.Size(55, 22);
        _btnExpandAll.TabIndex = 2;
        _btnExpandAll.Text = "Expand";
        // 
        // _btnCollapseAll
        // 
        _btnCollapseAll.Location = new System.Drawing.Point(212, 2);
        _btnCollapseAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseAll.Name = "_btnCollapseAll";
        _btnCollapseAll.Size = new System.Drawing.Size(60, 22);
        _btnCollapseAll.TabIndex = 3;
        _btnCollapseAll.Text = "Collapse";
        // 
        // _btnCollapseTo2
        // 
        _btnCollapseTo2.Location = new System.Drawing.Point(274, 2);
        _btnCollapseTo2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseTo2.Name = "_btnCollapseTo2";
        _btnCollapseTo2.Size = new System.Drawing.Size(42, 20);
        _btnCollapseTo2.TabIndex = 4;
        _btnCollapseTo2.Text = "Lvl2";
        // 
        // _btnToggleView
        // 
        _btnToggleView.Location = new System.Drawing.Point(320, 2);
        _btnToggleView.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnToggleView.Name = "_btnToggleView";
        _btnToggleView.Size = new System.Drawing.Size(42, 20);
        _btnToggleView.TabIndex = 5;
        _btnToggleView.Text = "Raw";
        // 
        // _pnlBottomBar
        // 
        _pnlBottomBar.Controls.Add(_btnClear);
        _pnlBottomBar.Controls.Add(_btnExportJson);
        _pnlBottomBar.Controls.Add(_btnExportTxt);
        _pnlBottomBar.Dock = System.Windows.Forms.DockStyle.Bottom;
        _pnlBottomBar.Location = new System.Drawing.Point(0, 540);
        _pnlBottomBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _pnlBottomBar.Name = "_pnlBottomBar";
        _pnlBottomBar.Padding = new System.Windows.Forms.Padding(5, 2, 5, 2);
        _pnlBottomBar.Size = new System.Drawing.Size(1264, 32);
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
        _statusStrip.Location = new System.Drawing.Point(0, 572);
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
        ClientSize = new System.Drawing.Size(1264, 594);
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
        _tabNetwork.ResumeLayout(false);
        _networkTabContainer.ResumeLayout(false);
        _networkActionBar.ResumeLayout(false);
        _networkActionBar.PerformLayout();
        _tabSystem.ResumeLayout(false);
        _systemTabContainer.ResumeLayout(false);
        _systemActionBar.ResumeLayout(false);
        _systemActionBar.PerformLayout();
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
