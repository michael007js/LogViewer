using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using LogViewer.Static;

namespace LogViewer.Utils;

/// <summary>
/// scrcpy 会话的显示模式
/// </summary>
internal enum ScrcpySessionMode
{
    /// <summary>
    /// 嵌入模式：scrcpy 窗口作为子控件嵌入到宿主窗口内
    /// </summary>
    Embedded,

    /// <summary>
    /// 外部模式：scrcpy 以独立窗口运行
    /// </summary>
    External
}

/// <summary>
/// 启动 scrcpy 会话所需的配置选项
/// </summary>
internal sealed class ScrcpyStartOptions
{
    /// <summary>
    /// scrcpy 可执行文件的完整路径
    /// </summary>
    public required string ScrcpyPath { get; init; }

    /// <summary>
    /// 目标设备的 ADB 序列号
    /// </summary>
    public required string DeviceSerial { get; init; }

    /// <summary>
    /// scrcpy 窗口标题
    /// </summary>
    public required string WindowTitle { get; init; }

    /// <summary>
    /// 会话显示模式（嵌入或外部）
    /// </summary>
    public required ScrcpySessionMode Mode { get; init; }

    /// <summary>
    /// 嵌入模式下宿主控件的窗口句柄；外部模式下为 <see cref="IntPtr.Zero"/>
    /// </summary>
    public IntPtr HostHandle { get; init; }

    /// <summary>
    /// 屏幕旋转角度（0/90/180/270），0 表示不旋转
    /// </summary>
    public int AngleDegrees { get; init; }

    /// <summary>
    /// 内容宽高比，用于计算嵌入窗口的缩放布局
    /// </summary>
    public double ContentAspectRatio { get; init; }

    /// <summary>
    /// 外部模式下窗口左上角 X 坐标
    /// </summary>
    public int WindowX { get; init; }

    /// <summary>
    /// 外部模式下窗口左上角 Y 坐标
    /// </summary>
    public int WindowY { get; init; }

    /// <summary>
    /// 外部模式下窗口宽度
    /// </summary>
    public int WindowWidth { get; init; }

    /// <summary>
    /// 外部模式下窗口高度
    /// </summary>
    public int WindowHeight { get; init; }
}

/// <summary>
/// 表示一个正在运行的 scrcpy 实例，管理其生命周期和窗口嵌入
/// </summary>
/// <remarks>实现 <see cref="IDisposable"/>，释放时会优雅关闭 scrcpy 进程</remarks>
internal sealed class ScrcpySession : IDisposable
{
    // 标识是否已调用过 Stop/Dispose，防止重复终止
    private bool _disposed;

    /// <summary>
    /// 初始化 scrcpy 会话实例
    /// </summary>
    /// <param name="process">scrcpy 进程对象</param>
    /// <param name="windowHandle">scrcpy 创建的窗口句柄</param>
    /// <param name="options">启动时使用的配置选项</param>
    internal ScrcpySession(Process process, IntPtr windowHandle, ScrcpyStartOptions options)
    {
        Process = process;
        WindowHandle = windowHandle;
        DeviceSerial = options.DeviceSerial;
        Mode = options.Mode;
        HostHandle = options.HostHandle;
        AngleDegrees = options.AngleDegrees;

        // 订阅进程退出事件，转发为 Exited 事件
        Process.EnableRaisingEvents = true;
        Process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);

