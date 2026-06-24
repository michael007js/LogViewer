using System.Text.Json;
using LogViewer.Utils;

namespace LogViewer.UI;

/// <summary>
/// MainForm 的 scrcpy 投屏部分，包含投屏的生命周期管理、状态同步和截图逻辑。
/// </summary>
public partial class MainForm
{
    /// <summary>当前 scrcpy 会话。</summary>
    private ScrcpySession? _scrcpySession;
    /// <summary>外部启动的 scrcpy 会话列表。</summary>
    private readonly List<ScrcpySession> _externalScrcpySessions = new();
    /// <summary>scrcpy 旋转角度索引。</summary>
    private int _scrcpyRotationIndex;
    /// <summary>scrcpy 是否正在准备中。</summary>
    private bool _scrcpyPreparing;
    /// <summary>scrcpy 部署状态信息。</summary>
    private string? _scrcpyDeployStatus;
    /// <summary>scrcpy 部署错误信息。</summary>
    private string? _scrcpyDeployError;
    /// <summary>scrcpy 是否已验证可用。</summary>
    private bool _scrcpyValidated;
    /// <summary>正在启动镜像的设备序列号。</summary>
    private string? _mirrorStartingSerial;
    /// <summary>镜像重启定时器。</summary>
    private System.Windows.Forms.Timer? _mirrorRestartTimer;
    /// <summary>是否有待处理的镜像重启。</summary>
    private bool _mirrorRestartPending;
    /// <summary>镜像是否正在重启中。</summary>
    private bool _mirrorRestartInProgress;

