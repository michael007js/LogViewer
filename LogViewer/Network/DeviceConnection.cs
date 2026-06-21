using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Network;

public class DeviceConnection
{
    private readonly TcpClient _tcpClient;
    private readonly CancellationToken _ct;
    private readonly NetworkStream _stream;

    public string? DeviceId { get; set; }
    public DeviceInfo? DeviceInfo { get; private set; }
    public string RemoteEndpoint => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "";

    public event EventHandler<DeviceInfo>? Registered;
    public event EventHandler<LogEntry>? LogReceived;
    public event EventHandler? Disconnected;

    public DeviceConnection(TcpClient tcpClient, CancellationToken ct)
    {
        _tcpClient = tcpClient;
        _ct = ct;
        _stream = tcpClient.GetStream();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task StartReceivingAsync()
    {
        try
        {
            while (!_ct.IsCancellationRequested && _tcpClient.Connected)
            {
                var lengthBytes = await ReadExactAsync(4);
                if (lengthBytes == null) break;

                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                int totalLength = BitConverter.ToInt32(lengthBytes, 0);
                if (totalLength <= 0 || totalLength > 10 * 1024 * 1024) break;

                var payloadBytes = await ReadExactAsync(totalLength);
                if (payloadBytes == null) break;

                byte messageType = payloadBytes[0];
                string jsonStr = Encoding.UTF8.GetString(payloadBytes, 1, totalLength - 1);

                switch (messageType)
                {
                    case 0x01:
                        DeviceInfo = JsonSerializer.Deserialize<DeviceInfo>(jsonStr, JsonOptions);
                        if (DeviceInfo != null)
                        {
                            Registered?.Invoke(this, DeviceInfo);
                        }
                        break;
                    case 0x02:
                        var entry = JsonSerializer.Deserialize<LogEntry>(jsonStr, JsonOptions);
                        if (entry != null)
                        {
                            LogReceived?.Invoke(this, entry);
                        }
                        break;
                }
            }
        }
        catch { }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<byte[]?> ReadExactAsync(int count)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await _stream.ReadAsync(buffer, offset, count - offset, _ct);
                if (read == 0) return null;
                offset += read;
            }
            var result = new byte[count];
            Array.Copy(buffer, result, count);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Disconnect()
    {
        try { _stream.Close(); } catch { }
        try { _tcpClient.Close(); } catch { }
    }
}