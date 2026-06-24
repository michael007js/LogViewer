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
        _container = new TableLayoutPanel();
        _cmbDevices = new ComboBox();
        _btnRefreshAdb = new Button();
        _mirrorHostPanel = new Panel();
        _mirrorViewportPanel = new Panel();
        _lblMirrorPlaceholder = new Label();
        _lblMirrorStatus = new Label();
        _buttonBar = new TableLayoutPanel();
        _btnMirrorToggle = new Button();
        _btnMirrorReconnect = new Button();
        _btnMirrorRotate = new Button();
        _btnMirrorScreenshot = new Button();
        _btnMirrorPopout = new Button();
        _container.SuspendLayout();
        _mirrorHostPanel.SuspendLayout();
        _buttonBar.SuspendLayout();
        SuspendLayout();
        // 
        // _cmbDevices
        // 
        _cmbDevices.Dock = DockStyle.Top;
        _cmbDevices.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDevices.Font = new Font("Consolas", 9F);
        _cmbDevices.Height = 28;
        _cmbDevices.SelectedIndexChanged += new EventHandler(OnDeviceSelected);
        // 
        // _btnRefreshAdb
        // 
        _btnRefreshAdb.Dock = DockStyle.Top;
        _btnRefreshAdb.FlatStyle = FlatStyle.Flat;
        _btnRefreshAdb.Font = new Font("Consolas", 8F);
        _btnRefreshAdb.Height = 26;
        _btnRefreshAdb.Text = Language.ScanAdb;
        _btnRefreshAdb.Click += new EventHandler(OnRefreshAdbClick);
        // 
        // _mirrorHostPanel
        // 
        _mirrorHostPanel.BackColor = Color.Black;
        _mirrorHostPanel.BorderStyle = BorderStyle.FixedSingle;
        _mirrorHostPanel.Dock = DockStyle.Fill;
        _mirrorHostPanel.Margin = new Padding(0, 8, 0, 8);
        _mirrorHostPanel.Resize += new EventHandler(OnMirrorHostResized);
        _mirrorHostPanel.SizeChanged += new EventHandler(OnMirrorHostResized);
        _mirrorHostPanel.VisibleChanged += new EventHandler(OnMirrorHostResized);
        _mirrorHostPanel.Layout += new LayoutEventHandler(OnMirrorHostLayoutChanged);
        // 
        // _mirrorViewportPanel
        // 
        _mirrorViewportPanel.BackColor = Color.Black;
        _mirrorViewportPanel.Visible = false;
        // 
        // _lblMirrorPlaceholder
        // 
        _lblMirrorPlaceholder.BackColor = Color.Black;
        _lblMirrorPlaceholder.Dock = DockStyle.Fill;
        _lblMirrorPlaceholder.ForeColor = Color.Gainsboro;
        _lblMirrorPlaceholder.Text = Language.DeviceSelectPrompt;
        _lblMirrorPlaceholder.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // _lblMirrorStatus
        // 
        _lblMirrorStatus.Dock = DockStyle.Bottom;
        _lblMirrorStatus.ForeColor = Color.DimGray;
        _lblMirrorStatus.Height = 36;
        _lblMirrorStatus.Padding = new Padding(2, 0, 2, 0);
        _lblMirrorStatus.Text = Language.DeviceSelectPrompt;
        _lblMirrorStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _btnMirrorToggle
        // 
        _btnMirrorToggle.Dock = DockStyle.Fill;
        _btnMirrorToggle.FlatStyle = FlatStyle.Flat;
        _btnMirrorToggle.Font = new Font("Consolas", 8F);
        _btnMirrorToggle.Height = 28;
        _btnMirrorToggle.Margin = new Padding(0, 0, 6, 6);
        _btnMirrorToggle.Text = Language.Start;
        _btnMirrorToggle.Click += new EventHandler(OnMirrorToggleClick);
        // 
        // _btnMirrorReconnect
        // 
        _btnMirrorReconnect.Dock = DockStyle.Fill;
        _btnMirrorReconnect.FlatStyle = FlatStyle.Flat;
        _btnMirrorReconnect.Font = new Font("Consolas", 8F);
        _btnMirrorReconnect.Height = 28;
        _btnMirrorReconnect.Margin = new Padding(0, 0, 6, 6);
        _btnMirrorReconnect.Text = Language.Reconnect;
        _btnMirrorReconnect.Click += new EventHandler(OnMirrorReconnectClick);
        // 
        // _btnMirrorRotate
        // 
        _btnMirrorRotate.Dock = DockStyle.Fill;
        _btnMirrorRotate.FlatStyle = FlatStyle.Flat;
        _btnMirrorRotate.Font = new Font("Consolas", 8F);
        _btnMirrorRotate.Height = 28;
        _btnMirrorRotate.Margin = new Padding(0, 0, 6, 6);
        _btnMirrorRotate.Text = Language.Rotate;
        _btnMirrorRotate.Click += new EventHandler(OnMirrorRotateClick);
        // 
        // _btnMirrorScreenshot
        // 
        _btnMirrorScreenshot.Dock = DockStyle.Fill;
        _btnMirrorScreenshot.FlatStyle = FlatStyle.Flat;
        _btnMirrorScreenshot.Font = new Font("Consolas", 8F);
        _btnMirrorScreenshot.Height = 28;
        _btnMirrorScreenshot.Margin = new Padding(0, 0, 6, 6);
        _btnMirrorScreenshot.Text = Language.Screenshot;
        _btnMirrorScreenshot.Click += new EventHandler(OnMirrorScreenshotClick);
        // 
        // _btnMirrorPopout
        // 
        _btnMirrorPopout.Dock = DockStyle.Fill;
        _btnMirrorPopout.FlatStyle = FlatStyle.Flat;
        _btnMirrorPopout.Font = new Font("Consolas", 8F);
        _btnMirrorPopout.Height = 28;
        _btnMirrorPopout.Margin = new Padding(0, 0, 6, 6);
        _btnMirrorPopout.Text = Language.Popout;
        _btnMirrorPopout.Click += new EventHandler(OnMirrorPopoutClick);
        // 
        // _buttonBar
        // 
        _buttonBar.ColumnCount = 2;
        _buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _buttonBar.Dock = DockStyle.Bottom;
        _buttonBar.Height = 102;
        _buttonBar.Margin = new Padding(0);
        _buttonBar.RowCount = 3;
        _buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _buttonBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _buttonBar.Controls.Add(_btnMirrorToggle, 0, 0);
        _buttonBar.Controls.Add(_btnMirrorReconnect, 1, 0);
        _buttonBar.Controls.Add(_btnMirrorRotate, 0, 1);
        _buttonBar.Controls.Add(_btnMirrorScreenshot, 1, 1);
        _buttonBar.Controls.Add(_btnMirrorPopout, 0, 2);
        _buttonBar.SetColumnSpan(_btnMirrorPopout, 2);
        // 
        // _mirrorHostPanel
        // 
        _mirrorHostPanel.Controls.Add(_mirrorViewportPanel);
        _mirrorHostPanel.Controls.Add(_lblMirrorPlaceholder);
        // 
        // _container
        // 
        _container.ColumnCount = 1;
        _container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _container.Controls.Add(_cmbDevices, 0, 0);
        _container.Controls.Add(_btnRefreshAdb, 0, 1);
        _container.Controls.Add(_mirrorHostPanel, 0, 2);
        _container.Controls.Add(_lblMirrorStatus, 0, 3);
        _container.Controls.Add(_buttonBar, 0, 4);
        _container.Dock = DockStyle.Fill;
        _container.RowCount = 5;
        _container.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _container.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _container.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        _container.RowStyles.Add(new RowStyle(SizeType.Absolute, 102F));
        // 
        // DevicePanel
        // 
        BackColor = SystemColors.Control;
        Padding = new Padding(8);
        Controls.Add(_container);
        Resize += new EventHandler(OnPanelResized);
        Layout += new LayoutEventHandler(OnPanelLayoutChanged);
        _container.ResumeLayout(false);
        _mirrorHostPanel.ResumeLayout(false);
        _buttonBar.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _container;
    private TableLayoutPanel _buttonBar;
    private ComboBox _cmbDevices;
    private Button _btnRefreshAdb;
    private Panel _mirrorHostPanel;
    private Panel _mirrorViewportPanel;
    private Label _lblMirrorPlaceholder;
    private Label _lblMirrorStatus;
    private Button _btnMirrorToggle;
    private Button _btnMirrorReconnect;
    private Button _btnMirrorRotate;
    private Button _btnMirrorScreenshot;
    private Button _btnMirrorPopout;
}
