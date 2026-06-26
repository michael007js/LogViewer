using LogViewer.Properties;

namespace LogViewer.Models;

/// <summary>
/// 应用设置模型，封装所有可持久化的用户配置项。
/// 通过 Properties.Settings 实现读写和持久化，设置存储在用户配置文件中。
/// 提供 Load/Save 静态方法完成整批读写，避免逐属性操作。
/// </summary>
public class AppSettings
{
    /// <summary>TCP 服务器监听端口号，默认 9527。</summary>
    public int ServerPort { get; set; } = 9527;

    /// <summary>每台设备最大缓存日志条目数。</summary>
    public int MaxLogEntriesPerDevice { get; set; } = 5000;

    /// <summary>所有设备最大缓存日志条目总数。</summary>
    public int MaxLogEntriesAll { get; set; } = 10000;

    /// <summary>系统日志（logcat）最大缓存条目数。</summary>
    public int MaxSystemLogEntries { get; set; } = 10000;

    /// <summary>Android 端日志发送队列大小。</summary>
    public int AndroidQueueSize { get; set; } = 1000;

    /// <summary>响应体最大缓存大小（KB），超过此大小截断。</summary>
    public int MaxBodySizeKb { get; set; } = 500;

    /// <summary>启动时自动执行 adb reverse 端口映射。</summary>
    public bool AutoAdbReverse { get; set; } = true;

    /// <summary>启动时自动开启 adb logcat 日志采集。</summary>
    public bool AutoStartLogcat { get; set; } = true;

    /// <summary>日志详情中自动格式化 JSON 响应体。</summary>
    public bool AutoFormatJson { get; set; } = true;

    /// <summary>UI 字体大小。</summary>
    public int FontSize { get; set; } = 11;

    /// <summary>ADB 设备扫描间隔（毫秒）。</summary>
    public int AdbScanIntervalMs { get; set; } = 2000;

    /// <summary>Logcat 过滤器表达式。</summary>
    public string LogcatFilter { get; set; } = "";

    /// <summary>正则表达式无效时是否弹窗提示。</summary>
    public bool NotifyRegexError { get; set; } = true;

    /// <summary>选中设备时自动启动 scrcpy 投屏。</summary>
    public bool AutoStartScrcpyForSelectedDevice { get; set; }

    /// <summary>上次关闭时左侧面板宽度，用于恢复布局。</summary>
    public int LastLeftPanelWidth { get; set; } = 340;

    /// <summary>
    /// 将当前设置写入 Properties.Settings 并持久化到用户配置文件。
    /// </summary>
    public void Save()
    {
        var s = Settings.Default;
        s.ServerPort = ServerPort;
        s.MaxLogEntriesPerDevice = MaxLogEntriesPerDevice;
        s.MaxLogEntriesAll = MaxLogEntriesAll;
        s.MaxSystemLogEntries = MaxSystemLogEntries;
        s.AndroidQueueSize = AndroidQueueSize;
        s.MaxBodySizeKb = MaxBodySizeKb;
        s.AutoAdbReverse = AutoAdbReverse;
        s.AutoStartLogcat = AutoStartLogcat;
        s.AutoFormatJson = AutoFormatJson;
        s.FontSize = FontSize;
        s.AdbScanIntervalMs = AdbScanIntervalMs;
        s.LogcatFilter = LogcatFilter;
        s.NotifyRegexError = NotifyRegexError;
        s.AutoStartScrcpyForSelectedDevice = AutoStartScrcpyForSelectedDevice;
        s.LastLeftPanelWidth = LastLeftPanelWidth;
        s.Save();
    }

    /// <summary>
    /// 从 Properties.Settings 读取所有配置项并创建 AppSettings 实例。
    /// </summary>
    public static AppSettings Load()
    {
        var s = Settings.Default;
        return new AppSettings
        {
            ServerPort = s.ServerPort,
            MaxLogEntriesPerDevice = s.MaxLogEntriesPerDevice,
            MaxLogEntriesAll = s.MaxLogEntriesAll,
            MaxSystemLogEntries = s.MaxSystemLogEntries,
            AndroidQueueSize = s.AndroidQueueSize,
            MaxBodySizeKb = s.MaxBodySizeKb,
            AutoAdbReverse = s.AutoAdbReverse,
            AutoStartLogcat = s.AutoStartLogcat,
            AutoFormatJson = s.AutoFormatJson,
            FontSize = s.FontSize,
            AdbScanIntervalMs = s.AdbScanIntervalMs,
            LogcatFilter = s.LogcatFilter ?? "",
            NotifyRegexError = s.NotifyRegexError,
            AutoStartScrcpyForSelectedDevice = s.AutoStartScrcpyForSelectedDevice,
            LastLeftPanelWidth = s.LastLeftPanelWidth
        };
    }
}