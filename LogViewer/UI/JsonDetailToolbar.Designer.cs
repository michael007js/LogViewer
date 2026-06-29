namespace LogViewer.UI;

public partial class JsonDetailToolbar
{
    private System.Windows.Forms.TextBox _txtJsonSearch;
    private System.Windows.Forms.Button _btnJsonSearch;
    private System.Windows.Forms.Button _btnExpandAll;
    private System.Windows.Forms.Button _btnCollapseAll;
    private System.Windows.Forms.Button _btnCollapseTo2;
    private System.Windows.Forms.Button _btnToggleView;

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _txtJsonSearch = new System.Windows.Forms.TextBox();
        _btnJsonSearch = new System.Windows.Forms.Button();
        _btnExpandAll = new System.Windows.Forms.Button();
        _btnCollapseAll = new System.Windows.Forms.Button();
        _btnCollapseTo2 = new System.Windows.Forms.Button();
        _btnToggleView = new System.Windows.Forms.Button();
        SuspendLayout();
        // 
        // _txtJsonSearch
        // 
        _txtJsonSearch.Location = new System.Drawing.Point(3, 0);
        _txtJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _txtJsonSearch.Name = "_txtJsonSearch";
        _txtJsonSearch.PlaceholderText = "Search JSON...";
        _txtJsonSearch.Size = new System.Drawing.Size(102, 23);
        _txtJsonSearch.TabIndex = 0;
        // 
        // _btnJsonSearch
        // 
        _btnJsonSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnJsonSearch.Location = new System.Drawing.Point(111, -3);
        _btnJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnJsonSearch.Name = "_btnJsonSearch";
        _btnJsonSearch.Size = new System.Drawing.Size(24, 25);
        _btnJsonSearch.TabIndex = 1;
        _btnJsonSearch.Text = "▶";
        _btnJsonSearch.Click += OnBtnJsonSearchClick;
        // 
        // _btnExpandAll
        // 
        _btnExpandAll.Location = new System.Drawing.Point(141, -3);
        _btnExpandAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnExpandAll.Name = "_btnExpandAll";
        _btnExpandAll.Size = new System.Drawing.Size(55, 25);
        _btnExpandAll.TabIndex = 2;
        _btnExpandAll.Text = "Expand";
        _btnExpandAll.Click += OnBtnExpandAllClick;
        // 
        // _btnCollapseAll
        // 
        _btnCollapseAll.Location = new System.Drawing.Point(202, -3);
        _btnCollapseAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseAll.Name = "_btnCollapseAll";
        _btnCollapseAll.Size = new System.Drawing.Size(60, 25);
        _btnCollapseAll.TabIndex = 3;
        _btnCollapseAll.Text = "Collapse";
        _btnCollapseAll.Click += OnBtnCollapseAllClick;
        // 
        // _btnCollapseTo2
        // 
        _btnCollapseTo2.Location = new System.Drawing.Point(268, -3);
        _btnCollapseTo2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseTo2.Name = "_btnCollapseTo2";
        _btnCollapseTo2.Size = new System.Drawing.Size(42, 25);
        _btnCollapseTo2.TabIndex = 4;
        _btnCollapseTo2.Text = "Lvl2";
        _btnCollapseTo2.Click += OnBtnCollapseTo2Click;
        // 
        // _btnToggleView
        // 
        _btnToggleView.Location = new System.Drawing.Point(316, -2);
        _btnToggleView.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnToggleView.Name = "_btnToggleView";
        _btnToggleView.Size = new System.Drawing.Size(42, 24);
        _btnToggleView.TabIndex = 5;
        _btnToggleView.Text = "Raw";
        _btnToggleView.Click += OnBtnToggleViewClick;
        // 
        // JsonDetailToolbar
        // 
        Controls.Add(_btnToggleView);
        Controls.Add(_btnCollapseTo2);
        Controls.Add(_btnCollapseAll);
        Controls.Add(_btnExpandAll);
        Controls.Add(_btnJsonSearch);
        Controls.Add(_txtJsonSearch);
        Size = new System.Drawing.Size(1061, 37);
        ResumeLayout(false);
        PerformLayout();
    }
}