    private void RefreshMirrorPanelState()
    {
        if (IsDesignTimeMode())
        {
            return;
        }

        UpdateAdbStatus();

        if (string.IsNullOrEmpty(_currentDeviceId))
        {
            _devicePanel.SetMirrorStatus("\u8BF7\u9009\u62E9\u5177\u4F53\u8BBE\u5907\u4EE5\u64CD\u63A7\u624B\u673A", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        var serial = ResolveAdbSerial(_currentDeviceId);
        if (string.IsNullOrEmpty(serial))
        {
            _devicePanel.SetMirrorStatus("\u5F53\u524D\u8BBE\u5907\u672A\u5339\u914D ADB serial\uFF0C\u65E0\u6CD5\u542F\u52A8\u624B\u673A\u955C\u50CF", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (_scrcpyPreparing)
        {
            _devicePanel.SetMirrorStatus(_scrcpyDeployStatus ?? "\u6B63\u5728\u90E8\u7F72 scrcpy...", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (string.Equals(_mirrorStartingSerial, serial, StringComparison.Ordinal))
        {
            _devicePanel.SetMirrorStatus($"\u6B63\u5728\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        var scrcpyPath = _scrcpyManager.GetScrcpyPath();
        if (string.IsNullOrEmpty(scrcpyPath) || !_scrcpyValidated)
        {
            var message = !string.IsNullOrEmpty(_scrcpyDeployError)
                ? $"\u81EA\u52A8\u90E8\u7F72 scrcpy \u5931\u8D25\uFF1A{_scrcpyDeployError}"
                : string.IsNullOrEmpty(scrcpyPath)
                    ? "\u672A\u5B8C\u6210 scrcpy \u90E8\u7F72\uFF0C\u7A0D\u540E\u4F1A\u81EA\u52A8\u51C6\u5907"
                    : "scrcpy 校验中...";
            _devicePanel.SetMirrorStatus(message, hostVisible: false, isRunning: false, isReady: false);
            return;
        }

        if (_scrcpySession?.IsRunning == true && string.Equals(_scrcpySession.DeviceSerial, serial, StringComparison.Ordinal))
        {
            _devicePanel.SetMirrorStatus($"\u955C\u50CF\u5DF2\u8FDE\u63A5\uFF1A{serial}", hostVisible: true, isRunning: true, isReady: true);
            return;
        }

        _devicePanel.SetMirrorStatus($"\u5DF2\u5C31\u7EEA\uFF0C\u53EF\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
    }

    private async Task<string?> EnsureScrcpyReadyAsync(bool forceDeploy, bool reportToMirrorPanel)
    {
        try
        {
            _scrcpyPreparing = true;
            _scrcpyDeployError = null;
            _scrcpyDeployStatus = "\u6B63\u5728\u90E8\u7F72 scrcpy...";
            UpdateAdbStatus();

            if (reportToMirrorPanel && !string.IsNullOrEmpty(_currentDeviceId))
            {
                _devicePanel.SetMirrorStatus(_scrcpyDeployStatus, hostVisible: false, isRunning: false, isReady: false);
            }

            var progress = new Progress<string>(message =>
            {
                _scrcpyDeployStatus = message;
                UpdateAdbStatus();

                if (reportToMirrorPanel && !string.IsNullOrEmpty(_currentDeviceId) && _scrcpySession?.IsRunning != true)
                {
                    _devicePanel.SetMirrorStatus(message, hostVisible: false, isRunning: false, isReady: false);
                }
            });

            var scrcpyPath = await _scrcpyManager
                .EnsureScrcpyAvailableAsync(forceDeploy, progress, CancellationToken.None)
                .ConfigureAwait(true);

            if (!string.IsNullOrEmpty(scrcpyPath))
            {
                _scrcpyDeployStatus = $"\u5DF2\u5B8C\u6210 scrcpy \u90E8\u7F72\uFF1A{Path.GetFileName(scrcpyPath)}";
            }

            return scrcpyPath;
        }
        catch (Exception ex)
        {
            _scrcpyDeployError = ex.Message;
            _scrcpyDeployStatus = null;
            return null;
        }
        finally
        {
            _scrcpyPreparing = false;
            UpdateAdbStatus();

            if (reportToMirrorPanel)
            {
                RefreshMirrorPanelState();
            }
        }
    }

    private string? ResolveAdbSerial(string deviceId)
    {
        var info = _server.GetDeviceInfo(deviceId);
        return info?.AdbSerial ?? _devicePanel.GetAdbSerialForKey(deviceId);
    }

    private async void OnMirrorStartRequested(object? sender, string deviceId)
    {
        if (!string.Equals(deviceId, _currentDeviceId, StringComparison.Ordinal))
        {
            _currentDeviceId = deviceId;
        }

        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private void OnMirrorStopRequested(object? sender, string deviceId)
    {
        StopMirror(clearStatusOnly: false);
        RefreshMirrorPanelState();
    }

    private async void OnMirrorReconnectRequested(object? sender, string deviceId)
    {
        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private async void OnMirrorRotateRequested(object? sender, string deviceId)
    {
        _scrcpyRotationIndex = (_scrcpyRotationIndex + 1) % 4;
        await StartMirrorForCurrentDeviceAsync(restart: true);
    }

    private async void OnMirrorPopoutRequested(object? sender, string deviceId)
    {
        await StartMirrorSessionAsync(embedded: false, restartCurrent: false);
    }

    private void OnMirrorScreenshotRequested(object? sender, string deviceId)
    {
        CaptureDeviceScreenshot(deviceId);
    }

    private async Task StartMirrorForCurrentDeviceAsync(bool restart)
    {
        if (string.IsNullOrEmpty(_currentDeviceId))
        {
            RefreshMirrorPanelState();
            return;
        }

        await StartMirrorSessionAsync(embedded: true, restartCurrent: restart);
    }

    private async Task StartMirrorSessionAsync(bool embedded, bool restartCurrent)
    {
        var deviceId = _currentDeviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            RefreshMirrorPanelState();
            return;
        }

        var serial = ResolveAdbSerial(deviceId);
        if (string.IsNullOrEmpty(serial))
        {
            RefreshMirrorPanelState();
            return;
        }

        var scrcpyPath = await EnsureScrcpyReadyAsync(forceDeploy: false, reportToMirrorPanel: true);
        if (string.IsNullOrEmpty(scrcpyPath))
        {
            RefreshMirrorPanelState();
            return;
        }

        _scrcpyManager.TerminateBundledProcesses(scrcpyPath);

        if (restartCurrent)
        {
            StopMirror(clearStatusOnly: true);
        }

        _scrcpyStartCts?.Cancel();
        _scrcpyStartCts?.Dispose();
        _scrcpyStartCts = new CancellationTokenSource();
        var token = _scrcpyStartCts.Token;

        if (embedded)
        {
            _mirrorStartingSerial = serial;
            _devicePanel.SetMirrorAspectRatio(GetDeviceContentAspectRatio(serial));
            _devicePanel.SetMirrorStatus($"\u6B63\u5728\u542F\u52A8\u955C\u50CF\uFF1A{serial}", hostVisible: false, isRunning: false, isReady: false);
        }

        try
        {
             var contentAspectRatio = GetDeviceContentAspectRatio(serial);
             var hostHandle = embedded ? _devicePanel.EnsureMirrorHostHandle() : IntPtr.Zero;
             var windowBounds = embedded ? _devicePanel.GetMirrorDisplayBounds() : Rectangle.Empty;
             var session = await _scrcpyManager.StartSessionAsync(new ScrcpyStartOptions
             {
                 ScrcpyPath = scrcpyPath,
                 DeviceSerial = serial,
                 WindowTitle = $"LogViewer.scrcpy.{serial}.{Guid.NewGuid():N}",
                 Mode = embedded ? ScrcpySessionMode.Embedded : ScrcpySessionMode.External,
                 HostHandle = hostHandle,
                 AngleDegrees = _scrcpyRotationIndex * 90,
                 ContentAspectRatio = contentAspectRatio,
                 WindowX = windowBounds.X,
                 WindowY = windowBounds.Y,
                 WindowWidth = windowBounds.Width,
                 WindowHeight = windowBounds.Height
             }, token).ConfigureAwait(false);

            if (token.IsCancellationRequested || IsDisposed)
            {
                session.Dispose();
                return;
            }

            if (embedded)
            {
                BeginInvoke(new Action(() =>
                {
                    _mirrorStartingSerial = null;
                    _scrcpySession?.Dispose();
                    _scrcpySession = session;
                    _scrcpySession.Exited += OnScrcpySessionExited;
                    _devicePanel.SetMirrorAspectRatio(contentAspectRatio);
                    _devicePanel.SetMirrorStatus($"\u955C\u50CF\u5DF2\u8FDE\u63A5\uFF1A{serial}", hostVisible: true, isRunning: true, isReady: true);
                    ApplyEmbeddedMirrorLayout();
                }));
            }
            else
            {
                BeginInvoke(new Action(() =>
                {
                    _externalScrcpySessions.Add(session);
                    session.Exited += (_, _) =>
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke(new Action(() => _externalScrcpySessions.Remove(session)));
                        }
                    };
                }));
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    _mirrorStartingSerial = null;
                    _devicePanel.SetMirrorStatus($"\u955C\u50CF\u542F\u52A8\u5931\u8D25\uFF1A{ex.Message}", hostVisible: false, isRunning: false, isReady: false);
                }));
            }
        }
    }

    private void OnScrcpySessionExited(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            _mirrorStartingSerial = null;
            _scrcpySession?.Dispose();
            _scrcpySession = null;
            RefreshMirrorPanelState();
        }));
    }

    private void StopMirror(bool clearStatusOnly)
    {
        _scrcpyStartCts?.Cancel();
        _mirrorStartingSerial = null;
        _mirrorRestartPending = false;
        _scrcpySession?.Dispose();
        _scrcpySession = null;
        if (clearStatusOnly)
        {
            _devicePanel.ClearMirrorHost();
        }
    }

    private void ScheduleEmbeddedMirrorRestart()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (_scrcpySession?.IsRunning != true ||
            string.IsNullOrEmpty(_currentDeviceId) ||
            _mirrorStartingSerial != null ||
            _mirrorRestartInProgress)
        {
            return;
        }

        _mirrorRestartPending = true;
        _mirrorRestartTimer ??= CreateMirrorRestartTimer();
        _mirrorRestartTimer.Stop();
        _mirrorRestartTimer.Start();
    }

    private void ApplyEmbeddedMirrorLayout()
    {
        if (_scrcpySession?.IsRunning != true)
            return;

        _devicePanel.SyncMirrorBounds();
        var bounds = _devicePanel.GetMirrorDisplayBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        _scrcpySession.ResizeEmbeddedBounds(bounds);
    }

    private System.Windows.Forms.Timer CreateMirrorRestartTimer()
    {
        var timer = new System.Windows.Forms.Timer
        {
            Interval = 260
        };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            if (!_mirrorRestartPending ||
                _mirrorRestartInProgress ||
                _mirrorStartingSerial != null ||
                IsDisposed ||
                !IsHandleCreated ||
                string.IsNullOrEmpty(_currentDeviceId))
            {
                return;
            }

            _mirrorRestartPending = false;
            _mirrorRestartInProgress = true;
            try
            {
                await StartMirrorForCurrentDeviceAsync(restart: true);
            }
            finally
            {
                _mirrorRestartInProgress = false;
            }
        };
        return timer;
    }

