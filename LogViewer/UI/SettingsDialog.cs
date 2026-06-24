using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class SettingsDialog : Form
{
    private readonly AppSettings _settings;

    public SettingsDialog()
        : this(new AppSettings(), null)
    {
    }

    public SettingsDialog(AppSettings settings, AdbHelper? adbHelper)
    {
        _settings = settings ?? new AppSettings();
        InitializeComponent();
        ApplyLanguage();

        if (IsDesignTimeMode())
        {
            LoadDesignValues();
            return;
        }

        LoadValues();
    }

    private void ApplyLanguage()
    {
        Text = Language.SettingsTitle;
        _lblPort.Text = "服务端口：";
        _lblMaxPerDevice.Text = "单设备最大日志：";
        _lblMaxAll.Text = "全部设备最大日志：";
        _lblMaxSystemLog.Text = "系统日志热缓存：";
        _lblAndroidQueue.Text = "Android 发送队列：";
        _lblMaxBodySize.Text = "Body 截断(KB)*：";
        _chkAutoAdb.Text = Language.AutoAdbReverse;
        _chkAutoLogcat.Text = Language.AutoStartLogcat;
        _chkAutoStartScrcpy.Text = Language.AutoStartScrcpy;
        _chkAutoFormatJson.Text = Language.AutoFormatJson;
        _lblFontSize.Text = "字体大小(pt)：";
        _lblAdbScanInterval.Text = "ADB 扫描间隔(ms)：";
        _lblLogcatFilter.Text = Language.LogcatFilter;
        _lblSettingsNote.Text = Language.SettingsNote;
        _lblLogcatFilterNote.Text = Language.LogcatFilterNote;
        _btnOk.Text = Language.Confirm;
        _btnCancel.Text = Language.Cancel;
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
        _chkAutoStartScrcpy.Checked = _settings.AutoStartScrcpyForSelectedDevice;
        _chkAutoFormatJson.Checked = _settings.AutoFormatJson;
        _nudFontSize.Value = _settings.FontSize;
        _nudAdbScanInterval.Value = _settings.AdbScanIntervalMs;
        _txtLogcatFilter.Text = _settings.LogcatFilter;
    }

    private void LoadDesignValues()
    {
        LoadValues();
        _txtLogcatFilter.Text = "*:I";
    }

    private static bool IsDesignTimeMode()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return true;
        }

        var processName = Process.GetCurrentProcess().ProcessName;
        if (processName.Contains("devenv", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("rider", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("resharper", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AppDomain.CurrentDomain.FriendlyName.Contains(
            "JetBrains.ReSharper.Features.WinForms.Designer.External",
            StringComparison.OrdinalIgnoreCase);
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
        _settings.AutoStartScrcpyForSelectedDevice = _chkAutoStartScrcpy.Checked;
        _settings.AutoFormatJson = _chkAutoFormatJson.Checked;
        _settings.FontSize = (int)_nudFontSize.Value;
        _settings.AdbScanIntervalMs = (int)_nudAdbScanInterval.Value;
        _settings.LogcatFilter = _txtLogcatFilter.Text;
        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
