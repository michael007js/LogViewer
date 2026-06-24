using System.ComponentModel;
using System.Diagnostics;
using LogViewer.Models;
using LogViewer.Static;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// 设置对话框，配置服务端口、日志容量、ADB 自动反向、Logcat 自动启动、
/// scrcpy 自动部署、JSON 自动格式化等选项。
/// 具有设计器模式保护，避免在 IDE 设计器中触发运行时逻辑。
/// </summary>
public partial class SettingsDialog : Form
{
    /// <summary>应用程序设置实例，用于加载和保存配置值。</summary>
    private readonly AppSettings _settings;

    /// <summary>
    /// 设计器模式构造函数，使用默认设置和 null ADB 辅助类。
    /// </summary>
    public SettingsDialog()
        : this(new AppSettings(), null)
    {
    }

    /// <summary>
    /// 主构造函数，初始化设置对话框并加载当前配置值。
    /// </summary>
    /// <param name="settings">应用程序设置实例。</param>
    /// <param name="adbHelper">ADB 辅助类实例，当前未使用但预留扩展。</param>
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

    /// <summary>
    /// 应用界面语言，将所有控件文本设置为中文。
    /// </summary>
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

    /// <summary>
    /// 从设置实例加载当前配置值到各控件。
    /// </summary>
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

    /// <summary>
    /// 加载设计器模式下的示例值，Logcat 筛选填入默认值 "*:I"。
    /// </summary>
    private void LoadDesignValues()
    {
        LoadValues();
        _txtLogcatFilter.Text = "*:I";
    }

    /// <summary>
    /// 检测当前是否处于 IDE 设计器模式（Visual Studio / Rider / ReSharper），
    /// 防止设计器中触发运行时逻辑（如 ADB 调用）导致崩溃。
    /// </summary>
    /// <returns>处于设计器模式返回 true。</returns>
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

    /// <summary>
    /// 确定按钮点击事件，将控件值回写到设置实例并持久化保存。
    /// </summary>
    /// <param name="sender">事件源控件。</param>
    /// <param name="e">事件参数。</param>
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