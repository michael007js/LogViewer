using System.Buffers;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LogViewer.Models;

namespace LogViewer.Network;

/// <summary>
/// 单设备 TCP 连接管理器，负责与 Android 设备建立 TCP 连接并解析协议帧。
/// 协议格式：[4字节大端int=长度][1字节消息类型][UTF-8 JSON字节]
/// 支持设备注册（0x01）和网络日志（0x02）两种消息类型。
/// </summary>
public class DeviceConnection
{
    private readonly TcpClient _tcpClient;
    private readonly CancellationToken _ct;
    private readonly NetworkStream _stream;

    /// <summary>设备唯一标识，由 LogServer 在设备注册时分配。</summary>
    public string? DeviceId { get; set; }

    /// <summary>设备注册信息，包含型号、版本等元数据。</summary>
    public DeviceInfo? DeviceInfo { get; private set; }

    /// <summary>远程端点地址，格式如 "192.168.1.100:12345"。</summary>
    public string RemoteEndpoint => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "";

    /// <summary>设备注册事件，当收到 0x01 消息时触发。</summary>
    public event EventHandler<DeviceInfo>? Registered;

    /// <summary>日志接收事件，当收到 0x02 消息时触发。</summary>
    public event EventHandler<LogEntry>? LogReceived;

    /// <summary>连接断开事件，当 TCP 连接关闭或异常时触发。</summary>
    public event EventHandler? Disconnected;

    /// <summary>
    /// 初始化设备连接实例。
    /// </summary>
    /// <param name="tcpClient">TCP 客户端实例。</param>
    /// <param name="ct">取消令牌，用于停止接收循环。</param>
    public DeviceConnection(TcpClient tcpClient, CancellationToken ct)
    {
        _tcpClient = tcpClient;
        _ct = ct;
        _stream = tcpClient.GetStream();
    }

    /// <summary>
    /// JSON 序列化选项，设置 PropertyNameCaseInsensitive = true 以兼容 Android 端 Gson 的小写驼峰命名。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 异步接收并解析 TCP 数据流，循环读取消息帧并分发处理。
    /// 协议帧格式：[4字节大端int=长度][1字节消息类型][UTF-8 JSON字节]
    /// </summary>
    public async Task StartReceivingAsync()
    {
        try
        {
            while (!_ct.IsCancellationRequested && _tcpClient.Connected)
            {
                // 读取4字节长度字段（大端序）
                var lengthBytes = await ReadExactAsync(4);
                if (lengthBytes == null) break;

                // 大端序转小端序：Java ByteBuffer.putInt() 默认大端，C# BitConverter 是小端
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                int totalLength = BitConverter.ToInt32(lengthBytes, 0);
                if (totalLength <= 0 || totalLength > 10 * 1024 * 1024) break;

                // 读取payload数据
                var payloadBytes = await ReadExactAsync(totalLength);
                if (payloadBytes == null) break;

                // 解析消息类型和JSON内容
                byte messageType = payloadBytes[0];
                string jsonStr = Encoding.UTF8.GetString(payloadBytes, 1, totalLength - 1);

                // 根据消息类型分发处理
                switch (messageType)
                {
                    case 0x01: // 设备注册消息
                        DeviceInfo = JsonSerializer.Deserialize<DeviceInfo>(jsonStr, JsonOptions);
                        if (DeviceInfo != null)
                        {
                            Registered?.Invoke(this, DeviceInfo);
                        }

                        break;
                    case 0x02: // 网络日志消息
                        var entry = JsonSerializer.Deserialize<LogEntry>(jsonStr, JsonOptions);
                        if (entry != null)
                        {
                            // 尝试解压 Gzip 压缩的内容
                            entry.Content = TryDecodeGzipJsonContent(entry.Content);
                            LogReceived?.Invoke(this, entry);
                        }

                        break;
                }
            }
        }
        catch
        {
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 从网络流中精确读取指定字节数的数据。
    /// 使用 ArrayPool 复用缓冲区，减少内存分配。
    /// </summary>
    /// <param name="count">需要读取的字节数。</param>
    /// <returns>读取的字节数组，如果连接断开则返回 null。</returns>
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

    /// <summary>
    /// 尝试解压 Gzip 压缩的 Base64 编码内容。
    /// 通过检查 Gzip 魔数 (0x1F 0x8B) 判断是否为压缩数据。
    /// </summary>
    /// <param name="content">Base64 编码的内容字符串。</param>
    /// <returns>解压后的内容，如果解压失败则返回原始内容。</returns>
    private static string? TryDecodeGzipContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            var bytes = Convert.FromBase64String(content);
            // 检查 Gzip 魔数 (0x1F 0x8B)
            if (bytes.Length < 2 || bytes[0] != 0x1F || bytes[1] != 0x8B)
            {
                return content;
            }

            using var input = new MemoryStream(bytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return content;
        }
    }

    /// <summary>
    /// 尝试解压并规范化 JSON 内容，处理嵌套的 Gzip 压缩。
    /// </summary>
    /// <param name="content">可能包含 Gzip 压缩的 JSON 内容。</param>
    /// <returns>规范化后的 JSON 字符串。</returns>
    private static string? TryDecodeGzipJsonContent(string? content)
    {
        var decoded = TryDecodeGzipContent(content);
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return decoded;
        }

        try
        {
            using var doc = JsonDocument.Parse(decoded);
            var changed = false;
            var normalized = NormalizeJsonElement(doc.RootElement, ref changed);
            return changed ? JsonSerializer.Serialize(normalized, JsonOptions) : decoded;
        }
        catch
        {
            return decoded;
        }
    }

