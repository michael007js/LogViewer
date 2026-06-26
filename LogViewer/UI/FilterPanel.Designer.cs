namespace LogViewer.UI;

partial class FilterPanel
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TableLayoutPanel tlpMain;
    private System.Windows.Forms.TextBox txtKeyword;
    private System.Windows.Forms.Button btnRegex;
    private System.Windows.Forms.ComboBox cmbFilter1;
    private System.Windows.Forms.ComboBox cmbFilter2;

    private void InitializeComponent()
    {
        tlpMain = new System.Windows.Forms.TableLayoutPanel();
        txtKeyword = new System.Windows.Forms.TextBox();
        btnRegex = new System.Windows.Forms.Button();
        cmbFilter1 = new System.Windows.Forms.ComboBox();
        cmbFilter2 = new System.Windows.Forms.ComboBox();
        tlpMain.SuspendLayout();
        SuspendLayout();
        // 
        // tlpMain
        // 
        tlpMain.ColumnCount = 4;
        tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
        tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
        tlpMain.Controls.Add(txtKeyword, 0, 0);
        tlpMain.Controls.Add(btnRegex, 1, 0);
        tlpMain.Controls.Add(cmbFilter1, 2, 0);
        tlpMain.Controls.Add(cmbFilter2, 3, 0);
        tlpMain.Dock = System.Windows.Forms.DockStyle.Fill;
        tlpMain.Location = new System.Drawing.Point(0, 0);
        tlpMain.Name = "tlpMain";
        tlpMain.RowCount = 1;
        tlpMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        tlpMain.Size = new System.Drawing.Size(600, 28);
        tlpMain.TabIndex = 0;
        // 
        // txtKeyword
        // 
        txtKeyword.Dock = System.Windows.Forms.DockStyle.Fill;
        txtKeyword.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        txtKeyword.Name = "txtKeyword";
        txtKeyword.PlaceholderText = "Keyword...";
        txtKeyword.TabIndex = 0;
        // 
        // btnRegex
        // 
        btnRegex.Dock = System.Windows.Forms.DockStyle.Fill;
        btnRegex.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnRegex.Font = new System.Drawing.Font("Consolas", 8F);
        btnRegex.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
        btnRegex.Name = "btnRegex";
        btnRegex.TabIndex = 1;
        btnRegex.Text = ".*";
        // 
        // cmbFilter1
        // 
        cmbFilter1.Dock = System.Windows.Forms.DockStyle.Fill;
        cmbFilter1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        cmbFilter1.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        cmbFilter1.Name = "cmbFilter1";
        cmbFilter1.TabIndex = 2;
        // 
        // cmbFilter2
        // 
        cmbFilter2.Dock = System.Windows.Forms.DockStyle.Fill;
        cmbFilter2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        cmbFilter2.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        cmbFilter2.Name = "cmbFilter2";
        cmbFilter2.TabIndex = 3;
        // 
        // FilterPanel
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Controls.Add(tlpMain);
        Font = new System.Drawing.Font("Consolas", 9F);
        Name = "FilterPanel";
        Size = new System.Drawing.Size(600, 28);
        tlpMain.ResumeLayout(false);
        tlpMain.PerformLayout();
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }
}