        // 嵌入模式：将 scrcpy 窗口附着到宿主控件
        if (Mode == ScrcpySessionMode.Embedded)
        {
            EmbeddedWindowHost.Attach(WindowHandle, HostHandle);
        }
    }

    /// <summary>
    /// scrcpy 进程对象
    /// </summary>
    public Process Process { get; }

    /// <summary>
    /// scrcpy 创建的窗口句柄
    /// </summary>
    public IntPtr WindowHandle { get; }

    /// <summary>
    /// 目标设备的 ADB 序列号
    /// </summary>
    public string DeviceSerial { get; }

    /// <summary>
    /// 会话显示模式
    /// </summary>
    public ScrcpySessionMode Mode { get; }

    /// <summary>
    /// 嵌入模式下的宿主窗口句柄
    /// </summary>
    public IntPtr HostHandle { get; }

    /// <summary>
    /// 屏幕旋转角度
    /// </summary>
    public int AngleDegrees { get; }

    /// <summary>
    /// 进程是否仍在运行
    /// </summary>
    public bool IsRunning => !Process.HasExited;

    /// <summary>
    /// scrcpy 进程退出时触发
    /// </summary>
    public event EventHandler? Exited;

    /// <summary>
    /// 最近一次错误文本（来自 stderr 输出）
    /// </summary>
    public string LastErrorText { get; internal set; } = string.Empty;

    /// <summary>
    /// 调整嵌入窗口的大小和位置以匹配指定边界
    /// </summary>
    /// <param name="bounds">目标边界矩形</param>
    /// <remarks>仅在嵌入模式且句柄有效时执行操作</remarks>
    public void ResizeEmbeddedBounds(Rectangle bounds)
    {
        if (Mode != ScrcpySessionMode.Embedded || WindowHandle == IntPtr.Zero || HostHandle == IntPtr.Zero)
        {
            return;
        }

        EmbeddedWindowHost.ResizeToBounds(WindowHandle, bounds);
    }

    /// <summary>
    /// 停止 scrcpy 会话：先尝试优雅关闭窗口，超时后强制终止进程树
    /// </summary>
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
        // 优先发送 WM_CLOSE 请求优雅关闭
                if (WindowHandle != IntPtr.Zero)
                {
                    EmbeddedWindowHost.RequestClose(WindowHandle);
                }
                else
                {
                // 优雅关闭失败则发送关闭主窗口命令
                    try { Process.CloseMainWindow(); } catch { }
                }

                // 等待1.5秒，超时则强制终止进程树
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

    /// <summary>
    /// 释放资源，等效于调用 <see cref="Stop"/>
    /// </summary>
    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// scrcpy 生命周期管理器：查找/验证可执行文件、启动会话、终止进程
/// </summary>
internal sealed class ScrcpyManager
{
    // 缓存已找到的 scrcpy 路径，避免重复磁盘检测
    private string? _scrcpyPath;

    /// <summary>
    /// 随应用分发的 scrcpy 可执行文件路径（位于应用根目录下的 scrcpy.exe）
    /// </summary>
    private static string BundledScrcpyPath => Path.Combine(AppContext.BaseDirectory, "scrcpy.exe");

    /// <summary>
    /// 获取 scrcpy 可执行文件路径，优先返回缓存路径，其次查找随应用分发的版本
    /// </summary>
    /// <returns>scrcpy 路径；未找到时返回 <c>null</c></returns>
    public string? GetScrcpyPath()
    {
        if (!string.IsNullOrEmpty(_scrcpyPath) && File.Exists(_scrcpyPath))
        {
            return _scrcpyPath;
        }

        // 缓存失效，重置为 null 以便重新查找
        _scrcpyPath = null;

        // 检查随应用分发的 scrcpy
        if (File.Exists(BundledScrcpyPath))
        {
            _scrcpyPath = BundledScrcpyPath;
        }

        return _scrcpyPath;
    }

    /// <summary>
    /// 确保 scrcpy 可用，检查现有路径或随应用分发的版本
    /// </summary>
    /// <param name="forceDeploy">是否强制重新部署（当前未实现部署逻辑）</param>
    /// <param name="progress">进度报告器，用于向 UI 反馈状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>scrcpy 路径；未找到时返回 <c>null</c></returns>
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

    /// <summary>
    /// 验证指定路径的 scrcpy 是否可用（执行 <c>--version</c> 检查退出码）
    /// </summary>
    /// <param name="path">待验证的 scrcpy 可执行文件路径</param>
    /// <returns>可用返回 <c>true</c>；否则 <c>false</c></returns>
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

    /// <summary>
    /// 获取 scrcpy 搜索路径列表（当前仅包含随应用分发的路径）
    /// </summary>
    /// <returns>搜索路径列表</returns>
    public List<string> GetSearchPaths()
    {
        return new List<string> { BundledScrcpyPath };
    }

    /// <summary>
    /// 终止所有与指定路径匹配的 scrcpy 进程（含进程树）
    /// </summary>
    /// <param name="scrcpyPath">scrcpy 可执行文件路径；为 <c>null</c> 时不执行任何操作</param>
    /// <remarks>仅终止路径匹配的进程，其他 scrcpy 进程不受影响</remarks>
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

