using LogViewer.Static;

namespace LogViewer.UI;

partial class DevicePanel
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _buttonBar = new System.Windows.Forms.TableLayoutPanel();
        _btnMirrorToggle = new System.Windows.Forms.Button();
        _btnMirrorReconnect = new System.Windows.Forms.Button();
        tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        _lblMirrorStatus = new System.Windows.Forms.Label();
        _mirrorHostPanel = new LogViewer.UI.MirrorHostPanel();
        tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
        button1 = new System.Windows.Forms.Button();
        button2 = new System.Windows.Forms.Button();
        _btnMirrorRotate = new System.Windows.Forms.Button();
        _btnMirrorScreenshot = new System.Windows.Forms.Button();
        _btnMirrorPopout = new System.Windows.Forms.Button();
        _cmbDevices = new System.Windows.Forms.ComboBox();
        _btnRefreshAdb = new System.Windows.Forms.Button();
        _buttonBar.SuspendLayout();
        tableLayoutPanel1.SuspendLayout();
        tableLayoutPanel2.SuspendLayout();
        SuspendLayout();
        // 
        // _buttonBar
        // 
        _buttonBar.ColumnCount = 2;
        _buttonBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _buttonBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _buttonBar.Controls.Add(_btnMirrorToggle, 0, 0);
        _buttonBar.Location = new System.Drawing.Point(0, 0);
        _buttonBar.Name = "_buttonBar";
        _buttonBar.RowCount = 1;
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        _buttonBar.Size = new System.Drawing.Size(200, 100);
        _buttonBar.TabIndex = 0;
        // 
        // _btnMirrorToggle
        // 
        _btnMirrorToggle.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorToggle.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorToggle.Location = new System.Drawing.Point(0, 0);
        _btnMirrorToggle.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorToggle.Name = "_btnMirrorToggle";
        _btnMirrorToggle.Size = new System.Drawing.Size(94, 94);
        _btnMirrorToggle.TabIndex = 0;
        _btnMirrorToggle.Text = "启动";
        // 
        // _btnMirrorReconnect
        // 
        _btnMirrorReconnect.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorReconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorReconnect.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorReconnect.Location = new System.Drawing.Point(100, 0);
        _btnMirrorReconnect.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorReconnect.Name = "_btnMirrorReconnect";
        _btnMirrorReconnect.Size = new System.Drawing.Size(94, 94);
        _btnMirrorReconnect.TabIndex = 1;
        _btnMirrorReconnect.Text = "重连";
        // 
        // tableLayoutPanel1
        // 
        tableLayoutPanel1.ColumnCount = 1;
        tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        tableLayoutPanel1.Controls.Add(_lblMirrorStatus, 0, 4);
        tableLayoutPanel1.Controls.Add(_mirrorHostPanel, 0, 2);
        tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 3);
        tableLayoutPanel1.Controls.Add(_cmbDevices, 0, 1);
        tableLayoutPanel1.Controls.Add(_btnRefreshAdb, 0, 0);
        tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
        tableLayoutPanel1.Location = new System.Drawing.Point(8, 8);
        tableLayoutPanel1.Name = "tableLayoutPanel1";
        tableLayoutPanel1.RowCount = 5;
        tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 70F));
        tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel1.Size = new System.Drawing.Size(156, 351);
        tableLayoutPanel1.TabIndex = 28;
        // 
        // _lblMirrorStatus
        // 
        _lblMirrorStatus.Dock = System.Windows.Forms.DockStyle.Fill;
        _lblMirrorStatus.ForeColor = System.Drawing.Color.DimGray;
        _lblMirrorStatus.Location = new System.Drawing.Point(3, 331);
        _lblMirrorStatus.Name = "_lblMirrorStatus";
        _lblMirrorStatus.Padding = new System.Windows.Forms.Padding(2, 0, 2, 0);
        _lblMirrorStatus.Size = new System.Drawing.Size(150, 20);
        _lblMirrorStatus.TabIndex = 31;
        _lblMirrorStatus.Text = "请选择具体设备以操控手机";
        _lblMirrorStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _mirrorHostPanel
        // 
        _mirrorHostPanel.BackColor = System.Drawing.Color.Black;
        _mirrorHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _mirrorHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _mirrorHostPanel.Location = new System.Drawing.Point(3, 61);
        _mirrorHostPanel.MirrorActive = false;
        _mirrorHostPanel.Name = "_mirrorHostPanel";
        _mirrorHostPanel.Size = new System.Drawing.Size(150, 183);
        _mirrorHostPanel.TabIndex = 30;
        // 
        // tableLayoutPanel2
        // 
        tableLayoutPanel2.ColumnCount = 2;
        tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        tableLayoutPanel2.Controls.Add(button1, 0, 0);
        tableLayoutPanel2.Controls.Add(button2, 1, 0);
        tableLayoutPanel2.Controls.Add(_btnMirrorRotate, 0, 1);
        tableLayoutPanel2.Controls.Add(_btnMirrorScreenshot, 1, 1);
        tableLayoutPanel2.Controls.Add(_btnMirrorPopout, 0, 2);
        tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
        tableLayoutPanel2.Location = new System.Drawing.Point(0, 247);
        tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(0);
        tableLayoutPanel2.Name = "tableLayoutPanel2";
        tableLayoutPanel2.RowCount = 3;
        tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        tableLayoutPanel2.Size = new System.Drawing.Size(156, 84);
        tableLayoutPanel2.TabIndex = 27;
        // 
        // button1
        // 
        button1.Dock = System.Windows.Forms.DockStyle.Fill;
        button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        button1.Font = new System.Drawing.Font("Consolas", 8F);
        button1.Location = new System.Drawing.Point(0, 0);
        button1.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        button1.Name = "button1";
        button1.Size = new System.Drawing.Size(72, 24);
        button1.TabIndex = 0;
        button1.Text = "启动";
        // 
        // button2
        // 
        button2.Dock = System.Windows.Forms.DockStyle.Fill;
        button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        button2.Font = new System.Drawing.Font("Consolas", 8F);
        button2.Location = new System.Drawing.Point(78, 0);
        button2.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        button2.Name = "button2";
        button2.Size = new System.Drawing.Size(72, 24);
        button2.TabIndex = 1;
        button2.Text = "重连";
        // 
        // _btnMirrorRotate
        // 
        _btnMirrorRotate.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorRotate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorRotate.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorRotate.Location = new System.Drawing.Point(0, 30);
        _btnMirrorRotate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorRotate.Name = "_btnMirrorRotate";
        _btnMirrorRotate.Size = new System.Drawing.Size(72, 24);
        _btnMirrorRotate.TabIndex = 2;
        _btnMirrorRotate.Text = "旋转";
        // 
        // _btnMirrorScreenshot
        // 
        _btnMirrorScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorScreenshot.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorScreenshot.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorScreenshot.Location = new System.Drawing.Point(78, 30);
        _btnMirrorScreenshot.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorScreenshot.Name = "_btnMirrorScreenshot";
        _btnMirrorScreenshot.Size = new System.Drawing.Size(72, 24);
        _btnMirrorScreenshot.TabIndex = 3;
        _btnMirrorScreenshot.Text = "截图";
        // 
        // _btnMirrorPopout
        // 
        tableLayoutPanel2.SetColumnSpan(_btnMirrorPopout, 2);
        _btnMirrorPopout.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorPopout.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorPopout.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorPopout.Location = new System.Drawing.Point(0, 60);
        _btnMirrorPopout.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorPopout.Name = "_btnMirrorPopout";
        _btnMirrorPopout.Size = new System.Drawing.Size(150, 24);
        _btnMirrorPopout.TabIndex = 4;
        _btnMirrorPopout.Text = "弹出";
        // 
        // _cmbDevices
        // 
        _cmbDevices.Dock = System.Windows.Forms.DockStyle.Top;
        _cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        _cmbDevices.Font = new System.Drawing.Font("Consolas", 9F);
        _cmbDevices.Location = new System.Drawing.Point(3, 33);
        _cmbDevices.Name = "_cmbDevices";
        _cmbDevices.Size = new System.Drawing.Size(150, 22);
        _cmbDevices.TabIndex = 3;
        // 
        // _btnRefreshAdb
        // 
        _btnRefreshAdb.Dock = System.Windows.Forms.DockStyle.Top;
        _btnRefreshAdb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnRefreshAdb.Font = new System.Drawing.Font("Consolas", 8F);
        _btnRefreshAdb.Location = new System.Drawing.Point(3, 3);
        _btnRefreshAdb.Name = "_btnRefreshAdb";
        _btnRefreshAdb.Size = new System.Drawing.Size(150, 24);
        _btnRefreshAdb.TabIndex = 2;
        _btnRefreshAdb.Text = "扫描 ADB";
        // 
        // DevicePanel
        // 
        BackColor = System.Drawing.SystemColors.Control;
        Controls.Add(tableLayoutPanel1);
        Padding = new System.Windows.Forms.Padding(8);
        Size = new System.Drawing.Size(172, 367);
        _buttonBar.ResumeLayout(false);
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel2.ResumeLayout(false);
        ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;

    private LogViewer.UI.MirrorHostPanel _mirrorHostPanel;

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
    private System.Windows.Forms.Button button1;
    private System.Windows.Forms.Button button2;
    private System.Windows.Forms.Button _btnMirrorRotate;
    private System.Windows.Forms.Button _btnMirrorScreenshot;
    private System.Windows.Forms.Button _btnMirrorPopout;

    private System.Windows.Forms.TableLayoutPanel _buttonBar;
    private System.Windows.Forms.Button _btnMirrorToggle;
    private System.Windows.Forms.Button _btnMirrorReconnect;

    #endregion

    private System.Windows.Forms.ComboBox _cmbDevices;
    private System.Windows.Forms.Button _btnRefreshAdb;
    private System.Windows.Forms.Label _lblMirrorStatus;
}
