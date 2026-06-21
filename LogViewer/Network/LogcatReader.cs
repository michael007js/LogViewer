using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using LogViewer.Models;

namespace LogViewer.Network;

public partial class LogcatReader
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public string? DeviceSerial { get; private set; }
    public bool IsRunning => _process != null && !_process.HasExited;

    public event EventHandler<SystemLogEntry>? SystemLogReceived;
    public event EventHandler<(string serial, bool success)>? ProcessExited;

    public void Start(string adbPath, string deviceSerial, string filter = "")
    {
        if (_process != null) return;

        DeviceSerial = deviceSerial;
        _cts = new CancellationTokenSource();

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

    private void Cleanup()
    {
        try { _process?.Dispose(); } catch { }
        _process = null;
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        _readTask = null;
    }

    [GeneratedRegex(@"^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d+)\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<level>[VDIWEF])\s+(?<tag>[^\s:]+)\s*:\s*(?<msg>.*)$")]
    private static partial Regex LogcatRegex();
}
