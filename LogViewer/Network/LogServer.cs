using System.Net.Sockets;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Network;

public class LogServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private readonly Dictionary<string, DeviceConnection> _connections = new();

    public event EventHandler<DeviceInfo>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;
    public event EventHandler<(string deviceId, LogEntry entry)>? LogReceived;
    public IReadOnlyDictionary<string, DeviceConnection> Connections => _connections;

    public void Start(int port)
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(System.Net.IPAddress.Any, port);
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        foreach (var conn in _connections.Values)
        {
            conn.Disconnect();
        }
        _connections.Clear();
        try { _acceptTask?.Wait(500); } catch { }
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(ct);
                var connection = new DeviceConnection(tcpClient, ct);
                connection.Registered += OnDeviceRegistered;
                connection.LogReceived += OnLogReceived;
                connection.Disconnected += OnDeviceDisconnected;

                _ = connection.StartReceivingAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
        }
    }

    private void OnDeviceRegistered(object? sender, DeviceInfo info)
    {
        if (sender is not DeviceConnection conn) return;
        info.ConnectedTime = DateTime.Now;
        info.IsConnected = true;
        info.AdbSerial = null;

        var deviceId = info.DeviceId ?? $"unknown_{conn.RemoteEndpoint}";
        if (_connections.TryGetValue(deviceId, out var existing))
        {
            existing.Disconnect();
            _connections.Remove(deviceId);
        }
        _connections[deviceId] = conn;
        conn.DeviceId = deviceId;
        DeviceConnected?.Invoke(this, info);
    }

    private void OnLogReceived(object? sender, LogEntry entry)
    {
        if (sender is not DeviceConnection conn) return;
        entry.SourceDeviceId = conn.DeviceId;
        LogReceived?.Invoke(this, (conn.DeviceId ?? "", entry));
    }

    private void OnDeviceDisconnected(object? sender, EventArgs _)
    {
        if (sender is not DeviceConnection conn) return;
        var deviceId = conn.DeviceId;
        if (deviceId != null && _connections.TryGetValue(deviceId, out var existing) && existing == conn)
        {
            _connections.Remove(deviceId);
            DeviceDisconnected?.Invoke(this, deviceId);
        }
        conn.Registered -= OnDeviceRegistered;
        conn.LogReceived -= OnLogReceived;
        conn.Disconnected -= OnDeviceDisconnected;
    }

    public DeviceInfo? GetDeviceInfo(string deviceId)
    {
        if (_connections.TryGetValue(deviceId, out var conn))
        {
            return conn.DeviceInfo;
        }
        return null;
    }
}