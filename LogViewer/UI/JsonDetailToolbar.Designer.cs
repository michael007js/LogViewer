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
        _btnToggleView = new System.Windows.Forms.Button();
        _btnCollapseTo2 = new System.Windows.Forms.Button();
        _btnCollapseAll = new System.Windows.Forms.Button();
        _btnExpandAll = new System.Windows.Forms.Button();
        _btnJsonSearch = new System.Windows.Forms.Button();
        SuspendLayout();
        // 
        // _txtJsonSearch
        // 
        _txtJsonSearch.Location = new System.Drawing.Point(0, 2);
        _txtJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _txtJsonSearch.Name = "_txtJsonSearch";
        _txtJsonSearch.PlaceholderText = "Search JSON...";
        _txtJsonSearch.Size = new System.Drawing.Size(102, 23);
        _txtJsonSearch.TabIndex = 0;
        // 
        // _btnToggleView
        // 
        _btnToggleView.Location = new System.Drawing.Point(313, 1);
        _btnToggleView.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnToggleView.Name = "_btnToggleView";
        _btnToggleView.Size = new System.Drawing.Size(59, 24);
        _btnToggleView.TabIndex = 10;
        _btnToggleView.Text = "Raw";
        // 
        // _btnCollapseTo2
        // 
        _btnCollapseTo2.Location = new System.Drawing.Point(265, 0);
        _btnCollapseTo2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseTo2.Name = "_btnCollapseTo2";
        _btnCollapseTo2.Size = new System.Drawing.Size(42, 25);
        _btnCollapseTo2.TabIndex = 9;
        _btnCollapseTo2.Text = "Lvl2";
        // 
        // _btnCollapseAll
        // 
        _btnCollapseAll.Location = new System.Drawing.Point(199, 0);
        _btnCollapseAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnCollapseAll.Name = "_btnCollapseAll";
        _btnCollapseAll.Size = new System.Drawing.Size(60, 25);
        _btnCollapseAll.TabIndex = 8;
        _btnCollapseAll.Text = "Collapse";
        // 
        // _btnExpandAll
        // 
        _btnExpandAll.Location = new System.Drawing.Point(138, 0);
        _btnExpandAll.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnExpandAll.Name = "_btnExpandAll";
        _btnExpandAll.Size = new System.Drawing.Size(55, 25);
        _btnExpandAll.TabIndex = 7;
        _btnExpandAll.Text = "Expand";
        // 
        // _btnJsonSearch
        // 
        _btnJsonSearch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnJsonSearch.Location = new System.Drawing.Point(108, 0);
        _btnJsonSearch.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
        _btnJsonSearch.Name = "_btnJsonSearch";
        _btnJsonSearch.Size = new System.Drawing.Size(24, 25);
        _btnJsonSearch.TabIndex = 6;
        _btnJsonSearch.Text = "▶";
        // 
        // JsonDetailToolbar
        // 
        Controls.Add(_btnToggleView);
        Controls.Add(_btnCollapseTo2);
        Controls.Add(_btnCollapseAll);
        Controls.Add(_btnExpandAll);
        Controls.Add(_btnJsonSearch);
        Controls.Add(_txtJsonSearch);
        Size = new System.Drawing.Size(1061, 26);
        ResumeLayout(false);
        PerformLayout();
    }
}
