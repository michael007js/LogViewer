using LogViewer.Models;
using LogViewer.Utils;

namespace LogViewer.UI;

public class SettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly AdbHelper _adbHelper;
    private readonly ScrcpyManager _scrcpyManager = new();

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
    private TextBox _txtAdbPath = null!;
    private TextBox _txtScrcpyPath = null!;
    private Button _btnBrowseAdb = null!;
    private Button _btnAutoDetectAdb = null!;
    private Button _btnBrowseScrcpy = null!;
    private Button _btnDeployScrcpy = null!;
    private Label _lblAdbStatus = null!;
    private Label _lblScrcpyStatus = null!;
    private CheckBox _chkAutoStartScrcpy = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public SettingsDialog(AppSettings settings, AdbHelper adbHelper)
    {
        _settings = settings;
        _adbHelper = adbHelper;
        if (!string.IsNullOrEmpty(_settings.ScrcpyPath))
        {
            _scrcpyManager.SetManualPath(_settings.ScrcpyPath);
        }
        InitializeComponents();
        LoadValues();
    }

    private void InitializeComponents()
    {
        Text = "Settings";
        Size = new Size(500, 700);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9f);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 20,
            Padding = new Padding(15),
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        int row = 0;

        var adbLabel = new Label { Text = "ADB Path:", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _txtAdbPath = new TextBox { Text = _settings.AdbPath, Dock = DockStyle.Fill };
        _btnBrowseAdb = new Button { Text = "Browse...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
        _btnBrowseAdb.Click += OnBrowseAdb;
        _btnAutoDetectAdb = new Button { Text = "Auto Detect", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
        _btnAutoDetectAdb.Click += OnAutoDetectAdb;

        panel.Controls.Add(adbLabel, 0, row);
        panel.Controls.Add(_txtAdbPath, 1, row);
        panel.Controls.Add(_btnBrowseAdb, 2, row);
        row++;

        _lblAdbStatus = new Label { Text = "", Dock = DockStyle.Fill, AutoSize = true };
        panel.Controls.Add(_lblAdbStatus, 0, row);
        panel.SetColumnSpan(_lblAdbStatus, 2);
        panel.Controls.Add(_btnAutoDetectAdb, 2, row);
        row++;

        var scrcpyLabel = new Label { Text = "scrcpy Override:", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _txtScrcpyPath = new TextBox { Text = _settings.ScrcpyPath, Dock = DockStyle.Fill };
        _btnBrowseScrcpy = new Button { Text = "Browse...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
        _btnBrowseScrcpy.Click += OnBrowseScrcpy;
        _btnDeployScrcpy = new Button { Text = "Deploy / Repair", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
        _btnDeployScrcpy.Click += OnDeployScrcpy;

        panel.Controls.Add(scrcpyLabel, 0, row);
        panel.Controls.Add(_txtScrcpyPath, 1, row);
        panel.Controls.Add(_btnBrowseScrcpy, 2, row);
        row++;

        _lblScrcpyStatus = new Label { Text = "", Dock = DockStyle.Fill, AutoSize = true };
        panel.Controls.Add(_lblScrcpyStatus, 0, row);
        panel.SetColumnSpan(_lblScrcpyStatus, 2);
        panel.Controls.Add(_btnDeployScrcpy, 2, row);
        row++;

        row++;

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
        row++;

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

        ValidateAdbPath();
        ValidateScrcpyPath();
    }

    private void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, 2);
    }

    private NumericUpDown CreateNumeric(int min, int max, int value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Dock = DockStyle.Fill
        };
    }

    private void OnBrowseAdb(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "ADB executable|adb.exe|All executables|*.exe|All files|*.*",
            Title = "Select ADB executable",
            CheckFileExists = true
        };
        if (!string.IsNullOrEmpty(_txtAdbPath.Text) && File.Exists(_txtAdbPath.Text))
        {
            try { dlg.InitialDirectory = Path.GetDirectoryName(_txtAdbPath.Text); } catch { }
        }
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtAdbPath.Text = dlg.FileName;
            ValidateAdbPath();
        }
    }

    private void OnAutoDetectAdb(object? sender, EventArgs e)
    {
        var paths = _adbHelper.GetSearchPaths();
        var found = paths.FirstOrDefault(p => _adbHelper.ValidateAdb(p));

        if (found != null)
        {
            _txtAdbPath.Text = found;
            ValidateAdbPath();
        }
        else
        {
            _lblAdbStatus.Text = "\u2716 ADB not found in any searched location";
            _lblAdbStatus.ForeColor = Color.Red;
            MessageBox.Show(
                "ADB could not be found automatically.\n\nSearched locations:\n" +
                paths.Where(File.Exists).Aggregate("", (acc, p) => acc + p + "\n") +
                "\nPlease install Android SDK Platform Tools and set the path manually.",
                "ADB Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnBrowseScrcpy(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "scrcpy executable|scrcpy.exe|All executables|*.exe|All files|*.*",
            Title = "Select scrcpy executable",
            CheckFileExists = true
        };
        if (!string.IsNullOrEmpty(_txtScrcpyPath.Text) && File.Exists(_txtScrcpyPath.Text))
        {
            try { dlg.InitialDirectory = Path.GetDirectoryName(_txtScrcpyPath.Text); } catch { }
        }
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtScrcpyPath.Text = dlg.FileName;
            _scrcpyManager.SetManualPath(dlg.FileName);
            ValidateScrcpyPath();
        }
    }

    private async void OnDeployScrcpy(object? sender, EventArgs e)
    {
        _btnDeployScrcpy.Enabled = false;
        _lblScrcpyStatus.Text = "正在部署 scrcpy...";
        _lblScrcpyStatus.ForeColor = Color.DarkOrange;

        try
        {
            var progress = new Progress<string>(message =>
            {
                _lblScrcpyStatus.Text = message;
                _lblScrcpyStatus.ForeColor = Color.DarkOrange;
            });

            var path = await _scrcpyManager.EnsureScrcpyAvailableAsync(forceDeploy: true, progress, CancellationToken.None);
            if (string.IsNullOrEmpty(path))
            {
                _lblScrcpyStatus.Text = "自动部署 scrcpy 失败";
                _lblScrcpyStatus.ForeColor = Color.Red;
                return;
            }

            _txtScrcpyPath.Text = path;
            _scrcpyManager.SetManualPath(path);
            ValidateScrcpyPath();
        }
        catch (Exception ex)
        {
            _lblScrcpyStatus.Text = "部署失败: " + ex.Message;
            _lblScrcpyStatus.ForeColor = Color.Red;
        }
        finally
        {
            _btnDeployScrcpy.Enabled = true;
        }
    }

    private void ValidateAdbPath()
    {
        var path = _txtAdbPath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            var autoFound = _adbHelper.GetAdbPath();
            if (autoFound != null)
            {
                _lblAdbStatus.Text = "\u2714 Auto detected: " + autoFound;
                _lblAdbStatus.ForeColor = Color.Green;
            }
            else
            {
                _lblAdbStatus.Text = "\u2716 ADB not found - click Auto Detect or Browse";
                _lblAdbStatus.ForeColor = Color.Red;
            }
            return;
        }

        if (_adbHelper.ValidateAdb(path))
        {
            _lblAdbStatus.Text = "\u2714 ADB valid";
            _lblAdbStatus.ForeColor = Color.Green;
        }
        else if (!File.Exists(path))
        {
            _lblAdbStatus.Text = "\u2716 File does not exist";
            _lblAdbStatus.ForeColor = Color.Red;
        }
        else
        {
            _lblAdbStatus.Text = "\u2716 File exists but not a valid ADB";
            _lblAdbStatus.ForeColor = Color.Red;
        }
    }

    private void ValidateScrcpyPath()
    {
        var path = _txtScrcpyPath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            var autoFound = _scrcpyManager.GetScrcpyPath();
            if (autoFound != null)
            {
                _lblScrcpyStatus.Text = "已就绪: " + autoFound;
                _lblScrcpyStatus.ForeColor = Color.Green;
            }
            else
            {
                _lblScrcpyStatus.Text = "首次启动会自动部署 scrcpy，也可点 Deploy / Repair 立即修复";
                _lblScrcpyStatus.ForeColor = Color.DarkOrange;
            }
            return;
        }

        if (_scrcpyManager.ValidateScrcpy(path))
        {
            _lblScrcpyStatus.Text = "scrcpy 可用";
            _lblScrcpyStatus.ForeColor = Color.Green;
        }
        else if (!File.Exists(path))
        {
            _lblScrcpyStatus.Text = "文件不存在";
            _lblScrcpyStatus.ForeColor = Color.Red;
        }
        else
        {
            _lblScrcpyStatus.Text = "文件存在，但不是可用的 scrcpy";
            _lblScrcpyStatus.ForeColor = Color.Red;
        }
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
        _txtAdbPath.Text = _settings.AdbPath;
        _txtScrcpyPath.Text = _settings.ScrcpyPath;
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
        _settings.AdbPath = _txtAdbPath.Text.Trim();
        _settings.ScrcpyPath = _txtScrcpyPath.Text.Trim();
        _settings.AutoStartScrcpyForSelectedDevice = _chkAutoStartScrcpy.Checked;
        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
