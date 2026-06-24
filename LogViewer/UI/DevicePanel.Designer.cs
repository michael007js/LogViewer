using LogViewer.Static;

namespace LogViewer.UI;

partial class DevicePanel
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
        _container = new System.Windows.Forms.TableLayoutPanel();
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
        _container.SuspendLayout();
        _mirrorHostPanel.SuspendLayout();
        _buttonBar.SuspendLayout();
        SuspendLayout();
        // 
        // _container
        // 
        _container.ColumnCount = 1;
        _container.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _container.Controls.Add(_cmbDevices, 0, 0);
        _container.Controls.Add(_btnRefreshAdb, 0, 1);
        _container.Controls.Add(_mirrorHostPanel, 0, 2);
        _container.Controls.Add(_lblMirrorStatus, 0, 3);
        _container.Controls.Add(_buttonBar, 0, 4);
        _container.Dock = System.Windows.Forms.DockStyle.Fill;
        _container.Location = new System.Drawing.Point(8, 8);
        _container.Name = "_container";
        _container.RowCount = 5;
        _container.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
        _container.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _container.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _container.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
        _container.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 102F));
        _container.Size = new System.Drawing.Size(134, 367);
        _container.TabIndex = 0;
        // 
        // _cmbDevices
        // 
        _cmbDevices.Dock = System.Windows.Forms.DockStyle.Top;
        _cmbDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        _cmbDevices.Font = new System.Drawing.Font("Consolas", 9F);
        _cmbDevices.Location = new System.Drawing.Point(3, 3);
        _cmbDevices.Name = "_cmbDevices";
        _cmbDevices.Size = new System.Drawing.Size(128, 22);
        _cmbDevices.TabIndex = 0;
        // 
        // _btnRefreshAdb
        // 
        _btnRefreshAdb.Dock = System.Windows.Forms.DockStyle.Top;
        _btnRefreshAdb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnRefreshAdb.Font = new System.Drawing.Font("Consolas", 8F);
        _btnRefreshAdb.Location = new System.Drawing.Point(3, 31);
        _btnRefreshAdb.Name = "_btnRefreshAdb";
        _btnRefreshAdb.Size = new System.Drawing.Size(128, 24);
        _btnRefreshAdb.TabIndex = 1;
        _btnRefreshAdb.Text = "扫描 ADB";
        // 
        // _mirrorHostPanel
        // 
        _mirrorHostPanel.BackColor = System.Drawing.Color.Black;
        _mirrorHostPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        _mirrorHostPanel.Controls.Add(_lblMirrorPlaceholder);
        _mirrorHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        _mirrorHostPanel.Location = new System.Drawing.Point(0, 66);
        _mirrorHostPanel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 8);
        _mirrorHostPanel.Name = "_mirrorHostPanel";
        _mirrorHostPanel.Size = new System.Drawing.Size(134, 155);
        _mirrorHostPanel.TabIndex = 2;

        // 
        // _lblMirrorPlaceholder
        // 
        _lblMirrorPlaceholder.BackColor = System.Drawing.Color.Black;
        _lblMirrorPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
        _lblMirrorPlaceholder.ForeColor = System.Drawing.Color.Gainsboro;
        _lblMirrorPlaceholder.Location = new System.Drawing.Point(0, 0);
        _lblMirrorPlaceholder.Name = "_lblMirrorPlaceholder";
        _lblMirrorPlaceholder.Size = new System.Drawing.Size(132, 153);
        _lblMirrorPlaceholder.TabIndex = 1;
        _lblMirrorPlaceholder.Text = "请选择具体设备以操控手机";
        _lblMirrorPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // _lblMirrorStatus
        // 
        _lblMirrorStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
        _lblMirrorStatus.ForeColor = System.Drawing.Color.DimGray;
        _lblMirrorStatus.Location = new System.Drawing.Point(3, 229);
        _lblMirrorStatus.Name = "_lblMirrorStatus";
        _lblMirrorStatus.Padding = new System.Windows.Forms.Padding(2, 0, 2, 0);
        _lblMirrorStatus.Size = new System.Drawing.Size(128, 36);
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
        _buttonBar.Location = new System.Drawing.Point(0, 265);
        _buttonBar.Margin = new System.Windows.Forms.Padding(0);
        _buttonBar.Name = "_buttonBar";
        _buttonBar.RowCount = 3;
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
        _buttonBar.Size = new System.Drawing.Size(134, 102);
        _buttonBar.TabIndex = 4;
        // 
        // _btnMirrorToggle
        // 
        _btnMirrorToggle.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorToggle.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorToggle.Location = new System.Drawing.Point(0, 0);
        _btnMirrorToggle.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorToggle.Name = "_btnMirrorToggle";
        _btnMirrorToggle.Size = new System.Drawing.Size(61, 24);
        _btnMirrorToggle.TabIndex = 0;
        _btnMirrorToggle.Text = "启动";
        // 
        // _btnMirrorReconnect
        // 
        _btnMirrorReconnect.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorReconnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorReconnect.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorReconnect.Location = new System.Drawing.Point(67, 0);
        _btnMirrorReconnect.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorReconnect.Name = "_btnMirrorReconnect";
        _btnMirrorReconnect.Size = new System.Drawing.Size(61, 24);
        _btnMirrorReconnect.TabIndex = 1;
        _btnMirrorReconnect.Text = "重连";
        // 
        // _btnMirrorRotate
        // 
        _btnMirrorRotate.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorRotate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorRotate.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorRotate.Location = new System.Drawing.Point(0, 30);
        _btnMirrorRotate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorRotate.Name = "_btnMirrorRotate";
        _btnMirrorRotate.Size = new System.Drawing.Size(61, 24);
        _btnMirrorRotate.TabIndex = 2;
        _btnMirrorRotate.Text = "旋转";
        // 
        // _btnMirrorScreenshot
        // 
        _btnMirrorScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorScreenshot.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorScreenshot.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorScreenshot.Location = new System.Drawing.Point(67, 30);
        _btnMirrorScreenshot.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorScreenshot.Name = "_btnMirrorScreenshot";
        _btnMirrorScreenshot.Size = new System.Drawing.Size(61, 24);
        _btnMirrorScreenshot.TabIndex = 3;
        _btnMirrorScreenshot.Text = "截图";
        // 
        // _btnMirrorPopout
        // 
        _buttonBar.SetColumnSpan(_btnMirrorPopout, 2);
        _btnMirrorPopout.Dock = System.Windows.Forms.DockStyle.Fill;
        _btnMirrorPopout.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        _btnMirrorPopout.Font = new System.Drawing.Font("Consolas", 8F);
        _btnMirrorPopout.Location = new System.Drawing.Point(0, 60);
        _btnMirrorPopout.Margin = new System.Windows.Forms.Padding(0, 0, 6, 6);
        _btnMirrorPopout.Name = "_btnMirrorPopout";
        _btnMirrorPopout.Size = new System.Drawing.Size(128, 36);
        _btnMirrorPopout.TabIndex = 4;
        _btnMirrorPopout.Text = "弹出";
        // 
        // DevicePanel
        // 
        BackColor = System.Drawing.SystemColors.Control;
        Controls.Add(_container);
        Padding = new System.Windows.Forms.Padding(8);
        Size = new System.Drawing.Size(150, 383);
        _container.ResumeLayout(false);
        _mirrorHostPanel.ResumeLayout(false);
        _buttonBar.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _container;
    private System.Windows.Forms.TableLayoutPanel _buttonBar;
    private ComboBox _cmbDevices;
    private Button _btnRefreshAdb;
    private System.Windows.Forms.Panel _mirrorHostPanel;

    private System.Windows.Forms.Label _lblMirrorPlaceholder;
    private System.Windows.Forms.Label _lblMirrorStatus;
    private Button _btnMirrorToggle;
    private Button _btnMirrorReconnect;
    private Button _btnMirrorRotate;
    private Button _btnMirrorScreenshot;
    private Button _btnMirrorPopout;
}
