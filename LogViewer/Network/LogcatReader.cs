using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LogViewer.Models;

namespace LogViewer.Network;

/// <summary>
/// ADB Logcat 日志读取器，通过启动 adb logcat 进程获取 Android 系统日志。
/// </summary>
public partial class LogcatReader
{
    private readonly object _stateGate = new();
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private int _runVersion;
    private int _completedVersion;
    private bool _startInProgress;
    private bool _stopRequested;

    public string? DeviceSerial { get; private set; }

    public bool IsRunning
    {
        get
        {
            lock (_stateGate)
            {
                if (_process == null || _startInProgress || _stopRequested) return false;
                try { return !_process.HasExited; }
                catch (ObjectDisposedException) { return false; }
                catch (InvalidOperationException) { return false; }
            }
        }
    }

    public event EventHandler<SystemLogEntry>? SystemLogReceived;
    public event EventHandler<(string serial, bool success)>? ProcessExited;

    public void Start(string adbPath, string deviceSerial, string filter = "")
    {
        _ = StartAsync(adbPath, deviceSerial, filter).ContinueWith(task =>
        {
            Debug.WriteLine(task.Exception?.GetBaseException());
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    public async Task StartAsync(string adbPath, string deviceSerial, string filter = "",
        CancellationToken cancellationToken = default)
    {
        Process process;
        CancellationTokenSource runCts;
        int runVersion;

        lock (_stateGate)
        {
            if (_process != null || _startInProgress) return;

            _startInProgress = true;
            _stopRequested = false;
            runVersion = ++_runVersion;
            DeviceSerial = deviceSerial;
            runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = runCts;
            process = CreateProcess(adbPath, deviceSerial, filter);
            _process = process;
        }

        process.Exited += (_, _) => CompleteRun(process, runVersion, IsStopRequested(runVersion));

        try
        {
            await Task.Run(() =>
            {
                runCts.Token.ThrowIfCancellationRequested();
                if (!process.Start()) throw new InvalidOperationException("Failed to start adb logcat.");
            }, runCts.Token).ConfigureAwait(false);

            var shouldStop = false;
            lock (_stateGate)
            {
                if (runVersion == _runVersion)
                {
                    _startInProgress = false;
                    shouldStop = _stopRequested || _completedVersion == runVersion;
                }
                else
                {
                    shouldStop = true;
                }
            }

            if (shouldStop)
            {
                StopProcess(process);
                CompleteRun(process, runVersion, expectedStop: true);
                return;
            }

            var readTask = ReadLoopAsync(process, deviceSerial, runVersion, runCts.Token);
            lock (_stateGate)
            {
                if (runVersion == _runVersion && _completedVersion != runVersion)
                    _readTask = readTask;
            }

            _ = readTask;
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            StopProcess(process);
            CompleteRun(process, runVersion, expectedStop: true);
        }
        catch
        {
            StopProcess(process);
            CompleteRun(process, runVersion, expectedStop: false);
            throw;
        }
    }

    public void Stop()
    {
        Process? process;
        CancellationTokenSource? cts;
        int runVersion;
        lock (_stateGate)
        {
            _stopRequested = true;
            runVersion = _runVersion;
            process = _process;
            cts = _cts;
        }

        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        if (process == null) return;
        StopProcess(process);
        CompleteRun(process, runVersion, expectedStop: true);
    }

    private static Process CreateProcess(string adbPath, string deviceSerial, string filter)
    {
        var args = $"-s {deviceSerial} logcat -v threadtime";
        if (!string.IsNullOrWhiteSpace(filter)) args += $" {filter}";

        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
    }

    private async Task ReadLoopAsync(Process process, string deviceSerial, int runVersion,
        CancellationToken cancellationToken)
    {
        var regex = LogcatRegex();
        try
        {
            using var reader = process.StandardOutput;
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;

                var entry = ParseLine(line, regex);
                if (entry == null) continue;
                entry.SourceDeviceSerial = deviceSerial;
                SystemLogReceived?.Invoke(this, entry);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            if (!HasExited(process)) StopProcess(process);
            CompleteRun(process, runVersion, cancellationToken.IsCancellationRequested || IsStopRequested(runVersion));
        }
    }

    private bool IsStopRequested(int runVersion)
    {
        lock (_stateGate)
        {
            return runVersion != _runVersion || _stopRequested;
        }
    }

    private void CompleteRun(Process process, int runVersion, bool expectedStop)
    {
        CancellationTokenSource? cts = null;
        var shouldRaise = false;
        var success = false;

        lock (_stateGate)
        {
            if (runVersion == _runVersion && _completedVersion != runVersion)
            {
                _completedVersion = runVersion;
                shouldRaise = true;
                _startInProgress = false;
                _readTask = null;
                if (ReferenceEquals(_process, process)) _process = null;
                cts = _cts;
                _cts = null;
                success = !expectedStop && TryGetExitCode(process, out var exitCode) && exitCode == 0;
            }
        }

        try { process.Dispose(); } catch { }
        try { cts?.Dispose(); } catch { }

        if (!shouldRaise) return;
        try { ProcessExited?.Invoke(this, (DeviceSerial ?? string.Empty, success)); }
        catch { }
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static bool TryGetExitCode(Process process, out int exitCode)
    {
        try
        {
            if (!process.HasExited)
            {
                exitCode = -1;
                return false;
            }

            exitCode = process.ExitCode;
            return true;
        }
        catch
        {
            exitCode = -1;
            return false;
        }
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(500);
            }
        }
        catch
        {
        }
    }

    private static SystemLogEntry? ParseLine(string line, Regex regex)
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
        if (DateTime.TryParseExact(parsedText, "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var year = dt.Month > now.Month ? now.Year - 1 : now.Year;
            entry.Timestamp = new DateTime(year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
        }
        else
        {
            entry.Timestamp = now;
        }

        return entry;
    }

    [GeneratedRegex(
        @"^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d+)\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<level>[VDIWEF])\s+(?<tag>[^\s:]+)\s*:\s*(?<msg>.*)$")]
    private static partial Regex LogcatRegex();
}