    /// <summary>
    /// 异步启动 scrcpy 会话，构建命令行参数并等待窗口创建完成
    /// </summary>
    /// <param name="options">启动配置选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已启动的 <see cref="ScrcpySession"/> 实例</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <c>null</c></exception>
    /// <exception cref="InvalidOperationException">嵌入模式下宿主句柄未就绪，或 scrcpy 启动失败</exception>
    /// <exception cref="TimeoutException">等待 scrcpy 窗口超时</exception>
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
            psi.ArgumentList.Add(options.WindowX.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-y");
            psi.ArgumentList.Add(options.WindowY.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-width");
            psi.ArgumentList.Add(options.WindowWidth.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--window-height");
            psi.ArgumentList.Add(options.WindowHeight.ToString(CultureInfo.InvariantCulture));
        }

        if (options.AngleDegrees != 0)
        {
            // scrcpy --rotation 参数接受 0/1/2/3（对应 0°/90°/180°/270°）
            psi.ArgumentList.Add("--rotation");
            psi.ArgumentList.Add((options.AngleDegrees / 90).ToString(CultureInfo.InvariantCulture));
        }

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start scrcpy.");
        var stdoutTask = ReadToEndSafeAsync(process.StandardOutput);
        var stderrTask = ReadToEndSafeAsync(process.StandardError);

        try
        {
            var windowHandle = await WaitForWindowAsync(process, stderrTask, cancellationToken, options.WindowTitle).ConfigureAwait(false);
            var session = new ScrcpySession(process, windowHandle, options);
            _ = stdoutTask;
            _ = stderrTask;
            return session;
        }
        // 启动失败时清理进程，防止僵尸进程
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

    /// <summary>
    /// 安全地异步读取流到末尾，异常时返回空字符串
    /// </summary>
    /// <param name="reader">要读取的流读取器</param>
    /// <returns>流内容字符串；异常时返回 <see cref="string.Empty"/></returns>
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

    /// <summary>
    /// 等待 scrcpy 进程创建窗口句柄，超时或进程提前退出时抛出异常
    /// </summary>
    /// <param name="process">scrcpy 进程对象</param>
    /// <param name="stderrTask">stderr 读取任务，用于在进程异常退出时提取错误信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>scrcpy 窗口句柄</returns>
    /// <exception cref="InvalidOperationException">scrcpy 在创建窗口前退出</exception>
    /// <exception cref="TimeoutException">等待窗口超时（约8秒）</exception>
    private static async Task<IntPtr> WaitForWindowAsync(Process process, Task<string> stderrTask, CancellationToken cancellationToken, string windowTitle)
    {
        return await Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 80; attempt++)
            {
                if (attempt % 10 == 0)
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException("scrcpy exited before creating a window.");
                    }

                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        return process.MainWindowHandle;
                    }
                }

                Thread.Sleep(100);
            }

            throw new TimeoutException("Timed out waiting for scrcpy window.");
        }, cancellationToken);
    }

}

/// <summary>
/// Win32 API 互操作工具类，用于将外部窗口嵌入为子控件并管理其位置和大小
/// </summary>
/// <remarks>
/// 通过 Win32 <c>SetParent</c> + 窗口样式修改实现嵌入，仅适用于 Windows 平台。
/// 64 位进程使用 <c>GetWindowLongPtr</c>，32 位进程使用 <c>GetWindowLong</c>。
/// </remarks>
internal static class EmbeddedWindowHost
{
    // GWL_STYLE：窗口样式偏移量
    private const int GwlStyle = -16;
    // WS_CHILD：子窗口样式
    private const int WsChild = 0x40000000;
    // WS_CAPTION：标题栏样式
    private const int WsCaption = 0x00C00000;
    // WS_THICKFRAME：可调大小边框
    private const int WsThickFrame = 0x00040000;
    // WS_MINIMIZE：最小化样式
    private const int WsMinimize = 0x20000000;
    // WS_MAXIMIZE：最大化样式
    private const int WsMaximize = 0x01000000;
    // WS_SYSMENU：系统菜单样式
    private const int WsSysMenu = 0x00080000;
    private const int WsClipChildren = 0x02000000;
    // SWP_NOZORDER：不改变 Z 序
    private const uint SwpNoZOrder = 0x0004;
    // SWP_NOACTIVATE：不激活窗口
    private const uint SwpNoActivate = 0x0010;
    // SWP_FRAMECHANGED：重新应用窗口样式
    private const uint SwpFrameChanged = 0x0020;
    // SW_SHOW：显示窗口
    private const uint SwShow = 5;
    // WM_CLOSE：关闭消息
    private const uint WmClose = 0x0010;

