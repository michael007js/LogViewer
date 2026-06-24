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

    private void InitializeComponent()
    {
        _cmbDevices = new System.Windows.Forms.ComboBox();
        _btnRefreshAdb = new System.Windows.Forms.Button();
        _mirrorHostPanel = new System.Windows.Forms.Panel();
        _lblMirrorPlaceholder = new System.Windows.Forms.Label();
        _lblMirrorStatus = new System.Windows.Forms.Label();
        _buttonBar = new System.Windows.Forms.TableLayoutPanel();
        _btnMirrorToggle = new System.Windows.Forms.Button();
        _btnMirrorReconnect = new System.Windows.Forms.Button();
        _btnMirrorRotate = new System.Windows.Forms.Button();
        _btnMirrorScreenshot = new System.Windows.Forms.Button();
        _btnMirrorPopout = new System.Windows.Forms.Button();
        _mirrorHostPanel.SuspendLayout();
        _buttonBar.SuspendLayout();
        SuspendLayout();
        // 
        // _cmbDevices
        // 
        _cmbDevices.Dock = System.Windows.Forms.DockStyle.Top;
        _cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        _cmbDevices.Font = new System.Drawing.Font("Consolas", 9F);
        _cmbDevices.Name = "_cmbDevices";
        _cmbDevices.Size = new System.Drawing.Size(150, 22);
        _cmbDevices.TabIndex = 0;
        // 
        // _btnRefreshAdb
        // 
        _btnRefreshAdb.Dock = System.Windows.Forms.DockStyle.Top;
        _btnRefreshAdb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnRefreshAdb.Font = new System.Drawing.Font("Consolas", 8F);
        _btnRefreshAdb.Name = "_btnRefreshAdb";
        _btnRefreshAdb.Size = new System.Drawing.Size(150, 24);
        _btnRefreshAdb.TabIndex = 1;
        _btnRefreshAdb.Text = "扫描 ADB";
        // 
        // _mirrorHostPanel
        // 
        _mirrorHostPanel.BackColor = System.Drawing.Color.Black;
        _mirrorHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _mirrorHostPanel.Controls.Add(_lblMirrorPlaceholder);
        _mirrorHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _mirrorHostPanel.Name = "_mirrorHostPanel";
        _mirrorHostPanel.TabIndex = 2;
        // 
        // _lblMirrorPlaceholder
        // 
        _lblMirrorPlaceholder.BackColor = System.Drawing.Color.Black;
        _lblMirrorPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
        _lblMirrorPlaceholder.ForeColor = System.Drawing.Color.Gainsboro;
        _lblMirrorPlaceholder.Name = "_lblMirrorPlaceholder";
        _lblMirrorPlaceholder.TabIndex = 1;
        _lblMirrorPlaceholder.Text = "请选择具体设备以操控手机";
        _lblMirrorPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // _lblMirrorStatus
        // 
        _lblMirrorStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
        _lblMirrorStatus.ForeColor = System.Drawing.Color.DimGray;
        _lblMirrorStatus.Name = "_lblMirrorStatus";
        _lblMirrorStatus.Padding = new System.Windows.Forms.Padding(2, 0, 2, 0);
        _lblMirrorStatus.Size = new System.Drawing.Size(150, 36);
        _lblMirrorStatus.TabIndex = 3;
        _lblMirrorStatus.Text = "请选择具体设备以操控手机";
        _lblMirrorStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // _buttonBar
        // 
        _buttonBar.ColumnCount = 2;
        _buttonBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _buttonBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        _buttonBar.Controls.Add(_btnMirrorToggle, 0, 0);
        _buttonBar.Controls.Add(_btnMirrorReconnect, 1, 0);
        _buttonBar.Controls.Add(_btnMirrorRotate, 0, 1);
        _buttonBar.Controls.Add(_btnMirrorScreenshot, 1, 1);
        _buttonBar.Controls.Add(_btnMirrorPopout, 0, 2);
        _buttonBar.Dock = System.Windows.Forms.DockStyle.Bottom;
        _buttonBar.Margin = new System.Windows.Forms.Padding(0);
        _buttonBar.Name = "_buttonBar";
        _buttonBar.RowCount = 3;
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.Size = new System.Drawing.Size(150, 102);
        _buttonBar.TabIndex = 4;
        // 
        // _btnMirrorToggle
        // 
        _btnMirrorToggle.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorToggle.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorToggle.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorToggle.Name = "_btnMirrorToggle";
        _btnMirrorToggle.TabIndex = 0;
        _btnMirrorToggle.Text = "启动";
        // 
        // _btnMirrorReconnect
        // 
        _btnMirrorReconnect.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorReconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorReconnect.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorReconnect.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorReconnect.Name = "_btnMirrorReconnect";
        _btnMirrorReconnect.TabIndex = 1;
        _btnMirrorReconnect.Text = "重连";
        // 
        // _btnMirrorRotate
        // 
        _btnMirrorRotate.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorRotate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorRotate.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorRotate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorRotate.Name = "_btnMirrorRotate";
        _btnMirrorRotate.TabIndex = 2;
        _btnMirrorRotate.Text = "旋转";
        // 
        // _btnMirrorScreenshot
        // 
        _btnMirrorScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorScreenshot.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorScreenshot.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorScreenshot.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorScreenshot.Name = "_btnMirrorScreenshot";
        _btnMirrorScreenshot.TabIndex = 3;
        _btnMirrorScreenshot.Text = "截图";
        // 
        // _btnMirrorPopout
        // 
        _buttonBar.SetColumnSpan(_btnMirrorPopout, 2);
        _btnMirrorPopout.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorPopout.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorPopout.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorPopout.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorPopout.Name = "_btnMirrorPopout";
        _btnMirrorPopout.TabIndex = 4;
        _btnMirrorPopout.Text = "弹出";
        // 
        // DevicePanel — Dock布局: Top→Top→Bottom→Bottom→Fill
        // 
        BackColor = System.Drawing.SystemColors.Control;
        Padding = new System.Windows.Forms.Padding(8);
        Size = new System.Drawing.Size(172, 434);
        Controls.Add(_cmbDevices);
        Controls.Add(_btnRefreshAdb);
        Controls.Add(_buttonBar);
        Controls.Add(_lblMirrorStatus);
        Controls.Add(_mirrorHostPanel);
        _mirrorHostPanel.ResumeLayout(false);
        _buttonBar.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _buttonBar;
    private System.Windows.Forms.ComboBox _cmbDevices;
    private System.Windows.Forms.Button _btnRefreshAdb;
    private System.Windows.Forms.Panel _mirrorHostPanel;
    private System.Windows.Forms.Label _lblMirrorPlaceholder;
    private System.Windows.Forms.Label _lblMirrorStatus;
    private System.Windows.Forms.Button _btnMirrorToggle;
    private System.Windows.Forms.Button _btnMirrorReconnect;
    private System.Windows.Forms.Button _btnMirrorRotate;
    private System.Windows.Forms.Button _btnMirrorScreenshot;
    private System.Windows.Forms.Button _btnMirrorPopout;
}
