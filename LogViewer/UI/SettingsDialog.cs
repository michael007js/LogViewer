using LogViewer.Models;
using LogViewer.Utils;

namespace LogViewer.UI;

public class SettingsDialog : Form
{
    private readonly AppSettings _settings;

    private NumericUpDown _nudPort = null!;
    private NumericUpDown _nudMaxPerDevice = null!;
    private NumericUpDown _nudMaxAll = null!;
    private NumericUpDown _nudMaxSystemLog = null!;
    private NumericUpDown _nudAndroidQueue = null!;
    private NumericUpDown _nudMaxBodySize = null!;
    private CheckBox _chkAutoAdb = null!;
    private CheckBox _chkAutoLogcat = null!;
    private CheckBox _chkAutoFormatJson = null!;
    private NumericUpDown _nudFontSize = null!;
    private NumericUpDown _nudAdbScanInterval = null!;
    private TextBox _txtLogcatFilter = null!;
    private CheckBox _chkAutoStartScrcpy = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public SettingsDialog(AppSettings settings, AdbHelper adbHelper)
    {
        _settings = settings;
        InitializeComponents();
        LoadValues();
    }

    private void InitializeComponents()
    {
        Text = "Settings";
        Size = new Size(500, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9f);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 16,
            Padding = new Padding(15),
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        var row = 0;
        AddRow(panel, row++, "Server Port:", _nudPort = CreateNumeric(1024, 65535, _settings.ServerPort));
        AddRow(panel, row++, "Max Logs Per Device:", _nudMaxPerDevice = CreateNumeric(100, 100000, _settings.MaxLogEntriesPerDevice));
        AddRow(panel, row++, "Max Logs All Devices:", _nudMaxAll = CreateNumeric(100, 100000, _settings.MaxLogEntriesAll));
        AddRow(panel, row++, "System Log Hot Cache:", _nudMaxSystemLog = CreateNumeric(100, 100000, _settings.MaxSystemLogEntries));
        AddRow(panel, row++, "Android Send Queue*:", _nudAndroidQueue = CreateNumeric(100, 10000, _settings.AndroidQueueSize));
        AddRow(panel, row++, "Body Truncate (KB)*:", _nudMaxBodySize = CreateNumeric(10, 1024, _settings.MaxBodySizeKb));

        _chkAutoAdb = new CheckBox { Text = "Auto ADB Reverse on start", Checked = _settings.AutoAdbReverse };
        panel.Controls.Add(_chkAutoAdb, 0, row);
        panel.SetColumnSpan(_chkAutoAdb, 3);
        row++;

        _chkAutoLogcat = new CheckBox { Text = "Auto start Logcat on device connect", Checked = _settings.AutoStartLogcat };
        panel.Controls.Add(_chkAutoLogcat, 0, row);
        panel.SetColumnSpan(_chkAutoLogcat, 3);
        row++;

        _chkAutoStartScrcpy = new CheckBox { Text = "Auto start scrcpy when selecting a device", Checked = _settings.AutoStartScrcpyForSelectedDevice };
        panel.Controls.Add(_chkAutoStartScrcpy, 0, row);
        panel.SetColumnSpan(_chkAutoStartScrcpy, 3);
        row++;

        _chkAutoFormatJson = new CheckBox { Text = "Auto format JSON with folding", Checked = _settings.AutoFormatJson };
        panel.Controls.Add(_chkAutoFormatJson, 0, row);
        panel.SetColumnSpan(_chkAutoFormatJson, 3);
        row++;

        AddRow(panel, row++, "Font Size (pt):", _nudFontSize = CreateNumeric(8, 24, _settings.FontSize));
        AddRow(panel, row++, "ADB Scan Interval (ms):", _nudAdbScanInterval = CreateNumeric(500, 30000, _settings.AdbScanIntervalMs));

        panel.Controls.Add(new Label { Text = "Logcat Filter:", AutoSize = true }, 0, row);
        _txtLogcatFilter = new TextBox { Text = _settings.LogcatFilter, Dock = DockStyle.Fill };
        panel.Controls.Add(_txtLogcatFilter, 1, row);
        panel.SetColumnSpan(_txtLogcatFilter, 2);
        row++;

        var noteLabel = new Label
        {
            Text = "* Requires modifying AppConstant.java and\n  rebuilding the Android app to take effect.",
            ForeColor = Color.Gray,
            AutoSize = true
        };
        panel.Controls.Add(noteLabel, 0, row);
        panel.SetColumnSpan(noteLabel, 3);
        row++;

        var filterNote = new Label
        {
            Text = "Logcat filter: empty=all, e.g. ActivityManager:I *:S",
            ForeColor = Color.Gray,
            AutoSize = true
        };
        panel.Controls.Add(filterNote, 0, row);
        panel.SetColumnSpan(filterNote, 3);

        Controls.Add(panel);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 45,
            Padding = new Padding(10)
        };

        _btnCancel = new Button { Text = "Cancel", Size = new Size(90, 30), DialogResult = DialogResult.Cancel };
        _btnOk = new Button { Text = "OK", Size = new Size(90, 30) };
        _btnOk.Click += OnOkClick;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnOk);
        Controls.Add(btnPanel);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, 2);
    }

    private static NumericUpDown CreateNumeric(int min, int max, int value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Dock = DockStyle.Fill
        };
    }

    private void LoadValues()
    {
        _nudPort.Value = _settings.ServerPort;
        _nudMaxPerDevice.Value = _settings.MaxLogEntriesPerDevice;
        _nudMaxAll.Value = _settings.MaxLogEntriesAll;
        _nudMaxSystemLog.Value = _settings.MaxSystemLogEntries;
        _nudAndroidQueue.Value = _settings.AndroidQueueSize;
        _nudMaxBodySize.Value = _settings.MaxBodySizeKb;
        _chkAutoAdb.Checked = _settings.AutoAdbReverse;
        _chkAutoLogcat.Checked = _settings.AutoStartLogcat;
        _chkAutoFormatJson.Checked = _settings.AutoFormatJson;
        _nudFontSize.Value = _settings.FontSize;
        _nudAdbScanInterval.Value = _settings.AdbScanIntervalMs;
        _txtLogcatFilter.Text = _settings.LogcatFilter;
        _chkAutoStartScrcpy.Checked = _settings.AutoStartScrcpyForSelectedDevice;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        _settings.ServerPort = (int)_nudPort.Value;
        _settings.MaxLogEntriesPerDevice = (int)_nudMaxPerDevice.Value;
        _settings.MaxLogEntriesAll = (int)_nudMaxAll.Value;
        _settings.MaxSystemLogEntries = (int)_nudMaxSystemLog.Value;
        _settings.AndroidQueueSize = (int)_nudAndroidQueue.Value;
        _settings.MaxBodySizeKb = (int)_nudMaxBodySize.Value;
        _settings.AutoAdbReverse = _chkAutoAdb.Checked;
        _settings.AutoStartLogcat = _chkAutoLogcat.Checked;
        _settings.AutoFormatJson = _chkAutoFormatJson.Checked;
        _settings.FontSize = (int)_nudFontSize.Value;
        _settings.AdbScanIntervalMs = (int)_nudAdbScanInterval.Value;
        _settings.LogcatFilter = _txtLogcatFilter.Text;
        _settings.AutoStartScrcpyForSelectedDevice = _chkAutoStartScrcpy.Checked;
        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
