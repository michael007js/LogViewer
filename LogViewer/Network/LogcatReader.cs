using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using LogViewer.Models;

namespace LogViewer.Network;

/// <summary>
/// ADB Logcat 日志读取器，通过启动 adb logcat 进程获取 Android 系统日志。
/// 支持 threadtime 格式解析，实时触发 SystemLogReceived 事件。
/// </summary>
public partial class LogcatReader
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    /// <summary>目标设备的 ADB 序列号。</summary>
    public string? DeviceSerial { get; private set; }
    /// <summary>logcat 进程是否正在运行。</summary>
    public bool IsRunning => _process != null && !_process.HasExited;

    /// <summary>系统日志接收事件，每解析一行有效日志时触发。</summary>
    public event EventHandler<SystemLogEntry>? SystemLogReceived;
    /// <summary>进程退出事件，当 adb logcat 进程退出时触发。</summary>
    public event EventHandler<(string serial, bool success)>? ProcessExited;

    /// <summary>
    /// 启动 adb logcat 进程，开始读取指定设备的系统日志。
    /// </summary>
    /// <param name="adbPath">adb 可执行文件路径。</param>
    /// <param name="deviceSerial">目标设备的 ADB 序列号。</param>
    /// <param name="filter">logcat 过滤器表达式（如 "ActivityManager:I *:S"）。</param>
    public void Start(string adbPath, string deviceSerial, string filter = "")
    {
        if (_process != null) return;

        DeviceSerial = deviceSerial;
        _cts = new CancellationTokenSource();

        // 构建 adb logcat 命令参数
        var args = $"-s {deviceSerial} logcat -v threadtime";
        if (!string.IsNullOrWhiteSpace(filter)) args += $" {filter}";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        _process.Exited += (s, e) =>
        {
            ProcessExited?.Invoke(this, (deviceSerial, false));
            Cleanup();
        };

        _process.Start();
        _readTask = ReadLoopAsync(_cts.Token);
    }

    /// <summary>
    /// 停止 adb logcat 进程，释放相关资源。
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(500);
            }
        }
        catch { }
        Cleanup();
    }

    /// <summary>
    /// 异步读取循环，持续从 adb logcat 进程的标准输出读取日志行。
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_process == null) return;

        var regex = LogcatRegex();

        try
        {
            using var reader = _process.StandardOutput;
            while (!ct.IsCancellationRequested && _process != null && !_process.HasExited)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                var entry = ParseLine(line, regex);
                if (entry != null)
                {
                    entry.SourceDeviceSerial = DeviceSerial;
                    SystemLogReceived?.Invoke(this, entry);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    /// <summary>
    /// 解析单行 logcat 日志，提取时间、PID、TID、级别、标签和消息。
    /// threadtime 格式：MM-DD HH:mm:ss.fff PID TID LEVEL TAG: MESSAGE
    /// </summary>
    /// <param name="line">原始日志行。</param>
    /// <param name="regex">预编译的正则表达式。</param>
    /// <returns>解析后的日志条目，如果格式不匹配则返回 null。</returns>
    private SystemLogEntry? ParseLine(string line, Regex regex)
    {
        var match = regex.Match(line);
        if (!match.Success) return null;

        var entry = new SystemLogEntry
        {
            Level = match.Groups["level"].Value,
            Tag = match.Groups["tag"].Value.Trim(),
            Message = match.Groups["msg"].Value,
            ProcessId = int.TryParse(match.Groups["pid"].Value, out var pid) ? pid : 0,
            ThreadId = int.TryParse(match.Groups["tid"].Value, out var tid) ? tid : 0
        };

        // 解析时间戳（threadtime 格式不含年份，使用当前年份推断）
        var dateStr = match.Groups["date"].Value;
        var timeStr = match.Groups["time"].Value;
        var now = DateTime.Now;
        var parsedText = $"{now.Year}-{dateStr} {timeStr}";
        if (DateTime.TryParseExact(
                parsedText,
                "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
        {
            // 跨年推断：如果解析的月份大于当前月份，则认为是上一年
            var year = now.Year;
            var parsedMonth = dt.Month;
            if (parsedMonth > now.Month) year--;
            entry.Timestamp = new DateTime(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
        }
        else
        {
            entry.Timestamp = now;
        }

        return entry;
    }

    /// <summary>
    /// 清理进程和 CancellationTokenSource 资源。
    /// </summary>
    private void Cleanup()
    {
        try { _process?.Dispose(); } catch { }
        _process = null;
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _readTask = null;
    }

    /// <summary>
    /// 预编译的 logcat threadtime 格式正则表达式。
    /// 格式：MM-DD HH:mm:ss.fff PID TID LEVEL TAG: MESSAGE
    /// </summary>
    [GeneratedRegex(@"^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d+)\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<level>[VDIWEF])\s+(?<tag>[^\s:]+)\s*:\s*(?<msg>.*)$")]
    private static partial Regex LogcatRegex();
}
