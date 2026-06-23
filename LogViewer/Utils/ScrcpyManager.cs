using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using LogViewer.Static;

namespace LogViewer.Utils;

internal enum ScrcpySessionMode
{
    Embedded,
    External
}

internal sealed class ScrcpyStartOptions
{
    public required string ScrcpyPath { get; init; }
    public required string DeviceSerial { get; init; }
    public required string WindowTitle { get; init; }
    public required ScrcpySessionMode Mode { get; init; }
    public IntPtr HostHandle { get; init; }
    public int AngleDegrees { get; init; }
    public double ContentAspectRatio { get; init; }
    public int WindowX { get; init; }
    public int WindowY { get; init; }
    public int WindowWidth { get; init; }
    public int WindowHeight { get; init; }
}

internal sealed class ScrcpySession : IDisposable
{
    private bool _disposed;

    internal ScrcpySession(Process process, IntPtr windowHandle, ScrcpyStartOptions options)
    {
        Process = process;
        WindowHandle = windowHandle;
        DeviceSerial = options.DeviceSerial;
        Mode = options.Mode;
        HostHandle = options.HostHandle;
        AngleDegrees = options.AngleDegrees;

        Process.EnableRaisingEvents = true;
        Process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);

        if (Mode == ScrcpySessionMode.Embedded)
        {
            EmbeddedWindowHost.Attach(WindowHandle, HostHandle);
        }
    }

    public Process Process { get; }
    public IntPtr WindowHandle { get; }
    public string DeviceSerial { get; }
    public ScrcpySessionMode Mode { get; }
    public IntPtr HostHandle { get; }
    public int AngleDegrees { get; }
    public bool IsRunning => !Process.HasExited;

    public event EventHandler? Exited;
    public string LastErrorText { get; internal set; } = string.Empty;

    public void ResizeEmbeddedBounds(Rectangle bounds)
    {
        if (Mode != ScrcpySessionMode.Embedded || WindowHandle == IntPtr.Zero || HostHandle == IntPtr.Zero)
        {
            return;
        }

        EmbeddedWindowHost.ResizeToBounds(WindowHandle, bounds);
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!Process.HasExited)
            {
                if (WindowHandle != IntPtr.Zero)
                {
                    EmbeddedWindowHost.RequestClose(WindowHandle);
                }
                else
                {
                    try { Process.CloseMainWindow(); } catch { }
                }

                if (!Process.WaitForExit(1500))
                {
                    try { Process.Kill(entireProcessTree: true); } catch { }
                    try { Process.WaitForExit(1500); } catch { }
                }
            }
        }
        catch
        {
        }
        finally
        {
            try { Process.Dispose(); } catch { }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

internal sealed class ScrcpyManager
{
    private string? _scrcpyPath;
    private static string BundledScrcpyPath => Path.Combine(AppContext.BaseDirectory, "scrcpy.exe");

    public string? GetScrcpyPath()
    {
        if (!string.IsNullOrEmpty(_scrcpyPath) && File.Exists(_scrcpyPath))
        {
            return _scrcpyPath;
        }

        _scrcpyPath = null;

        if (File.Exists(BundledScrcpyPath))
        {
            _scrcpyPath = BundledScrcpyPath;
        }

        return _scrcpyPath;
    }

    public Task<string?> EnsureScrcpyAvailableAsync(bool forceDeploy, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = GetScrcpyPath();
        if (!string.IsNullOrEmpty(existing))
        {
            progress?.Report(Language.ScrcpyReady(existing));
            return Task.FromResult<string?>(existing);
        }

        progress?.Report(Language.ScrcpyNotFound(BundledScrcpyPath));
        return Task.FromResult<string?>(null);
    }

    public bool ValidateScrcpy(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(path)
            });

            if (process == null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetSearchPaths()
    {
        return new List<string> { BundledScrcpyPath };
    }

    public void TerminateBundledProcesses(string? scrcpyPath)
    {
        if (string.IsNullOrWhiteSpace(scrcpyPath))
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName("scrcpy"))
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                string? processPath = null;
                try
                {
                    processPath = process.MainModule?.FileName;
                }
                catch
                {
                }

                if (!string.IsNullOrEmpty(processPath) &&
                    !string.Equals(processPath, scrcpyPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
            catch
            {
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
    }

    public async Task<ScrcpySession> StartSessionAsync(ScrcpyStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == ScrcpySessionMode.Embedded && options.HostHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Embedded scrcpy host handle is not ready.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = options.ScrcpyPath,
            WorkingDirectory = Path.GetDirectoryName(options.ScrcpyPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add(options.DeviceSerial);
        psi.ArgumentList.Add("--window-title");
        psi.ArgumentList.Add(options.WindowTitle);
        psi.ArgumentList.Add("--no-audio");
        psi.ArgumentList.Add("--no-clipboard-autosync");
        psi.ArgumentList.Add("--window-borderless");

        if (options.WindowWidth > 0 && options.WindowHeight > 0)
        {
            psi.ArgumentList.Add("--window-x");
            psi.ArgumentList.Add(options.WindowX.ToString(System.Globalization.CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-y");
            psi.ArgumentList.Add(options.WindowY.ToString(System.Globalization.CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-width");
            psi.ArgumentList.Add(options.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-height");
            psi.ArgumentList.Add(options.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (options.AngleDegrees != 0)
        {
            psi.ArgumentList.Add("--rotation");
            psi.ArgumentList.Add((options.AngleDegrees / 90).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start scrcpy.");
        var stdoutTask = ReadToEndSafeAsync(process.StandardOutput);
        var stderrTask = ReadToEndSafeAsync(process.StandardError);

        try
        {
            var windowHandle = await WaitForWindowAsync(process, stderrTask, cancellationToken).ConfigureAwait(false);
            var session = new ScrcpySession(process, windowHandle, options)
            {
                LastErrorText = await stderrTask.ConfigureAwait(false)
            };
            _ = stdoutTask;
            return session;
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static async Task<string> ReadToEndSafeAsync(StreamReader reader)
    {
        try
        {
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<IntPtr> WaitForWindowAsync(Process process, Task<string> stderrTask, CancellationToken cancellationToken)
    {
        try { process.WaitForInputIdle(5000); } catch { }

        for (var attempt = 0; attempt < 80; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Refresh();

            if (process.HasExited)
            {
                var errorText = (await stderrTask.ConfigureAwait(false)).Trim();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText)
                    ? "scrcpy exited before creating a window."
                    : $"scrcpy exited before creating a window. {errorText}");
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for scrcpy window.");
    }

}

internal static class EmbeddedWindowHost
{
    private const int GwlStyle = -16;
    private const int WsChild = 0x40000000;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsMinimize = 0x20000000;
    private const int WsMaximize = 0x01000000;
    private const int WsSysMenu = 0x00080000;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwShow = 5;
    private const uint WmClose = 0x0010;

    public static void Attach(IntPtr windowHandle, IntPtr hostHandle)
    {
        if (windowHandle == IntPtr.Zero || hostHandle == IntPtr.Zero)
            return;

        SetParent(windowHandle, hostHandle);

        if (IntPtr.Size == 8)
        {
            var style = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimize | WsMaximize | WsSysMenu);
            style |= WsChild;
            SetWindowLongPtr(windowHandle, GwlStyle, (IntPtr)style);
        }
        else
        {
            var style = GetWindowLong(windowHandle, GwlStyle);
            style &= ~(WsCaption | WsThickFrame | WsMinimize | WsMaximize | WsSysMenu);
            style |= WsChild;
            SetWindowLong(windowHandle, GwlStyle, style);
        }

        if (GetClientRect(hostHandle, out var rect))
        {
            SetWindowPos(windowHandle, IntPtr.Zero,
                0, 0,
                Math.Max(0, rect.Right - rect.Left),
                Math.Max(0, rect.Bottom - rect.Top),
                SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        }

        ShowWindow(windowHandle, SwShow);
    }

    public static void ResizeToBounds(IntPtr windowHandle, Rectangle bounds)
    {
        if (windowHandle == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        MoveWindow(windowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
    }

    public static void RequestClose(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
            PostMessage(windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool TryGetClientSize(IntPtr hostHandle, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (hostHandle == IntPtr.Zero || !GetClientRect(hostHandle, out var rect))
            return false;

        width = Math.Max(0, rect.Right - rect.Left);
        height = Math.Max(0, rect.Bottom - rect.Top);
        return width > 0 && height > 0;
    }

    public static bool TryGetWindowSize(IntPtr windowHandle, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (windowHandle == IntPtr.Zero || !GetWindowRect(windowHandle, out var rect))
            return false;

        width = Math.Max(0, rect.Right - rect.Left);
        height = Math.Max(0, rect.Bottom - rect.Top);
        return width > 0 && height > 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