    /// <summary>
    /// 递归规范化 JSON 元素，处理嵌套的 Gzip 压缩字符串。
    /// </summary>
    /// <param name="element">要规范化的 JSON 元素。</param>
    /// <param name="changed">标记是否有内容被解压修改。</param>
    /// <returns>规范化后的对象。</returns>
    private static object? NormalizeJsonElement(JsonElement element, ref bool changed)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeObject(element, ref changed),
            JsonValueKind.Array => NormalizeArray(element, ref changed),
            JsonValueKind.String => NormalizeString(element.GetString(), ref changed),
            JsonValueKind.Number => JsonSerializer.Deserialize<object>(element.GetRawText(), JsonOptions),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// 规范化 JSON 对象，递归处理所有属性值。
    /// </summary>
    private static Dictionary<string, object?> NormalizeObject(JsonElement element, ref bool changed)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = NormalizeJsonElement(prop.Value, ref changed);
        }

        return result;
    }

    /// <summary>
    /// 规范化 JSON 数组，递归处理所有元素。
    /// </summary>
    private static List<object?> NormalizeArray(JsonElement element, ref bool changed)
    {
        var result = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(NormalizeJsonElement(item, ref changed));
        }

        return result;
    }

    /// <summary>
    /// 规范化 JSON 字符串值，尝试解压可能的 Gzip 压缩内容。
    /// </summary>
    private static object? NormalizeString(string? value, ref bool changed)
    {
        var decoded = TryDecodeGzipContent(value);
        if (string.Equals(decoded, value, StringComparison.Ordinal))
        {
            return value;
        }

        changed = true;
        if (string.IsNullOrWhiteSpace(decoded))
        {
            return decoded;
        }

        try
        {
            using var doc = JsonDocument.Parse(decoded);
            return NormalizeJsonElement(doc.RootElement, ref changed);
        }
        catch
        {
            return decoded;
        }
    }

    /// <summary>
    /// 断开 TCP 连接，关闭网络流和客户端。
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _stream.Close();
        }
        catch
        {
        }

        try
        {
            _tcpClient.Close();
        }
        catch
        {
        }
    }
}