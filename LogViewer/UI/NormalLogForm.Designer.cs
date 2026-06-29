namespace LogViewer.UI;

public partial class NormalLogForm
{
    private System.Windows.Forms.Panel _normalTabContainer;
    private System.Windows.Forms.ListView _lstNormalLogs;
    private System.Windows.Forms.Panel _normalActionBar;
    private System.Windows.Forms.Button _btnNormalScrollToTop;
    private System.Windows.Forms.Button _btnNormalScrollToBottom;
    private System.Windows.Forms.Label _lblNormalLogCount;
    private LogViewer.UI.FilterPanel _normalFilterPanel;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _normalTabContainer = new System.Windows.Forms.Panel();
        _lstNormalLogs = new System.Windows.Forms.ListView();
        _normalActionBar = new System.Windows.Forms.Panel();
        _btnNormalScrollToBottom = new System.Windows.Forms.Button();
        _btnNormalScrollToTop = new System.Windows.Forms.Button();
        _lblNormalLogCount = new System.Windows.Forms.Label();
        _normalFilterPanel = new LogViewer.UI.FilterPanel();
        _normalTabContainer.SuspendLayout();
        _normalActionBar.SuspendLayout();
        SuspendLayout();
        // 
        // _normalTabContainer
        // 
        _normalTabContainer.Controls.Add(_lstNormalLogs);
        _normalTabContainer.Controls.Add(_normalActionBar);
        _normalTabContainer.Controls.Add(_normalFilterPanel);
        _normalTabContainer.Dock = System.Windows.Forms.DockStyle.Fill;
        _normalTabContainer.Location = new System.Drawing.Point(0, 0);
        _normalTabContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _normalTabContainer.Name = "_normalTabContainer";
        _normalTabContainer.Size = new System.Drawing.Size(600, 189);
        _normalTabContainer.TabIndex = 0;
        // 
        // _lstNormalLogs
        // 
        _lstNormalLogs.Dock = System.Windows.Forms.DockStyle.Fill;
        _lstNormalLogs.FullRowSelect = true;
        _lstNormalLogs.GridLines = true;
        _lstNormalLogs.Location = new System.Drawing.Point(0, 54);
        _lstNormalLogs.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _lstNormalLogs.MultiSelect = false;
        _lstNormalLogs.Name = "_lstNormalLogs";
        _lstNormalLogs.Size = new System.Drawing.Size(600, 135);
        _lstNormalLogs.TabIndex = 0;
        _lstNormalLogs.UseCompatibleStateImageBehavior = false;
        _lstNormalLogs.View = System.Windows.Forms.View.Details;
        _lstNormalLogs.VirtualMode = true;
        // 
        // _normalActionBar
        // 
        _normalActionBar.Controls.Add(_btnNormalScrollToBottom);
        _normalActionBar.Controls.Add(_btnNormalScrollToTop);
        _normalActionBar.Controls.Add(_lblNormalLogCount);
        _normalActionBar.Dock = System.Windows.Forms.DockStyle.Top;
        _normalActionBar.Location = new System.Drawing.Point(0, 28);
        _normalActionBar.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _normalActionBar.Name = "_normalActionBar";
        _normalActionBar.Size = new System.Drawing.Size(600, 26);
        _normalActionBar.TabIndex = 1;
        // 
        // _btnNormalScrollToBottom
        // 
        _btnNormalScrollToBottom.AutoSize = true;
        _btnNormalScrollToBottom.Dock = System.Windows.Forms.DockStyle.Left;
        _btnNormalScrollToBottom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnNormalScrollToBottom.Font = new System.Drawing.Font("Consolas", 8F);
        _btnNormalScrollToBottom.Location = new System.Drawing.Point(109, 0);
        _btnNormalScrollToBottom.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnNormalScrollToBottom.Name = "_btnNormalScrollToBottom";
        _btnNormalScrollToBottom.Size = new System.Drawing.Size(127, 26);
        _btnNormalScrollToBottom.TabIndex = 1;
        _btnNormalScrollToBottom.Text = "⬇ Scroll to Bottom";
        _btnNormalScrollToBottom.Click += OnNormalScrollToBottomClick;
        // 
        // _btnNormalScrollToTop
        // 
        _btnNormalScrollToTop.AutoSize = true;
        _btnNormalScrollToTop.Dock = System.Windows.Forms.DockStyle.Left;
        _btnNormalScrollToTop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnNormalScrollToTop.Font = new System.Drawing.Font("Consolas", 8F);
        _btnNormalScrollToTop.Location = new System.Drawing.Point(0, 0);
        _btnNormalScrollToTop.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnNormalScrollToTop.Name = "_btnNormalScrollToTop";
        _btnNormalScrollToTop.Size = new System.Drawing.Size(109, 26);
        _btnNormalScrollToTop.TabIndex = 0;
        _btnNormalScrollToTop.Text = "↑ Scroll to Top";
        _btnNormalScrollToTop.Click += OnNormalScrollToTopClick;
        // 
        // _lblNormalLogCount
        // 
        _lblNormalLogCount.Dock = System.Windows.Forms.DockStyle.Right;
        _lblNormalLogCount.Location = new System.Drawing.Point(450, 0);
        _lblNormalLogCount.Name = "_lblNormalLogCount";
        _lblNormalLogCount.Size = new System.Drawing.Size(150, 26);
        _lblNormalLogCount.TabIndex = 1;
        _lblNormalLogCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // _normalFilterPanel
        // 
        _normalFilterPanel.Dock = System.Windows.Forms.DockStyle.Top;
        _normalFilterPanel.Filter1SelectedIndex = -1;
        _normalFilterPanel.Filter2SelectedIndex = -1;
        _normalFilterPanel.Font = new System.Drawing.Font("Consolas", 9F);
        _normalFilterPanel.Location = new System.Drawing.Point(0, 0);
        _normalFilterPanel.Name = "_normalFilterPanel";
        _normalFilterPanel.NotifyRegexError = false;
        _normalFilterPanel.Size = new System.Drawing.Size(600, 28);
        _normalFilterPanel.TabIndex = 2;
        _normalFilterPanel.FilterChanged += OnNormalFilterChanged;
        // 
        // NormalLogForm
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(600, 189);
        Controls.Add(_normalTabContainer);
        Font = new System.Drawing.Font("Consolas", 9F);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        Text = "NormalLogForm";
        _normalTabContainer.ResumeLayout(false);
        _normalActionBar.ResumeLayout(false);
        _normalActionBar.PerformLayout();
        ResumeLayout(false);
    }
}
