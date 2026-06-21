# LogViewer 完整实施计划（最终版）

---

## 核心架构

**C# 做 TCP Server，Android 做 TCP Client**（支持多设备，用 `adb reverse`）

多台手机各自执行 `adb -s {serial} reverse tcp:9527 tcp:9527`，手机上 `localhost:9527` 映射到电脑 9527 端口，所有设备连接同一个 Server。

系统日志通过 C# 端直接运行 `adb logcat` 获取，不走手机 TCP 通道，零手机端开销。

---

## 一、Android 端（4个文件，1新建 + 3处小改动）

### 文件1：新建 `NetworkLogSender.java`

**路径**：`app/src/main/java/com/eagersoft/youzy/youzy/debug/NetworkLogSender.java`
**包名**：`com.eagersoft.youzy.youzy.debug`

- **单例**，`getInstance()` 获取实例
- **`start()` 方法**：仅在 `AppConstant.IS_DEBUG == true` 时启动。后台线程循环尝试连接 `localhost:9527`。连接成功后：
  1. 先发送一条 **设备注册消息**（消息类型 `0x01`）
  2. 进入发送状态，从 `LinkedBlockingQueue<byte[]>` 取数据写入 Socket
  3. 连接断开后清空流引用，等待1秒后重试连接
- **`send(NetworkResponseEntity entity)` 方法**：序列化为 JSON，打包为消息类型 `0x02`，放入队列。队列满时丢弃最旧数据：
  ```java
  if (!sendQueue.offer(data)) {
      sendQueue.poll();
      sendQueue.offer(data);
  }
  ```
- **大响应体截断**：`content` 和 `send` 字段超过 `AppConstant.NETWORK_LOG_MAX_BODY_SIZE`（默认50KB）时截断，末尾追加 `[truncated, original: XXX KB]`
- **`stop()` 方法**：关闭 Socket，停止线程
- 队列容量引用 `AppConstant.NETWORK_LOG_SENDER_QUEUE_SIZE`
- 所有 IO 在子线程，不阻塞 OkHttp 拦截器线程

**设备注册消息 JSON**（消息类型 `0x01`）：
```json
{
  "deviceId": "YouZyUtils.getRegistrationId()值",
  "deviceModel": "Build.BRAND + ' ' + Build.MODEL",
  "androidVersion": "Build.VERSION.RELEASE",
  "appVersion": "AppUtil.getVersionName()值",
  "isQa": "AppConstant.IS_QA"
}
```

**日志数据 JSON**（消息类型 `0x02`，13个字段全传）：
```json
{
  "send": "请求体",
  "message": "响应消息",
  "url": "完整URL",
  "code": 200,
  "isRedirect": false,
  "isSuccessful": true,
  "isHttps": true,
  "protocol": "http/1.1",
  "method": "POST",
  "headers": "请求头",
  "content": "响应内容",
  "sendTime": 1718500000000,
  "receiveTime": 1718500001234
}
```

**消息协议**：`[4字节大端int=总长度][1字节消息类型][UTF-8 JSON字节]`
- `0x01` = 设备注册
- `0x02` = 日志数据

### 文件2：修改 `OkHttp3Utils.java`

**路径**：`app/src/main/java/com/eagersoft/youzy/youzy/data/retrofit/OkHttp3Utils.java`

1. 新增 import：`import com.eagersoft.youzy.youzy.debug.NetworkLogSender;`
2. 在 `response()` 方法第241行 `networkResponseInfo.print(...)` 之后加一行：
   ```java
   NetworkLogSender.getInstance().send(networkResponseInfo);
   ```
   **不做其他任何修改。**

### 文件3：修改 `MyApplication.java`

**路径**：`app/src/main/java/com/eagersoft/youzy/youzy/application/MyApplication.java`

1. 新增 import：`import com.eagersoft.youzy.youzy.debug.NetworkLogSender;`
2. 在 `onCreate()` 方法末尾加一行：
   ```java
   NetworkLogSender.getInstance().start();
   ```

### 文件4：修改 `AppConstant.java`

**路径**：`app/src/main/java/com/eagersoft/youzy/youzy/constants/AppConstant.java`

新增2行常量：
```java
public static final int NETWORK_LOG_SENDER_QUEUE_SIZE = 1000;
public static final int NETWORK_LOG_MAX_BODY_SIZE = 50 * 1024; // 50KB
```

---

## 二、C# WinForms 客户端（独立工程）

**工程路径**：`D:\youzyapp\youzy.android.log.client`

### 项目结构