    /// <summary>
    /// 将外部窗口嵌入到宿主控件中，移除标题栏/边框并设置为子窗口样式
    /// </summary>
    /// <param name="windowHandle">要嵌入的外部窗口句柄</param>
    /// <param name="hostHandle">宿主控件窗口句柄</param>
    public static void Attach(IntPtr windowHandle, IntPtr hostHandle)
    {
        if (windowHandle == IntPtr.Zero || hostHandle == IntPtr.Zero)
            return;

        EnableClipChildren(hostHandle);
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

    /// <summary>
    /// 将嵌入的窗口移动并调整到指定边界
    /// </summary>
    /// <param name="windowHandle">嵌入窗口句柄</param>
    /// <param name="bounds">目标边界矩形</param>
    public static void ResizeToBounds(IntPtr windowHandle, Rectangle bounds)
    {
        if (windowHandle == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        MoveWindow(windowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
    }

    /// <summary>
    /// 向指定窗口发送 WM_CLOSE 消息，请求优雅关闭
    /// </summary>
    /// <param name="windowHandle">目标窗口句柄</param>
    public static void RequestClose(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero)
            PostMessage(windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static void EnableClipChildren(IntPtr hostHandle)
    {
        if (hostHandle == IntPtr.Zero)
            return;

        if (IntPtr.Size == 8)
        {
            var style = GetWindowLongPtr(hostHandle, GwlStyle).ToInt64();
            if ((style & WsClipChildren) == 0)
                SetWindowLongPtr(hostHandle, GwlStyle, (IntPtr)(style | WsClipChildren));
        }
        else
        {
            var style = GetWindowLong(hostHandle, GwlStyle);
            if ((style & WsClipChildren) == 0)
                SetWindowLong(hostHandle, GwlStyle, style | WsClipChildren);
        }
    }

    public static IntPtr FindWindowByTitle(string title)
    {
        return FindWindow(null, title);
    }

    public static IntPtr FindWindowByTitlePrefix(string titlePrefix)
    {
        var result = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (GetWindowTextLength(hWnd) > 0 && IsWindowVisible(hWnd))
            {
                var length = GetWindowTextLength(hWnd) + 1;
                var sb = new System.Text.StringBuilder(length);
                GetWindowText(hWnd, sb, length);
                if (sb.ToString().StartsWith(titlePrefix, StringComparison.Ordinal))
                {
                    result = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// 获取宿主窗口的客户区尺寸
    /// </summary>
    /// <param name="hostHandle">宿主窗口句柄</param>
    /// <param name="width">输出宽度</param>
    /// <param name="height">输出高度</param>
    /// <returns>成功获取且尺寸有效返回 <c>true</c>；否则 <c>false</c></returns>
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

    /// <summary>
    /// 获取窗口的整体尺寸（含边框）
    /// </summary>
    /// <param name="windowHandle">窗口句柄</param>
    /// <param name="width">输出宽度</param>
    /// <param name="height">输出高度</param>
    /// <returns>成功获取且尺寸有效返回 <c>true</c>；否则 <c>false</c></returns>
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

    /// <summary>设置窗口的父窗口</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    /// <summary>获取窗口样式（32位进程）</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>设置窗口样式（32位进程）</summary>
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>获取窗口样式（64位进程）</summary>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>设置窗口样式（64位进程）</summary>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>移动并调整窗口位置和大小</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    /// <summary>设置窗口位置、大小和Z序</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>设置窗口的显示状态</summary>
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    /// <summary>获取窗口客户区矩形</summary>
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    /// <summary>获取窗口整体矩形（含边框）</summary>
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    /// <summary>向窗口发送消息（异步）</summary>
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    /// <summary>
    /// Win32 RECT 结构体，表示矩形区域
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
