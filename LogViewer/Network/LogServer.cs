using System.Net.Sockets;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Network;

/// <summary>
/// TCP 服务器，负责监听并接受 Android 设备的网络日志连接。
/// 支持多设备同时连接，通过 deviceId 管理设备连接生命周期。
/// </summary>
public class LogServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private readonly Dictionary<string, DeviceConnection> _connections = new();

    /// <summary>设备连接事件，当新设备注册时触发。</summary>
    public event EventHandler<DeviceInfo>? DeviceConnected;
    /// <summary>设备断开事件，当设备连接断开时触发，参数为 deviceId。</summary>
    public event EventHandler<string>? DeviceDisconnected;
    /// <summary>日志接收事件，当设备发送网络日志时触发，参数为 (deviceId, LogEntry)。</summary>
    public event EventHandler<(string deviceId, LogEntry entry)>? LogReceived;
    /// <summary>当前所有活跃设备连接的只读字典。</summary>
    public IReadOnlyDictionary<string, DeviceConnection> Connections => _connections;

    /// <summary>
    /// 启动 TCP 服务器，开始监听指定端口。
    /// </summary>
    /// <param name="port">监听的端口号，默认 9527。</param>
    public void Start(int port)
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(System.Net.IPAddress.Any, port);
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
    }

    /// <summary>
    /// 停止 TCP 服务器，断开所有连接并释放资源。
    /// </summary>
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

    /// <summary>
    /// 异步接受连接循环，持续监听新的 TCP 连接。
    /// </summary>
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

    /// <summary>
    /// 处理设备注册事件，将设备添加到连接字典。
    /// 如果 deviceId 已存在，先断开旧连接再替换。
    /// </summary>
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

    /// <summary>
    /// 处理日志接收事件，标记来源设备并转发给上层。
    /// </summary>
    private void OnLogReceived(object? sender, LogEntry entry)
    {
        if (sender is not DeviceConnection conn) return;
        entry.SourceDeviceId = conn.DeviceId;
        LogReceived?.Invoke(this, (conn.DeviceId ?? "", entry));
    }

    /// <summary>
    /// 处理设备断开事件，从连接字典移除并触发 DeviceDisconnected。
    /// </summary>
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

    /// <summary>
    /// 获取指定设备的注册信息。
    /// </summary>
    /// <param name="device">设备唯一标识。</param>
    /// <returns>设备信息，如果设备不存在则返回 null。</returns>
    public DeviceInfo? GetDeviceInfo(string deviceId)
    {
        if (_connections.TryGetValue(deviceId, out var conn))
        {
            return conn.DeviceInfo;
        }
        return null;
    }
}