```
youzy.android.log.client/
├── LogViewer.sln
├── LogViewer/
│   ├── LogViewer.csproj          (.NET 8 WinForms)
│   ├── Program.cs
│   ├── app.manifest
│   ├── Models/
│   │   ├── LogEntry.cs                  (网络日志数据，13个字段)
│   │   ├── SystemLogEntry.cs           (系统日志数据)
│   │   ├── DeviceInfo.cs                (设备注册信息)
│   │   ├── AppSettings.cs              (应用设置模型)
│   │   └── RingBuffer.cs               (高性能环形缓冲区)
│   ├── Network/
│   │   ├── LogServer.cs                 (TCP Server，接受多设备连接)
│   │   ├── DeviceConnection.cs          (单设备连接管理+协议解析)
│   │   └── LogcatReader.cs             (ADB logcat 进程读取+解析)
│   ├── UI/
│   │   ├── MainForm.cs
│   │   ├── MainForm.Designer.cs
│   │   ├── MainForm.resx
│   │   ├── JsonTreeView.cs              (JSON折叠+语法高亮)
│   │   ├── DevicePanel.cs              (设备列表+切换)
│   │   └── SettingsDialog.cs           (设置对话框)
│   └── Utils/
│       ├── JsonFormatter.cs
│       └── AdbHelper.cs
```

### LogEntry.cs — 网络日志数据模型

```csharp
public class LogEntry
{
    public string? Send { get; set; }
    public string? Message { get; set; }
    public string? Url { get; set; }
    public int Code { get; set; }
    public bool IsRedirect { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsHttps { get; set; }
    public string? Protocol { get; set; }
    public string? Method { get; set; }
    public string? Headers { get; set; }
    public string? Content { get; set; }
    public long SendTime { get; set; }
    public long ReceiveTime { get; set; }

    // 计算属性
    public long Duration => ReceiveTime - SendTime;
    public string UrlPath => 提取URL路径部分;
    public DateTime SendTimeDt => DateTimeOffset.FromUnixTimeMilliseconds(SendTime).LocalDateTime;

    // 运行时关联
    public string? SourceDeviceId { get; set; }
}
```

### SystemLogEntry.cs — 系统日志数据模型

```csharp
public class SystemLogEntry
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
    public string? PackageName { get; set; }
    public string? Level { get; set; }       // V/D/I/W/E/F
    public string? Tag { get; set; }
    public string? Message { get; set; }
    public string? SourceDeviceSerial { get; set; }

    // 颜色映射
    public Color LevelColor => Level switch {
        "V" => Color.Gray, "D" => Color.Blue,
        "I" => Color.Green, "W" => Color.Orange,
        "E" => Color.Red, "F" => Color.DarkRed,
        _ => Color.Black
    };
}
```

### DeviceInfo.cs — 设备信息模型

```csharp
public class DeviceInfo
{
    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public string? AndroidVersion { get; set; }
    public string? AppVersion { get; set; }
    public bool IsQa { get; set; }

    // 运行时属性
    public DateTime ConnectedTime { get; set; }
    public bool IsConnected { get; set; }
    public string? AdbSerial { get; set; }
    public string DisplayName => $"{DeviceModel} ({AppVersion})";
}
```

### AppSettings.cs — 应用设置模型

```csharp
public class AppSettings
{
    public int ServerPort { get; set; } = 9527;
    public int MaxLogEntriesPerDevice { get; set; } = 5000;
    public int MaxLogEntriesAll { get; set; } = 10000;
    public int MaxSystemLogEntries { get; set; } = 10000;
    public int AndroidQueueSize { get; set; } = 1000;
    public int MaxBodySizeKb { get; set; } = 50;
    public bool AutoAdbReverse { get; set; } = true;
    public bool AutoStartLogcat { get; set; } = true;
    public bool AutoFormatJson { get; set; } = true;
    public int FontSize { get; set; } = 11;
    public string LogcatFilter { get; set; } = "";
}
```

### RingBuffer.cs — 高性能环形缓冲区

```csharp
public class RingBuffer<T>
{
    private T[] _buffer;
    private int _head;    // 下一个写入位置
    private int _tail;    // 最旧数据位置
    private int _count;

    public RingBuffer(int capacity) { _buffer = new T[capacity]; }

    public void Add(T item) {
        if (_count == _buffer.Length) {
            _buffer[_tail] = default!;
            _tail = (_tail + 1) % _buffer.Length;
        } else {
            _count++;
        }
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
    }

    public T Get(int index) {
        if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException();
        return _buffer[(_tail + index) % _buffer.Length];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    public void Clear() {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0; _tail = 0; _count = 0;
    }
}
```

