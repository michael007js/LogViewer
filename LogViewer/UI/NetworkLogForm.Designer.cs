namespace LogViewer.UI;

public partial class NetworkLogForm
{
    private System.Windows.Forms.Panel _networkTabContainer;
    private System.Windows.Forms.ListView _lstNetworkLogs;
    private System.Windows.Forms.Panel _networkActionBar;
    private System.Windows.Forms.Button _btnScrollToTop;
    private System.Windows.Forms.Button _btnScrollToBottom;
    private System.Windows.Forms.Label _lblLogCount;
    private LogViewer.UI.FilterPanel _networkFilterPanel;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _networkTabContainer = new System.Windows.Forms.Panel();
        _lstNetworkLogs = new System.Windows.Forms.ListView();
        _networkActionBar = new System.Windows.Forms.Panel();
        _btnScrollToBottom = new System.Windows.Forms.Button();
        _btnScrollToTop = new System.Windows.Forms.Button();
        _lblLogCount = new System.Windows.Forms.Label();
        _networkFilterPanel = new LogViewer.UI.FilterPanel();
        _networkTabContainer.SuspendLayout();
        _networkActionBar.SuspendLayout();
        SuspendLayout();
        // 
        // _networkTabContainer
        // 
        _networkTabContainer.Controls.Add(_lstNetworkLogs);
        _networkTabContainer.Controls.Add(_networkActionBar);
        _networkTabContainer.Controls.Add(_networkFilterPanel);
        _networkTabContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _networkTabContainer.Location = new System.Drawing.Point(0, 0);
        _networkTabContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _networkTabContainer.Name = "_networkTabContainer";
        _networkTabContainer.Size = new System.Drawing.Size(600, 189);
        _networkTabContainer.TabIndex = 0;
        // 
        // _lstNetworkLogs
        // 
        _lstNetworkLogs.Dock = System.Windows.Forms.DockStyle.Fill;
        _lstNetworkLogs.FullRowSelect = true;
        _lstNetworkLogs.GridLines = true;
        _lstNetworkLogs.Location = new System.Drawing.Point(0, 54);
        _lstNetworkLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _lstNetworkLogs.MultiSelect = false;
        _lstNetworkLogs.Name = "_lstNetworkLogs";
        _lstNetworkLogs.Size = new System.Drawing.Size(600, 135);
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
        _networkActionBar.Size = new System.Drawing.Size(600, 26);
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
        _btnScrollToBottom.Size = new System.Drawing.Size(127, 26);
        _btnScrollToBottom.TabIndex = 1;
        _btnScrollToBottom.Text = "⬇ Scroll to Bottom";
        _btnScrollToBottom.Click += OnScrollToBottomClick;
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
        _btnScrollToTop.Size = new System.Drawing.Size(109, 26);
        _btnScrollToTop.TabIndex = 0;
        _btnScrollToTop.Text = "↑ Scroll to Top";
        _btnScrollToTop.Click += OnScrollToTopClick;
        // 
        // _lblLogCount
        // 
        _lblLogCount.Dock = System.Windows.Forms.DockStyle.Right;
        _lblLogCount.Location = new System.Drawing.Point(450, 0);
        _lblLogCount.Name = "_lblLogCount";
        _lblLogCount.Size = new System.Drawing.Size(150, 26);
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
        _networkFilterPanel.Name = "_networkFilterPanel";
        _networkFilterPanel.NotifyRegexError = false;
        _networkFilterPanel.Size = new System.Drawing.Size(600, 28);
        _networkFilterPanel.TabIndex = 2;
        _networkFilterPanel.FilterChanged += OnNetworkFilterChanged;
        // 
        // NetworkLogForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(600, 189);
        Controls.Add(_networkTabContainer);
        Font = new System.Drawing.Font("Consolas", 9F);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        Text = "NetworkLogForm";
        _networkTabContainer.ResumeLayout(false);
        _networkActionBar.ResumeLayout(false);
        _networkActionBar.PerformLayout();
        ResumeLayout(false);
    }
}
