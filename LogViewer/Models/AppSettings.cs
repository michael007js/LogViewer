namespace LogViewer.Models;

public class AppSettings
{
    public int ServerPort { get; set; } = 9527;
    public int MaxLogEntriesPerDevice { get; set; } = 5000;
    public int MaxLogEntriesAll { get; set; } = 10000;
    public int MaxSystemLogEntries { get; set; } = 10000;
    public int AndroidQueueSize { get; set; } = 1000;
    public int MaxBodySizeKb { get; set; } = 500;
    public bool AutoAdbReverse { get; set; } = true;
    public bool AutoStartLogcat { get; set; } = true;
    public bool AutoFormatJson { get; set; } = true;
    public int FontSize { get; set; } = 11;
    public int AdbScanIntervalMs { get; set; } = 2000;
    public string LogcatFilter { get; set; } = "";
    public string AdbPath { get; set; } = "";

    public void Save()
    {
        var s = Properties.Settings.Default;
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
        s.AdbPath = AdbPath;
        s.Save();
    }

    public static AppSettings Load()
    {
        var s = Properties.Settings.Default;
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
            AdbPath = s.AdbPath ?? ""
        };
    }
}