- O(1) Add 和 Get，无 RemoveAt(0) 的 O(n) 开销
- 达到上限时自动淘汰最旧数据
- 替代所有日志存储

### LogServer.cs — TCP Server

- `Start(int port)`：启动 `TcpListener` 监听
- `Stop()`：停止监听，关闭所有设备连接
- 异步接受连接：循环 `AcceptTcpClientAsync()`，创建 `DeviceConnection`
- 事件：DeviceConnected / DeviceDisconnected / LogReceived
- `Dictionary<string, DeviceConnection> Connections`

### DeviceConnection.cs — 单设备连接管理

- 管理一个 `TcpClient` 连接
- 接收循环：异步读取 `[4字节长度][1字节类型][JSON字节]`
  - `0x01` → `DeviceInfo`
  - `0x02` → `LogEntry`
- 高性能接收：`ArrayPool<byte>.Shared` 池化缓冲区
- `Disconnect()`：关闭连接

### LogcatReader.cs — 系统日志读取器

**通过 ADB logcat 获取，不走手机TCP通道**。

- `Start(AdbDevice device, string filter)`：启动 `adb -s {serial} logcat -v threadtime {filter}` 进程
- `Stop()`：终止 logcat 进程
- 逐行读取 stdout，解析为 `SystemLogEntry`
- 事件：SystemLogReceived / ProcessExited
- 内存优化：逐行读取，不缓冲整个 stdout

### AdbHelper.cs — ADB 工具

```csharp
public class AdbDevice
{
    public string Serial { get; set; }
    public string State { get; set; }
    public string Model { get; set; }
}
```

方法：IsAdbAvailable / GetAdbPath / GetDevices / ReversePort / RemoveReverse / RemoveAllReverses

### JsonTreeView.cs — JSON 折叠+语法高亮控件

继承 `TreeView`，`DrawMode = OwnerDrawText`。

节点显示：object `► "key": { 3 items }`，array `► "key": [ 5 items ]`，primitives 叶子节点。

着色：key蓝/string绿/number橙/bool紫/null灰/摘要灰斜体。

API：DisplayJson / DisplayPlainText / ExpandAll / CollapseAll / CollapseToLevel / SearchAndHighlight

搜索：匹配节点黄色背景，自动展开，底部显示匹配数。

右键：复制值/复制路径/复制节点/展开全部/折叠全部/折到2层

### DevicePanel.cs — 设备列表面板

顶部"全部"选项 + 每台设备（状态/型号/版本/条数）。点击切换，右键删除/ADB Reverse/启动Logcat。

### SettingsDialog.cs — 设置对话框

菜单 `工具→设置`。配置：端口/上限/队列/截断/自动ADB/自动Logcat/JSON折叠/字体/Logcat过滤。持久化 `Properties.Settings.Default`。

### MainForm.cs — 主窗口

**3面板 SplitContainer 嵌套布局**：

```
┌─────────────────────────────────────────────────────────────────────────┐
│ [工具]→[设置]                                                           │
│ 工具栏: [启动服务▼端口9527] [停止服务] [ADB Reverse▼]  ●服务运行中      │
├────────┬────────────────────────┬────────────────────────────────────────┤
│        │  ┌─网络请求─┬─系统日志─┐│                                        │
│ 设备   │  │ (Tab切换)           ││  JSON 预览面板                          │
│ 面板   │  ├────────────────────┤│                                        │
│        │  │关键字:[___] M▼ S▼ ││ ┌──────────────────────────────────────┐│
│┌──────┐│  │[⬇滚动到底部]       ││ │ [请求头][请求体][响应体] Tab           ││
││●全部 ││  │日志:3452/5000      ││ │ 搜索:[____] [▶搜索]                  ││
││      ││  ├────────────────────┤│ │ [展开][折叠][折2层]                   ││
││●设备1││  │▶ POST /api/x 200ms││ │                                       ││
││Galaxy││  │  GET  /api/y 404  ││ │  ▼ {                                  ││
││S21   ││  │▶ POST /api/z 200ms││ │    ▼ "code": 200                     ││
││3.5.1 ││  │  ...               ││ │    ► "data": { 5 items }             ││
││128条 ││  │                    ││ │    ► "list": [ 12 items ]            ││
││      ││  │                    ││ │  }                                    ││
││○设备2││  │                    ││ │                                       ││
││Xiaomi││  │                    ││ │                                       ││
││2.8.0 ││  │                    ││ │                                       ││
││45条  ││  │                    ││ │                                       ││
│└──────┘│  └────────────────────┘│ └──────────────────────────────────────┘│
│◄可拖─► │◄──── 可拖动 ────────►  │                                         │
├────────┴────────────────────────┴────────────────────────────────────────┤
│ [清空] [导出JSON] [导出TXT]                                              │
│ 服务9527 | 设备:2 | ADB:已检测 | Logcat:2设备运行                        │
└─────────────────────────────────────────────────────────────────────────┘
```

