namespace LogViewer.UI;

partial class SettingsDialog
{
    private System.ComponentModel.IContainer? components = null;
    private System.Windows.Forms.TableLayoutPanel _layoutRoot = null!;
    private System.Windows.Forms.Label _lblPort = null!;
    private System.Windows.Forms.NumericUpDown _nudPort = null!;
    private System.Windows.Forms.Label _lblMaxPerDevice = null!;
    private System.Windows.Forms.NumericUpDown _nudMaxPerDevice = null!;
    private System.Windows.Forms.Label _lblMaxAll = null!;
    private System.Windows.Forms.NumericUpDown _nudMaxAll = null!;
    private System.Windows.Forms.Label _lblMaxSystemLog = null!;
    private System.Windows.Forms.NumericUpDown _nudMaxSystemLog = null!;
    private System.Windows.Forms.Label _lblAndroidQueue = null!;
    private System.Windows.Forms.NumericUpDown _nudAndroidQueue = null!;
    private System.Windows.Forms.CheckBox _chkAutoAdb = null!;
    private System.Windows.Forms.CheckBox _chkAutoLogcat = null!;
    private System.Windows.Forms.CheckBox _chkAutoStartScrcpy = null!;
    private System.Windows.Forms.CheckBox _chkAutoFormatJson = null!;
    private System.Windows.Forms.Label _lblFontSize = null!;
    private System.Windows.Forms.NumericUpDown _nudFontSize = null!;
    private System.Windows.Forms.Label _lblAdbScanInterval = null!;
    private System.Windows.Forms.NumericUpDown _nudAdbScanInterval = null!;
    private System.Windows.Forms.Label _lblLogcatFilter = null!;
    private System.Windows.Forms.TextBox _txtLogcatFilter = null!;
    private System.Windows.Forms.CheckBox _chkNotifyRegexError = null!;
    private System.Windows.Forms.FlowLayoutPanel _buttonPanel = null!;
    private System.Windows.Forms.Button _btnOk = null!;
    private System.Windows.Forms.Button _btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layoutRoot = new System.Windows.Forms.TableLayoutPanel();
        _lblPort = new System.Windows.Forms.Label();
        _nudPort = new System.Windows.Forms.NumericUpDown();
        _lblMaxPerDevice = new System.Windows.Forms.Label();
        _nudMaxPerDevice = new System.Windows.Forms.NumericUpDown();
        _lblMaxAll = new System.Windows.Forms.Label();
        _nudMaxAll = new System.Windows.Forms.NumericUpDown();
        _lblMaxSystemLog = new System.Windows.Forms.Label();
        _nudMaxSystemLog = new System.Windows.Forms.NumericUpDown();
        _lblAndroidQueue = new System.Windows.Forms.Label();
        _nudAndroidQueue = new System.Windows.Forms.NumericUpDown();
        _chkAutoAdb = new System.Windows.Forms.CheckBox();
        _chkAutoLogcat = new System.Windows.Forms.CheckBox();
        _chkAutoStartScrcpy = new System.Windows.Forms.CheckBox();
        _chkAutoFormatJson = new System.Windows.Forms.CheckBox();
        _lblFontSize = new System.Windows.Forms.Label();
        _nudFontSize = new System.Windows.Forms.NumericUpDown();
        _lblAdbScanInterval = new System.Windows.Forms.Label();
        _nudAdbScanInterval = new System.Windows.Forms.NumericUpDown();
        _lblLogcatFilter = new System.Windows.Forms.Label();
        _txtLogcatFilter = new System.Windows.Forms.TextBox();
        _chkNotifyRegexError = new System.Windows.Forms.CheckBox();
        _buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
        _btnCancel = new System.Windows.Forms.Button();
        _btnOk = new System.Windows.Forms.Button();
        ((System.ComponentModel.ISupportInitialize)_nudPort).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxPerDevice).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxAll).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxSystemLog).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudAndroidQueue).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudFontSize).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudAdbScanInterval).BeginInit();
        _layoutRoot.SuspendLayout();
        _buttonPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _layoutRoot
        // 
        _layoutRoot.ColumnCount = 2;
        _layoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
        _layoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layoutRoot.Controls.Add(_lblPort, 0, 0);
        _layoutRoot.Controls.Add(_nudPort, 1, 0);
        _layoutRoot.Controls.Add(_lblMaxPerDevice, 0, 1);
        _layoutRoot.Controls.Add(_nudMaxPerDevice, 1, 1);
        _layoutRoot.Controls.Add(_lblMaxAll, 0, 2);
        _layoutRoot.Controls.Add(_nudMaxAll, 1, 2);
        _layoutRoot.Controls.Add(_lblMaxSystemLog, 0, 3);
        _layoutRoot.Controls.Add(_nudMaxSystemLog, 1, 3);
        _layoutRoot.Controls.Add(_lblAndroidQueue, 0, 4);
        _layoutRoot.Controls.Add(_nudAndroidQueue, 1, 4);
        _layoutRoot.Controls.Add(_chkAutoAdb, 0, 5);
        _layoutRoot.Controls.Add(_chkAutoLogcat, 0, 6);
        _layoutRoot.Controls.Add(_chkAutoStartScrcpy, 0, 7);
        _layoutRoot.Controls.Add(_chkAutoFormatJson, 0, 8);
        _layoutRoot.Controls.Add(_lblFontSize, 0, 9);
        _layoutRoot.Controls.Add(_nudFontSize, 1, 9);
        _layoutRoot.Controls.Add(_lblAdbScanInterval, 0, 10);
        _layoutRoot.Controls.Add(_nudAdbScanInterval, 1, 10);
        _layoutRoot.Controls.Add(_lblLogcatFilter, 0, 11);
        _layoutRoot.Controls.Add(_txtLogcatFilter, 1, 11);
        _layoutRoot.Controls.Add(_chkNotifyRegexError, 0, 12);
        _layoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
        _layoutRoot.Location = new System.Drawing.Point(12, 12);
        _layoutRoot.Name = "_layoutRoot";
        _layoutRoot.RowCount = 14;
        for (var i = 0; i < 13; i++) _layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
        _layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
        _layoutRoot.Size = new System.Drawing.Size(500, 427);
        _layoutRoot.TabIndex = 0;
        // 
        // labels
        // 
        ConfigureLabel(_lblPort);
        ConfigureLabel(_lblMaxPerDevice);
        ConfigureLabel(_lblMaxAll);
        ConfigureLabel(_lblMaxSystemLog);
        ConfigureLabel(_lblAndroidQueue);
        ConfigureLabel(_lblFontSize);
        ConfigureLabel(_lblAdbScanInterval);
        ConfigureLabel(_lblLogcatFilter);
        // 
        // numeric up-down
        // 
        ConfigureNumeric(_nudPort, 1024, 65535, 9527);
        ConfigureNumeric(_nudMaxPerDevice, 100, 100000, 5000);
        ConfigureNumeric(_nudMaxAll, 100, 100000, 10000);
        ConfigureNumeric(_nudMaxSystemLog, 100, 100000, 10000);
        ConfigureNumeric(_nudAndroidQueue, 100, 10000, 1000);
        ConfigureNumeric(_nudFontSize, 8, 24, 11);
        ConfigureNumeric(_nudAdbScanInterval, 500, 30000, 2000);
        // 
        // checkboxes
        // 
        _chkAutoAdb.AutoSize = true;
        _chkAutoAdb.Dock = System.Windows.Forms.DockStyle.Fill;
        _chkAutoAdb.Margin = new System.Windows.Forms.Padding(3);
        _chkAutoAdb.Checked = true;
        _layoutRoot.SetColumnSpan(_chkAutoAdb, 2);
        _chkAutoLogcat.AutoSize = true;
        _chkAutoLogcat.Dock = System.Windows.Forms.DockStyle.Fill;
        _chkAutoLogcat.Margin = new System.Windows.Forms.Padding(3);
        _chkAutoLogcat.Checked = true;
        _layoutRoot.SetColumnSpan(_chkAutoLogcat, 2);
        _chkAutoStartScrcpy.AutoSize = true;
        _chkAutoStartScrcpy.Dock = System.Windows.Forms.DockStyle.Fill;
        _chkAutoStartScrcpy.Margin = new System.Windows.Forms.Padding(3);
        _layoutRoot.SetColumnSpan(_chkAutoStartScrcpy, 2);
        _chkAutoFormatJson.AutoSize = true;
        _chkAutoFormatJson.Dock = System.Windows.Forms.DockStyle.Fill;
        _chkAutoFormatJson.Margin = new System.Windows.Forms.Padding(3);
        _chkAutoFormatJson.Checked = true;
        _layoutRoot.SetColumnSpan(_chkAutoFormatJson, 2);
        // 
        // _txtLogcatFilter
        // 
        _txtLogcatFilter.Dock = System.Windows.Forms.DockStyle.Fill;
        _txtLogcatFilter.Margin = new System.Windows.Forms.Padding(3);
        _txtLogcatFilter.Name = "_txtLogcatFilter";
        _txtLogcatFilter.Size = new System.Drawing.Size(314, 23);
        _txtLogcatFilter.TabIndex = 12;
        // 
        // _chkNotifyRegexError
        // 
        _chkNotifyRegexError.AutoSize = true;
        _chkNotifyRegexError.Dock = System.Windows.Forms.DockStyle.Fill;
        _chkNotifyRegexError.Margin = new System.Windows.Forms.Padding(3);
        _layoutRoot.SetColumnSpan(_chkNotifyRegexError, 2);
        // 
        // _buttonPanel
        // 
        _buttonPanel.Controls.Add(_btnCancel);
        _buttonPanel.Controls.Add(_btnOk);
        _buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        _buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        _buttonPanel.Location = new System.Drawing.Point(12, 447);
        _buttonPanel.Name = "_buttonPanel";
        _buttonPanel.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
        _buttonPanel.Size = new System.Drawing.Size(500, 42);
        _buttonPanel.TabIndex = 1;
        // 
        // buttons
        // 
        _btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        _btnCancel.Location = new System.Drawing.Point(407, 11);
        _btnCancel.Name = "_btnCancel";
        _btnCancel.Size = new System.Drawing.Size(90, 28);
        _btnCancel.TabIndex = 1;
        _btnOk.Location = new System.Drawing.Point(311, 11);
        _btnOk.Name = "_btnOk";
        _btnOk.Size = new System.Drawing.Size(90, 28);
        _btnOk.TabIndex = 0;
        _btnOk.Click += OnOkClick;
        // 
        // SettingsDialog
        // 
        AcceptButton = _btnOk;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        CancelButton = _btnCancel;
        ClientSize = new System.Drawing.Size(524, 501);
        Controls.Add(_layoutRoot);
        Controls.Add(_buttonPanel);
        Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new System.Drawing.Size(540, 540);
        Name = "SettingsDialog";
        Padding = new System.Windows.Forms.Padding(12);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        ((System.ComponentModel.ISupportInitialize)_nudPort).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxPerDevice).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxAll).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudMaxSystemLog).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudAndroidQueue).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudFontSize).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudAdbScanInterval).EndInit();
        _layoutRoot.ResumeLayout(false);
        _layoutRoot.PerformLayout();
        _buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private static void ConfigureLabel(System.Windows.Forms.Label label)
    {
        label.AutoSize = true;
        label.Dock = System.Windows.Forms.DockStyle.Fill;
        label.Margin = new System.Windows.Forms.Padding(3);
        label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
    }

    private static void ConfigureNumeric(System.Windows.Forms.NumericUpDown control, int min, int max, int value)
    {
        control.Dock = System.Windows.Forms.DockStyle.Left;
        control.Location = new System.Drawing.Point(183, 3);
        control.Margin = new System.Windows.Forms.Padding(3);
        control.Maximum = max;
        control.Minimum = min;
        control.Name = control.Name;
        control.Size = new System.Drawing.Size(120, 23);
        control.Value = value;
    }
}
