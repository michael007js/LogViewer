using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

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
            SyncEmbeddedBounds();
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

    public void SyncEmbeddedBounds()
    {
        if (Mode != ScrcpySessionMode.Embedded || WindowHandle == IntPtr.Zero || HostHandle == IntPtr.Zero)
        {
            return;
        }

        EmbeddedWindowHost.ResizeToHost(WindowHandle, HostHandle);
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
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly SemaphoreSlim _deployLock = new(1, 1);
    private string? _scrcpyPath;
    private string? _manualScrcpyPath;
    private bool _autoSearchDone;

    public void SetManualPath(string? path)
    {
        _manualScrcpyPath = path?.Trim();
        _scrcpyPath = null;
        _autoSearchDone = false;
    }

    public string? GetScrcpyPath()
    {
        if (!string.IsNullOrEmpty(_scrcpyPath) && File.Exists(_scrcpyPath))
        {
            return _scrcpyPath;
        }

        _scrcpyPath = null;

        if (!string.IsNullOrWhiteSpace(_manualScrcpyPath) && ValidateScrcpy(_manualScrcpyPath))
        {
            _scrcpyPath = _manualScrcpyPath;
            return _scrcpyPath;
        }

        if (!_autoSearchDone)
        {
            _autoSearchDone = true;
            _scrcpyPath = GetSearchPaths().FirstOrDefault(ValidateScrcpy);
        }

        return _scrcpyPath;
    }

    public async Task<string?> EnsureScrcpyAvailableAsync(bool forceDeploy, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (!forceDeploy)
        {
            var existing = GetScrcpyPath();
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }
        }

        await _deployLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceDeploy)
            {
                var existing = GetScrcpyPath();
                if (!string.IsNullOrEmpty(existing))
                {
                    return existing;
                }
            }

            progress?.Report("正在检查 scrcpy 官方版本...");
            var release = await GetLatestWindowsReleaseAsync(cancellationToken).ConfigureAwait(false);
            var installDir = Path.Combine(GetManagedInstallRoot(), Path.GetFileNameWithoutExtension(release.AssetName));
            var existingPath = FindScrcpyExecutable(installDir);
            if (!forceDeploy && !string.IsNullOrEmpty(existingPath) && ValidateScrcpy(existingPath))
            {
                _scrcpyPath = existingPath;
                _autoSearchDone = true;
                progress?.Report($"scrcpy 已就绪：{release.TagName}");
                return _scrcpyPath;
            }

            Directory.CreateDirectory(GetManagedInstallRoot());
            var tempZipPath = Path.Combine(Path.GetTempPath(), $"logviewer-scrcpy-{Guid.NewGuid():N}.zip");

            try
            {
                progress?.Report($"正在下载 scrcpy {release.TagName}...");
                await DownloadFileAsync(release.DownloadUrl, tempZipPath, cancellationToken).ConfigureAwait(false);

                progress?.Report("正在解压 scrcpy...");
                if (Directory.Exists(installDir))
                {
                    Directory.Delete(installDir, recursive: true);
                }

                ZipFile.ExtractToDirectory(tempZipPath, installDir, overwriteFiles: true);
            }
            finally
            {
                try { File.Delete(tempZipPath); } catch { }
            }

            var exePath = FindScrcpyExecutable(installDir);
            if (string.IsNullOrEmpty(exePath) || !ValidateScrcpy(exePath))
            {
                throw new InvalidOperationException("已下载 scrcpy，但未找到可用的 scrcpy.exe。");
            }

            _scrcpyPath = exePath;
            _autoSearchDone = true;
            progress?.Report($"scrcpy 已就绪：{release.TagName}");
            return _scrcpyPath;
        }
        finally
        {
            _deployLock.Release();
        }
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
        var paths = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var dir in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "scrcpy.exe");
            if (File.Exists(candidate))
            {
                paths.Add(candidate);
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        paths.Add(Path.Combine(programFiles, "scrcpy", "scrcpy.exe"));
        paths.Add(Path.Combine(programFiles, "Genymobile", "scrcpy", "scrcpy.exe"));
        paths.Add(Path.Combine(localAppData, "Programs", "scrcpy", "scrcpy.exe"));
        paths.AddRange(GetManagedInstallCandidates());

        return paths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        if (options.AngleDegrees != 0)
        {
            psi.ArgumentList.Add("--angle");
            psi.ArgumentList.Add(options.AngleDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start scrcpy.");
        _ = Task.Run(() => DrainAsync(process.StandardOutput), CancellationToken.None);
        _ = Task.Run(() => DrainAsync(process.StandardError), CancellationToken.None);

        var windowHandle = await WaitForWindowAsync(process, cancellationToken).ConfigureAwait(false);
        return new ScrcpySession(process, windowHandle, options);
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try
        {
            await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task<IntPtr> WaitForWindowAsync(Process process, CancellationToken cancellationToken)
    {
        try { process.WaitForInputIdle(5000); } catch { }

        for (var attempt = 0; attempt < 80; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Refresh();

            if (process.HasExited)
            {
                throw new InvalidOperationException("scrcpy exited before creating a window.");
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for scrcpy window.");
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LogViewer", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static async Task DownloadFileAsync(string downloadUrl, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ScrcpyReleaseAsset> GetLatestWindowsReleaseAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(LatestReleaseApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        var tagName = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new InvalidOperationException("未从 GitHub release 中读取到版本号。");
        }

        var architectureToken = Environment.Is64BitOperatingSystem ? "win64" : "win32";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var assetName = asset.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(assetName))
            {
                continue;
            }

            if (!assetName.StartsWith($"scrcpy-{architectureToken}-", StringComparison.OrdinalIgnoreCase) ||
                !assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            return new ScrcpyReleaseAsset(tagName, assetName, downloadUrl);
        }

        throw new InvalidOperationException($"官方最新版本未找到 {architectureToken} Windows 发布包。");
    }

    private static string? FindScrcpyExecutable(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(directory, "scrcpy.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static IEnumerable<string> GetManagedInstallCandidates()
    {
        var root = GetManagedInstallRoot();
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(root, "scrcpy.exe", SearchOption.AllDirectories))
        {
            yield return path;
        }
    }

    private static string GetManagedInstallRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "LogViewer", "Tools", "scrcpy");
    }

    private sealed record ScrcpyReleaseAsset(string TagName, string AssetName, string DownloadUrl);
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
    private const uint SwShow = 5;
    private const uint WmClose = 0x0010;

    public static void Attach(IntPtr windowHandle, IntPtr hostHandle)
    {
        if (windowHandle == IntPtr.Zero || hostHandle == IntPtr.Zero)
        {
            return;
        }

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

        ShowWindow(windowHandle, SwShow);
    }

    public static void ResizeToHost(IntPtr windowHandle, IntPtr hostHandle)
    {
        if (windowHandle == IntPtr.Zero || hostHandle == IntPtr.Zero)
        {
            return;
        }

        if (!GetClientRect(hostHandle, out var rect))
        {
            return;
        }

        MoveWindow(windowHandle, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, true);
    }

    public static void RequestClose(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
        {
            PostMessage(windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
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

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

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