**系统日志Tab**：
```
┌──────────────────────────────────────────┐
│  筛选: 关键字:[___] Level▼ Tag▼          │
│  [⬇滚动到底部] 日志:8000/10000           │
├──────────────────────────────────────────┤
│  06-16 10:23 I  ActivityManager: Start.. │ ← 绿色(I)
│  06-16 10:24 W  dalvikvm: GC overhead.. │ ← 橙色(W)
│  06-16 10:25 E  CRASH: Fatal exception.. │ ← 红色(E)
│  ...                                      │
└──────────────────────────────────────────┘
```

**SplitContainer**：outer(设备↔内容, 200px/150min) + inner(列表↔预览, 400px/250min)，分割位置持久化。

---

## 三、性能与内存优化策略

| 优化点 | 策略 | 效果 |
|--------|------|------|
| 日志存储 | `RingBuffer<T>` 环形缓冲区，O(1) | 避免List.RemoveAt(0)的O(n)，内存恒定 |
| ListView渲染 | `VirtualMode=true` 按需创建 | 只渲染可见行，百万条无卡顿 |
| TCP接收缓冲 | `ArrayPool<byte>.Shared` 池化 | 减少大数组GC压力 |
| JSON解析 | `System.Text.Json` UTF-8直解析 | 比Newtonsoft快3-5x，内存少50% |
| Logcat读取 | StreamReader逐行流式 | 仅当前行+解析对象 |
| 大响应体 | Android端截断超50KB | 避免传输存储巨大JSON |
| Lazy JSON | 仅点击时解析渲染 | 避免后台持续解析 |
| UI更新 | BeginUpdate/EndUpdate批量 | 避免每条触发重绘 |
| 窗口隐藏 | 检测最小化降频 | 最小化时CPU接近零 |
| 连接空闲 | 阻塞读取无轮询 | 无数据时零CPU |
| 线程模型 | 2N+1线程 | 线程数=2×设备数+1 |

**内存预算(2设备)**：网络日志~20MB + 系统日志~10MB + 其他~80KB ≈ **~30MB**

---

## 四、智能自动滚动

- 在底部 → 自动跟随
- 不在底部 → 暂停，不打断
- 用户滚回底部 → 自动恢复
- `⬇滚动到底部`按钮 + `End`快捷键
- 状态指示：条数/上限(默认/橙色80%/红色100%/暂停滚动)

---

## 五、通信协议

```
消息格式: [4字节大端int=总长度][1字节消息类型][UTF-8 JSON字节]
0x01 = 设备注册, 0x02 = 网络日志数据

Android: 连接localhost:9527 → 0x01注册 → 0x02日志 → 断开重连
C#: TcpListener监听 → Accept → 0x01解析DeviceInfo → 0x02解析LogEntry

系统日志(独立通道):
C#端启动 adb -s {serial} logcat -v threadtime {filter}
逐行解析为SystemLogEntry

大端序(C#): if LittleEndian → Array.Reverse
```

---

## 六、功能完整清单

1-7: 服务管理/设备面板/切换/全部/ADB/断开重连/删除
8-13: 系统日志Logcat/Tab/筛选/着色/自动启动/过滤配置
14-22: 请求列表/颜色/详情/JSON折叠/语法高亮/搜索/复制/展开折叠/筛选
23-24: 智能滚动/⬇按钮
25-27: 条数上限/清空/导出
28-31: 设置/SplitContainer/请求头/字体
32-36: RingBuffer/VirtualMode/ArrayPool/截断/窗口隐藏降频

---

## 七、使用流程

1. USB连接多台手机（USB调试）
2. 启动服务 → ADB Reverse全部设备
3. 网络日志自动传输，系统日志自动logcat采集
4. Tab切换网络请求/系统日志
5. 设备切换或"全部"合并查看

---

## 八、改动汇总

| 新建 | `debug/NetworkLogSender.java` | ~160行 |
| 新建 | C# 客户端整个工程 | ~1100行 |
| 加1行 | `OkHttp3Utils.java:241` | send调用 |
| 加1行 | `MyApplication.java:onCreate末尾` | start调用 |
| 加2行 | `AppConstant.java` | QUEUE_SIZE + MAX_BODY_SIZE |
| 加2个import | OkHttp3Utils + MyApplication | 各1行 |

**对现有代码的影响**：仅增加4行代码和2行import，不修改任何现有逻辑。