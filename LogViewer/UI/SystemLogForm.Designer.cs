namespace LogViewer.UI;

public partial class SystemLogForm
{
    private System.Windows.Forms.Panel _systemTabContainer;
    private System.Windows.Forms.ListView _lstSystemLogs;
    private System.Windows.Forms.Panel _systemActionBar;
    private System.Windows.Forms.Button _btnSystemScrollToTop;
    private System.Windows.Forms.Button _btnSystemScrollToBottom;
    private System.Windows.Forms.Button _btnSystemPauseResume;
    private System.Windows.Forms.Label _lblSystemBacklog;
    private LogViewer.UI.FilterPanel _systemFilterPanel;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _systemTabContainer = new System.Windows.Forms.Panel();
        _lstSystemLogs = new System.Windows.Forms.ListView();
        _systemActionBar = new System.Windows.Forms.Panel();
        _lblSystemBacklog = new System.Windows.Forms.Label();
        _btnSystemPauseResume = new System.Windows.Forms.Button();
        _btnSystemScrollToBottom = new System.Windows.Forms.Button();
        _btnSystemScrollToTop = new System.Windows.Forms.Button();
        _systemFilterPanel = new LogViewer.UI.FilterPanel();
        _systemTabContainer.SuspendLayout();
        _systemActionBar.SuspendLayout();
        SuspendLayout();
        // 
        // _systemTabContainer
        // 
        _systemTabContainer.Controls.Add(_lstSystemLogs);
        _systemTabContainer.Controls.Add(_systemActionBar);
        _systemTabContainer.Controls.Add(_systemFilterPanel);
        _systemTabContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _systemTabContainer.Location = new System.Drawing.Point(0, 0);
        _systemTabContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _systemTabContainer.Name = "_systemTabContainer";
        _systemTabContainer.Size = new System.Drawing.Size(600, 110);
        _systemTabContainer.TabIndex = 0;
        // 
        // _lstSystemLogs
        // 
        _lstSystemLogs.Dock = System.Windows.Forms.DockStyle.Fill;
        _lstSystemLogs.FullRowSelect = true;
        _lstSystemLogs.GridLines = true;
        _lstSystemLogs.Location = new System.Drawing.Point(0, 49);
        _lstSystemLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _lstSystemLogs.MultiSelect = false;
        _lstSystemLogs.Name = "_lstSystemLogs";
        _lstSystemLogs.Size = new System.Drawing.Size(600, 61);
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
        _systemActionBar.Size = new System.Drawing.Size(600, 26);
        _systemActionBar.TabIndex = 2;
        // 
        // _lblSystemBacklog
        // 
        _lblSystemBacklog.Dock = System.Windows.Forms.DockStyle.Right;
        _lblSystemBacklog.Location = new System.Drawing.Point(430, 0);
        _lblSystemBacklog.Name = "_lblSystemBacklog";
        _lblSystemBacklog.Size = new System.Drawing.Size(170, 26);
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
        _btnSystemPauseResume.Size = new System.Drawing.Size(61, 26);
        _btnSystemPauseResume.TabIndex = 2;
        _btnSystemPauseResume.Text = "Pause";
        _btnSystemPauseResume.Click += OnSystemPauseResumeClick;
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
        _btnSystemScrollToBottom.Size = new System.Drawing.Size(127, 26);
        _btnSystemScrollToBottom.TabIndex = 1;
        _btnSystemScrollToBottom.Text = "⬇ Scroll to Bottom";
        _btnSystemScrollToBottom.Click += OnSystemScrollToBottomClick;
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
        _btnSystemScrollToTop.Size = new System.Drawing.Size(109, 26);
        _btnSystemScrollToTop.TabIndex = 0;
        _btnSystemScrollToTop.Text = "↑ Scroll to Top";
        _btnSystemScrollToTop.Click += OnSystemScrollToTopClick;
        // 
        // _systemFilterPanel
        // 
        _systemFilterPanel.Dock = System.Windows.Forms.DockStyle.Top;
        _systemFilterPanel.Filter1SelectedIndex = -1;
        _systemFilterPanel.Filter2SelectedIndex = -1;
        _systemFilterPanel.Font = new System.Drawing.Font("Consolas", 9F);
        _systemFilterPanel.Location = new System.Drawing.Point(0, 0);
        _systemFilterPanel.Name = "_systemFilterPanel";
        _systemFilterPanel.NotifyRegexError = false;
        _systemFilterPanel.Size = new System.Drawing.Size(600, 28);
        _systemFilterPanel.TabIndex = 1;
        _systemFilterPanel.FilterChanged += OnSystemFilterChanged;
        _systemFilterPanel.Filter2DropDown += OnSystemFilter2DropDown;
        // 
        // SystemLogForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(600, 110);
        Controls.Add(_systemTabContainer);
        Font = new System.Drawing.Font("Consolas", 9F);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        Text = "SystemLogForm";
        _systemTabContainer.ResumeLayout(false);
        _systemActionBar.ResumeLayout(false);
        _systemActionBar.PerformLayout();
        ResumeLayout(false);
    }
}