    private void CaptureDeviceScreenshot(string deviceId)
    {
        var adbPath = _adbHelper.GetAdbPath();
        var serial = ResolveAdbSerial(deviceId);
        if (adbPath == null || string.IsNullOrEmpty(serial))
        {
            RefreshMirrorPanelState();
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"screenshot_{serial}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            var bytes = RunAdbBinary(adbPath, serial, "exec-out screencap -p");
            File.WriteAllBytes(dialog.FileName, bytes);
            _devicePanel.SetMirrorStatus($"\u622A\u56FE\u5DF2\u4FDD\u5B58\uFF1A{Path.GetFileName(dialog.FileName)}", hostVisible: _scrcpySession?.IsRunning == true, isRunning: _scrcpySession?.IsRunning == true, isReady: _scrcpySession != null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"\u622A\u56FE\u5931\u8D25\uFF1A{ex.Message}", "Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshMirrorPanelState();
        }
    }

    private static byte[] RunAdbBinary(string adbPath, string serial, string arguments)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = $"-s {serial} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start adb.");

        using var memory = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(memory);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "adb screenshot failed." : error.Trim());
        }

        return memory.ToArray();
    }

    private double GetDeviceContentAspectRatio(string serial)
    {
        var adbPath = _adbHelper.GetAdbPath();
        if (string.IsNullOrEmpty(adbPath))
        {
            return 9d / 16d;
        }

        try
        {
            var output = System.Text.Encoding.UTF8.GetString(RunAdbBinary(adbPath, serial, "shell wm size"));
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)\s*x\s*(\d+)");
            if (!match.Success)
            {
                return 9d / 16d;
            }

            var width = int.Parse(match.Groups[1].Value);
            var height = int.Parse(match.Groups[2].Value);
            if (width <= 0 || height <= 0)
            {
                return 9d / 16d;
            }

            var normalizedWidth = width;
            var normalizedHeight = height;
            if ((_scrcpyRotationIndex % 2) == 1)
            {
                normalizedWidth = height;
                normalizedHeight = width;
            }

            return (double)normalizedWidth / normalizedHeight;
        }
        catch
        {
            return 9d / 16d;
        }
    }
}
