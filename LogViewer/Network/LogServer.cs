using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LogViewer.Models;
using Timer = System.Threading.Timer;

namespace LogViewer.Network;

/// <summary>
/// TCP 服务器，负责监听并接受 Android 设备的网络日志连接。
/// 支持多设备同时连接，通过 deviceId 管理设备连接生命周期。
/// 内置超时检测定时器，定期扫描 LastActiveTime 超时的连接并断开。
/// </summary>
public class LogServer
{
    private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "server.log");
    private static readonly object FileLogLock = new();

    private static void FileLog(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        lock (FileLogLock)
        {
            // try { File.AppendAllText(LogFilePath, line + Environment.NewLine); } catch { }
        }
        Debug.WriteLine(line);
    }
    private static readonly string LogTag = nameof(LogServer);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private readonly Dictionary<string, DeviceConnection> _connections = new();

    /// <summary>
    /// 超时检测定时器（10 秒间隔扫描），配合 DeviceConnection.LastActiveTime 使用。
    /// 如果某连接超过 TimeoutSeconds 未收到任何消息帧（包括 Ping），则断开该连接。
    /// 客户端每 5s 发 Ping，服务端回复 Pong 并刷新 LastActiveTime，
    /// 因此正常连接的 LastActiveTime 不会超过 5s，
    /// 20s 超时容忍最多 3 次 Ping 丢失后仍判定断连。
    /// </summary>
    private Timer? _timeoutTimer;

    private const int TimeoutSeconds = 20;

    /// <summary>设备连接事件，当新设备注册时触发。</summary>
    public event EventHandler<DeviceInfo>? DeviceConnected;

    /// <summary>设备断开事件，当设备连接断开时触发，参数为 deviceId。</summary>
    public event EventHandler<string>? DeviceDisconnected;

    /// <summary>日志接收事件，当设备发送网络日志时触发，参数为 (deviceId, LogEntry)。</summary>
    public event EventHandler<(string deviceId, LogEntry entry)>? LogReceived;

    /// <summary>普通日志接收事件，当设备发送普通日志时触发，参数为 (deviceId, LogEntry)。</summary>
    public event EventHandler<(string deviceId, LogEntry entry)>? NormalLogReceived;

    /// <summary>当前所有活跃设备连接的只读字典。</summary>
    public IReadOnlyDictionary<string, DeviceConnection> Connections => _connections;

    /// <summary>
    /// 启动 TCP 服务器，开始监听指定端口。
    /// 同时启动超时检测定时器，每 10 秒扫描一次所有连接的活跃时间。
    /// </summary>
    /// <param name="port">监听的端口号，默认 9527。</param>
    public void Start(int port)
    {
        if (_listener != null) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        FileLog($"[{LogTag}] Server started, listening on port {port}");
        _acceptTask = AcceptLoopAsync(_cts.Token);
        _timeoutTimer = new Timer(OnTimeoutCheck, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// 停止 TCP 服务器，断开所有连接并释放资源。
    /// </summary>
    public void Stop()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        foreach (var conn in _connections.Values)
        {
            conn.Disconnect();
        }

        _connections.Clear();
        try
        {
            _acceptTask?.Wait(500);
        }
        catch
        {
        }

        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 异步接受连接循环，持续监听新的 TCP 连接。
    /// 每个新连接创建 DeviceConnection 实例并订阅其事件，
    /// 然后异步启动该连接的消息接收循环。
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(ct);
                FileLog($"[{LogTag}] New connection from {tcpClient.Client.RemoteEndPoint}");
                var connection = new DeviceConnection(tcpClient, ct);
                connection.Registered += OnDeviceRegistered;
                connection.LogReceived += OnLogReceived;
                connection.NormalLogReceived += OnNormalLogReceived;
                connection.Disconnected += OnDeviceDisconnected;

                _ = connection.StartReceivingAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 处理设备注册事件，将设备添加到连接字典。
    /// 如果 deviceId 已存在，先断开旧连接再替换（REPLACE 策略）。
    /// 这是因为 Android 端断线重连后 deviceId 相同但 Socket 是新创建的，
    /// 旧连接的 readLoop 已读到 EOF 会触发 handleClose，
    /// 但在 handleClose 和 OnDeviceRegistered 之间存在竞态：
    /// 如果旧连接的 Disconnected 事件先于新连接的 Registered 事件处理，
    /// 会导致新设备从列表中被移除。
    /// 因此采用 REPLACE 策略：新注册直接替换旧连接，保证列表中始终是最新的连接。
    /// </summary>
    private void OnDeviceRegistered(object? sender, DeviceInfo info)
    {
        if (sender is not DeviceConnection conn) return;
        info.ConnectedTime = DateTime.Now;
        info.IsConnected = true;
        info.AdbSerial = null;

        // 设备唯一标识：优先使用 Android 端发送的 deviceId，
        // 若为空则用 RemoteEndpoint 的 IP 部分（不含端口）作为稳定标识。
        // 不能用含端口的 "unknown_{endpoint}"，因为端口每次重连都变，
        // 会导致同一设备在 _connections 和 DevicePanel._devices 中产生不同的 key，无限添加。
        var deviceId = info.DeviceId;
        if (string.IsNullOrEmpty(deviceId))
        {
            var ep = conn.RemoteEndpoint;
            var colonIdx = ep.LastIndexOf(':');
            deviceId = colonIdx > 0 ? ep[..colonIdx] : ep;
        }

        // REPLACE 策略：同一个 deviceId 的新连接替换旧连接
        if (_connections.TryGetValue(deviceId, out var existing))
        {
            existing.Disconnect();
            _connections.Remove(deviceId);
        }

        _connections[deviceId] = conn;
        conn.DeviceId = deviceId;
        FileLog($"[{LogTag}] Device registered: {deviceId} ({info.DeviceModel}) from {conn.RemoteEndpoint}");
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

    private void OnNormalLogReceived(object? sender, LogEntry entry)
    {
        if (sender is not DeviceConnection conn) return;
        entry.SourceDeviceId = conn.DeviceId;
        NormalLogReceived?.Invoke(this, (conn.DeviceId ?? "", entry));
    }

    /// <summary>
    /// 处理设备断开事件，从连接字典移除并触发 DeviceDisconnected。
    /// 通过引用比较 (existing == conn) 防止 REPLACE 竞态：
    /// 如果新连接已替换旧连接，existing != conn，此时不执行移除操作。
    /// </summary>
    private void OnDeviceDisconnected(object? sender, EventArgs _)
    {
        if (sender is not DeviceConnection conn) return;
        var deviceId = conn.DeviceId;

        // 引用比较：只有当字典中的连接就是触发断开事件的那个实例时才移除，
        // 防止旧连接的 Disconnected 事件误删已被新连接替换的条目
        if (deviceId != null && _connections.TryGetValue(deviceId, out var existing) && existing == conn)
        {
            _connections.Remove(deviceId);
            DeviceDisconnected?.Invoke(this, deviceId);
        }

        conn.Registered -= OnDeviceRegistered;
        conn.LogReceived -= OnLogReceived;
        conn.NormalLogReceived -= OnNormalLogReceived;
        conn.Disconnected -= OnDeviceDisconnected;
    }

    /// <summary>
    /// 超时检测回调，定期扫描所有连接的 LastActiveTime。
    /// 如果连接超时未收到任何消息帧（包括 Ping 心跳），则主动断开，
    /// 触发客户端重连流程。超时阈值为 TimeoutSeconds（20 秒），
    /// 容忍最多 3 次心跳丢失（心跳间隔 5s）。
    /// </summary>
    private void OnTimeoutCheck(object? state)
    {
        var now = DateTime.Now;
        foreach (var kvp in _connections.ToList())
        {
            if ((now - kvp.Value.LastActiveTime).TotalSeconds > TimeoutSeconds)
            {
                FileLog($"[{LogTag}] Timeout: {kvp.Key}, last active {kvp.Value.LastActiveTime:HH:mm:ss}");
                kvp.Value.Disconnect();
            }
        }
    }

    /// <summary>
    /// 获取指定设备的注册信息。
    /// </summary>
    /// <param name="deviceId">设备唯一标识。</param>
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