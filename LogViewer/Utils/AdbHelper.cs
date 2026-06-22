using System.Diagnostics;

namespace LogViewer.Utils;

public class AdbDevice
{
    public string Serial { get; set; } = "";
    public string State { get; set; } = "";
    public string Model { get; set; } = "";
    public string DisplayName => string.IsNullOrEmpty(Model) ? Serial : $"{Model} ({Serial})";
}

public class AdbHelper
{
    private string? _adbPath;
    private static string BundledAdbPath => Path.Combine(AppContext.BaseDirectory, "adb.exe");

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

    public bool IsAdbAvailable() => GetAdbPath() != null;

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

    public List<string> GetSearchPaths()
    {
        return new List<string> { BundledAdbPath };
    }

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

    public void EnsureServerStarted()
    {
        var adbPath = GetAdbPath();
        if (adbPath == null) return;
        EnsureServerStarted(adbPath);
    }

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

    public (bool success, string output) ReversePort(string adbPath, AdbDevice device, int port)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse tcp:{port} tcp:{port}");
    }

    public (bool success, string output) RemoveReverse(string adbPath, AdbDevice device, int port)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse --remove tcp:{port}");
    }

    public (bool success, string output) RemoveAllReverses(string adbPath, AdbDevice device)
    {
        return RunAdbCommand(adbPath, $"-s {device.Serial} reverse --remove-all");
    }

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
