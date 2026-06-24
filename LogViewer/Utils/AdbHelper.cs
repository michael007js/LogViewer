using System.Diagnostics;

namespace LogViewer.Utils;

/// <summary>
/// ADB 设备信息模型，包含序列号、状态和型号。
/// </summary>
public class AdbDevice
{
    /// <summary>设备序列号。</summary>
    public string Serial { get; set; } = "";
    /// <summary>设备状态（如 "device"、"offline"）。</summary>
    public string State { get; set; } = "";
    /// <summary>设备型号名称。</summary>
    public string Model { get; set; } = "";
    /// <summary>设备显示名称，格式为 "型号 (序列号)" 或仅序列号。</summary>
    public string DisplayName => string.IsNullOrEmpty(Model) ? Serial : $"{Model} ({Serial})";
}

/// <summary>
/// ADB 辅助类，提供 ADB 命令执行、设备检测和端口映射功能。
/// </summary>
public class AdbHelper
{
    private string? _adbPath;
    /// <summary>程序目录下捆绑的 adb.exe 路径。</summary>
    private static string BundledAdbPath => Path.Combine(AppContext.BaseDirectory, "adb.exe");

    /// <summary>
    /// 获取 ADB 可执行文件路径，优先使用缓存路径，其次检查程序目录。
    /// </summary>
    /// <returns>ADB 路径，如果未找到则返回 null。</returns>
    public string? GetAdbPath()
    {
        if (_adbPath != null && File.Exists(_adbPath)) return _adbPath;
        if (_adbPath != null) _adbPath = null;

        if (File.Exists(BundledAdbPath))
        {
            _adbPath = BundledAdbPath;
            return _adbPath;
        }

        return null;
    }

    /// <summary>
    /// 检查 ADB 是否可用。
    /// </summary>
    public bool IsAdbAvailable() => GetAdbPath() != null;

    /// <summary>
    /// 验证指定路径的 ADB 可执行文件是否有效。
    /// </summary>
    /// <param name="path">ADB 可执行文件路径。</param>
    /// <returns>验证是否通过。</returns>
    public bool ValidateAdb(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// 获取 ADB 搜索路径列表。
    /// </summary>
    public List<string> GetSearchPaths()
    {
        return new List<string> { BundledAdbPath };
    }

    /// <summary>
    /// 获取已连接的 ADB 设备列表。
    /// </summary>
    /// <returns>设备列表，如果 ADB 不可用则返回空列表。</returns>
    public List<AdbDevice> GetDevices()
    {
        var adbPath = GetAdbPath();
        if (adbPath == null) return new();

        try
        {
            EnsureServerStarted(adbPath);
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = "devices -l",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return new();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            var devices = new List<AdbDevice>();
            var lines = output.Split('\n');
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var serial = parts[0];
                var state = parts[1];
                if (state != "device") continue;

                var model = "";
                for (int i = 2; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("model:"))
                    {
                        model = parts[i].Substring("model:".Length);
                        break;
                    }
                }

                devices.Add(new AdbDevice { Serial = serial, State = state, Model = model });
            }

            foreach (var d in devices)
            {
                d.Model = GetDeviceModel(adbPath, d.Serial, d.Model);
            }

            return devices;
        }
        catch { return new(); }
    }

    /// <summary>
    /// 获取设备型号，如果已有型号则直接返回，否则通过 adb 命令查询。
    /// </summary>
    private string GetDeviceModel(string adbPath, string serial, string existingModel)
    {
        if (!string.IsNullOrEmpty(existingModel)) return existingModel;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = $"-s {serial} shell getprop ro.product.model",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return existingModel;
            var result = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            return string.IsNullOrEmpty(result) ? existingModel : result;
        }
        catch { return existingModel; }
    }

    /// <summary>
    /// 确保 ADB 服务器已启动。
    /// </summary>
    public void EnsureServerStarted()
    {
        var adbPath = GetAdbPath();
        if (adbPath == null) return;
        EnsureServerStarted(adbPath);
    }

    /// <summary>
    /// 确保 ADB 服务器已启动（内部方法）。
    /// </summary>
    private void EnsureServerStarted(string adbPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = "start-server",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }

    /// <summary>
    /// 执行 ADB reverse 端口映射，将 PC 端口映射到 Android 设备。
    /// </summary>
    /// <param name="adbPath">ADB 可执行文件路径。</param>
    /// <param name="device">目标设备。</param>
    /// <param name="port">要映射的端口号。</param>
    /// <returns>执行结果和输出。</returns>
    public (bool success, string output) ReversePort(string adbPath, AdbDevice device, int port)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse tcp:{port} tcp:{port}");
    }

    /// <summary>
    /// 移除指定设备的 ADB reverse 端口映射。
    /// </summary>
    public (bool success, string output) RemoveReverse(string adbPath, AdbDevice device, int port)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse --remove tcp:{port}");
    }

    /// <summary>
    /// 移除指定设备的所有 ADB reverse 端口映射。
    /// </summary>
    public (bool success, string output) RemoveAllReverses(string adbPath, AdbDevice device)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse --remove-all");
    }

    /// <summary>
    /// 执行 ADB 命令并返回结果。
    /// </summary>
    private (bool success, string output) RunAdbCommand(string adbPath, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Failed to start process");

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);

            var result = string.IsNullOrEmpty(error) ? output : output + "\n" + error;
            return (proc.ExitCode == 0, result);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
