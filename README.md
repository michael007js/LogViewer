# LogViewer

Android 网络日志实时查看器。通过 USB 连接多台 Android 设备，在 PC 端实时查看网络请求日志和系统日志（Logcat）。

## 架构概览

```
┌─────────────────┐  adb reverse   ┌──────────────────┐
│  Android App    │  tcp:9527      │  C# WinForms     │
│  (TCP Client)   │ ──────────────►│  (TCP Server)    │
│  NetworkLog     │                │  LogServer       │
│  Sender         │                │  DeviceConnection│
└─────────────────┘                │                  │
                                   │  LogcatReader    │
┌─────────────────┐  adb logcat    │  (adb process)   │
│  Android Device │ ──────────────►│                  │
│  (system logs)  │  stdout        │  MainForm        │
└─────────────────┘                │  JsonTreeView    │
                                   └──────────────────┘
```

**关键设计决策**：
- C# 做 TCP **Server**，Android 做 TCP **Client**（支持多设备）
- 使用 `adb reverse` 而非 `adb forward`（多设备只需同一个端口映射命令）
- 系统日志通过 C# 端直接启动 `adb logcat` 进程获取，不走手机 TCP 通道（零手机端开销）

## 项目结构

### Android 端

修改位于 `youzy.mobile.android` 项目：

```
app/src/main/java/com/eagersoft/youzy/youzy/
├── data/retrofit/
│   └── OkHttp3Utils.java         ← 改动：构建NetworkLogData.Builder并send()
├── application/
│   └── MyApplication.java        ← 改动：init(NetworkLogSenderConfig)+start()
└── constants/
    └── AppConstant.java          ← 改动：+2行常量

youdebug/src/main/java/com/eagersoft/youzy/youzy/debug/sender/
├── NetworkLogSender.java         ← TCP Client+发送逻辑（从app模块迁移至此）
├── NetworkLogSenderConfig.java   ← Builder模式配置类（解耦AppConstant）
├── NetworkLogData.java           ← Builder模式数据类（解耦NetworkResponseEntity）
└── readme.md                     ← sender模块说明
```

### C# 客户端

位于 `D:\youzyapp\youzy.android.log.client\LogViewer\`：

```
LogViewer/
├── Program.cs                          入口
├── LogViewer.csproj             .NET 8 WinForms
├── Models/
│   ├── LogEntry.cs                     网络日志13字段模型+预览属性
│   ├── SystemLogEntry.cs               系统日志模型+Level着色
│   ├── DeviceInfo.cs                   设备注册信息模型
│   ├── AppSettings.cs                  设置+Properties.Settings持久化
│   └── RingBuffer.cs                   O(1)高性能环形缓冲区
├── Network/
│   ├── LogServer.cs                    TCP Server，AcceptLoop多设备
│   ├── DeviceConnection.cs             单设备连接+协议解析+ArrayPool
│   └── LogcatReader.cs                 adb logcat进程流式读取+正则解析
├── UI/
│   ├── MainForm.cs                     主窗口全部逻辑
│   ├── JsonDetailForm.cs               双击弹出的JSON详情窗口（左右4:6分栏+Tree/Raw切换）
│   ├── JsonTreeView.cs                 JSON折叠+语法高亮TreeView
│   ├── DevicePanel.cs                  设备列表+切换面板（空状态提示）
│   └── SettingsDialog.cs               设置对话框（含ADB路径Browse/AutoDetect）
├── Utils/
│   ├── AdbHelper.cs                    ADB搜索/验证/设备列表/Reverse
│   └── JsonFormatter.cs                JSON格式化+JSONPath
└── Properties/
    ├── Settings.settings               用户设置schema
    └── Settings.Designer.cs            类型化Settings访问类
```

## 通信协议

### 消息帧格式

```
[4字节大端int=payload长度][1字节消息类型][UTF-8 JSON字节]
```

- 长度字段 = `1(类型) + JSON字节数`
- 大端序：Java `ByteBuffer.putInt()` 默认大端；C# 端 `if (BitConverter.IsLittleEndian) Array.Reverse()`

### 消息类型

| 类型码 | 名称 | 方向 | 数据结构 |
|--------|------|------|----------|
| `0x01` | 设备注册 | Android→PC | `DeviceRegisterInfo` |
| `0x02` | 网络日志 | Android→PC | `LogData` |

### 设备注册消息 (0x01)

连接成功后 Android 端**立即**发送第一条注册消息：

```json
{
  "deviceId": "YouZyUtils.getRegistrationId()值",
  "deviceModel": "Xiaomi M2012K10C",
  "androidVersion": "12",
  "appVersion": "3.5.1",
  "isQa": false
}
```

### 网络日志消息 (0x02)

```json
{
  "send": "请求体字符串（GET时为null）",
  "message": "OK",
  "url": "https://api.example.com/v1/data",
  "code": 200,
  "isRedirect": false,
  "isSuccessful": true,
  "isHttps": true,
  "protocol": "http/1.1",
  "method": "POST",
  "headers": "请求头字符串",
  "content": "服务器返回内容（超50KB截断）",
  "sendTime": 1718500000000,
  "receiveTime": 1718500001234
}
```

### ⚠️ JSON大小写问题（重要）

Android 端使用 **Gson** 序列化，默认输出**小写驼峰**字段名（`isRedirect`, `sendTime`...）。

C# 端 `System.Text.Json` 默认**区分大小写**，属性是 PascalCase（`IsRedirect`, `SendTime`...）。

**必须在反序列化时设置 `PropertyNameCaseInsensitive = true`**：

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true
};
// 使用
JsonSerializer.Deserialize<LogEntry>(jsonStr, JsonOptions);
```

**如果不设置此选项，所有字段将反序列化为默认值（null/0/false）**——这是已踩过的坑。

### 系统日志（独立通道，不走TCP）

C# 端直接为每台设备启动一个 `adb logcat` 子进程：

```
adb -s {serial} logcat -v threadtime {filter}
```

输出格式（`-v threadtime`）：
```
06-16 10:23:45.123  1234  5678 I ActivityManager: Start proc com.example
```

解析正则：
```
^(?<date>\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d+)\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<level>[VDIWEF])\s+(?<tag>[^\s:]+)\s*:\s*(?<msg>.*)$
```

## Android 端改动详情

### NetworkLogSender.java

**位置**：`youdebug/src/main/java/com/eagersoft/youzy/youzy/debug/sender/NetworkLogSender.java`

（从 `app/debug/` 迁移至 `youdebug/sender/`，解除了对 app 模块的循环依赖）

**线程模型**：
- `connectThread`（守护线程）：循环尝试连接 `config.host:config.port`，连接成功后发送注册消息，然后 sleep 等待
- `sendThread`（守护线程）：从 `LinkedBlockingQueue<byte[]>` 阻塞取数据写入 Socket

**连接管理**：
- 连接断开 → `currentOutputStream` 置 null → `connectThread` 检测到后重试
- `sendThread` 写入失败 → 关闭流 → `sendQueue.clear()` 清空积压
- 重连后自动重新发送注册消息

**数据截断**：
- `content` 和 `send` 字段超过 `NETWORK_LOG_MAX_BODY_SIZE`（50KB）时截断
- 截断后末尾追加 `...[truncated, original: XXX KB]`

**队列满策略**：
- `sendQueue.offer(data)` 失败时 → `poll()` 弹出最旧 → 再 `offer()`
- 保证最新数据优先

### OkHttp3Utils.java 改动

在 `response()` 方法中，白名单URL匹配后发送日志：

```java
// 新增import
import com.eagersoft.youzy.youzy.debug.sender.NetworkLogData;
import com.eagersoft.youzy.youzy.debug.sender.NetworkLogSender;

// 替换原NetworkLogSender.getInstance().send(networkResponseInfo)
NetworkLogSender.getInstance().send(new NetworkLogData.Builder()
    .send(networkResponseInfo.getSend())
    .message(networkResponseInfo.getMessage())
    .url(networkResponseInfo.getUrl())
    // ... 所有13个字段
    .build());
```

**位置关键**：必须放在 `print()` 之后、`return` 之前，确保 `networkResponseInfo` 所有字段已填充完毕。

### MyApplication.java 改动

在 `onCreate()` 方法末尾：

```java
import com.eagersoft.youzy.youzy.debug.sender.NetworkLogSender;
import com.eagersoft.youzy.youzy.debug.sender.NetworkLogSenderConfig;
import com.eagersoft.youzy.youzy.constants.AppConstant;

// onCreate末尾（替代原直接start调用）
NetworkLogSender.getInstance().init(new NetworkLogSenderConfig.Builder()
    .isDebug(AppConstant.IS_DEBUG)
    .host("localhost")
    .port(9527)
    .deviceId(YouZyUtils.getRegistrationId())
    .deviceModel(Build.MODEL)
    .androidVersion(Build.VERSION.RELEASE)
    .appVersion(AppUtil.getVersionName(this))
    .isQa(AppConstant.IS_QA)
    .queueSize(AppConstant.NETWORK_LOG_SENDER_QUEUE_SIZE)
    .build());
NetworkLogSender.getInstance().start();
```

### AppConstant.java 改动

```java
public static final int NETWORK_LOG_SENDER_QUEUE_SIZE = 1000;
public static final int NETWORK_LOG_MAX_BODY_SIZE = 50 * 1024; // 50KB
```

## C# 客户端关键实现

### RingBuffer\<T\> — 高性能环形缓冲区

替代 `List<T>` + `RemoveAt(0)` 的 O(n) 开销：

```csharp
// O(1) Add：写入_head位置，满时覆盖_tail位置
// O(1) Get：通过 (_tail + index) % Length 定位
// 达到上限自动淘汰最旧数据
```

用于所有日志存储：`_deviceLogs`, `_allLogs`, `_deviceSystemLogs`。

### VirtualMode ListView

网络日志和系统日志列表均使用 `VirtualMode = true`：

```csharp
_lstNetworkLogs.VirtualMode = true;
_lstNetworkLogs.RetrieveVirtualItem += OnRetrieveNetworkItem;
// 更新数量：_lstNetworkLogs.VirtualListSize = buf.Count;
```

**只渲染可见行**，百万条数据无卡顿。`RetrieveVirtualItem` 事件中从 `RingBuffer` 按需读取。

### 智能自动滚动

```csharp
private bool IsAtBottom(ListView lv) {
    if (lv.VirtualListSize == 0) return true;
    var lastItemIdx = lv.VirtualListSize - 1;
    var lastRect = lv.GetItemRect(lastItemIdx);
    return lastRect.Bottom <= lv.ClientRectangle.Bottom;
}

// 新日志到来时
bool wasAtBottom = IsAtBottom(_lstNetworkLogs);
_lstNetworkLogs.VirtualListSize = buf.Count;
if (wasAtBottom) ScrollToBottom(_lstNetworkLogs);
```

- 在底部 → 自动跟随
- 不在底部 → 不打断，⬇按钮高亮提醒
- 快捷键 `End` 也可跳到底部

### JsonTreeView — JSON折叠+语法高亮

继承 `TreeView`，`DrawMode = OwnerDrawText`：

- Object/Array 节点折叠显示 `{ N items }` / `[ N items ]`
- 展开时递归显示子节点
- 着色：key蓝/string绿/number橙/bool紫/null灰/摘要灰斜体
- 右键菜单：复制值/复制JSONPath/复制节点/展开折叠控制
- 搜索高亮：匹配节点黄色背景，自动展开父节点

### JsonDetailForm — 双击弹出的JSON详情窗口

双击网络日志列表条目，打开独立非模态窗口：

- **标题栏**：`{Method} {UrlPath} {Code} {Duration}ms`
- **布局**：左右 SplitContainer（40%:60%，可拖动调整）
  - 左侧：Request Body
  - 右侧：Response Body
- **每侧包含**：
  - 工具栏：标题 + Tree/Raw切换按钮 + Expand/Collapse/Lvl2 + Search
  - JsonTreeView（Tree模式可见）
  - TextBox（Raw模式可见，Multiline+ReadOnly+ScrollBars，显示格式化JSON原文）
- **切换逻辑**：按钮点击 → 互斥切换 Tree/Raw Visible → Tree模式时禁用Expand/Collapse/Search按钮
- **快捷键**：Ctrl+W 关闭窗口
- 可同时打开多个窗口，每次双击新开

### 主窗口右侧详情面板 — Tree/Raw切换

主窗口内嵌的 Headers/Request Body/Response Body 三个Tab也支持 Tree/Raw 切换：

- 每个Tab内叠放 JsonTreeView + TextBox(Raw)
- 工具栏增加"Raw↔Tree"切换按钮
- 切换时所有Tab同步切换显示模式
- 切换Tab时通过 `SyncDetailViewVisibility()` 保持一致
- Raw模式 TextBox 内容通过 `JsonFormatter.FormatJson()` 格式化

### ADB 路径管理

搜索策略（`AdbHelper.GetSearchPaths()`）：
1. PATH 环境变量中的 `adb.exe`
2. `Program Files\Android\android-sdk\platform-tools\adb.exe`
3. `LocalApplicationData\Android\Sdk\platform-tools\adb.exe`
4. 注册表 `SOFTWARE\Android Studio` 的 `SdkPath`
5. 所有盘符根目录下 `SDK\platform-tools\adb.exe`

手动设置：
- Settings 对话框提供 Browse... 和 Auto Detect 按钮
- 路径持久化在 `Properties.Settings.Default.AdbPath`
- 启动时若自动检测失败，弹窗提示手动设置

### 多设备管理

- `LogServer` 通过 `AcceptTcpClientAsync` 循环接受连接
- 每个连接创建 `DeviceConnection` 实例
- 收到 `0x01` 注册消息后按 `deviceId` 索引
- 同一 `deviceId` 重连时替换旧连接
- 设备面板显示所有设备，点击切换查看
- "全部"视图合并所有设备日志（按接收顺序）

### Logcat 系统日志

- 每台设备启动独立的 `adb logcat` 进程（`LogcatReader`）
- 流式逐行读取，不缓冲整个 stdout
- 正则解析 `-v threadtime` 格式
- Level着色：V灰/D蓝/I绿/W橙/E红/F深红
- 进程退出时自动清理

## 使用流程

```
1. USB连接Android手机（需开启USB调试）
2. 运行C#客户端
3. 首次运行若ADB未检测到 → 弹窗提示 → 设置ADB路径
4. 点击"Start Server"
5. 点击"ADB Reverse" → 选择设备或"Reverse All"
6. 手机App网络请求 → 日志实时出现在客户端
7. 系统日志自动通过 adb logcat 采集
8. 切换 Network Logs / System Logs Tab
9. 点击设备切换，或选"All"合并查看
10. 双击日志条目 → 打开JSON详情窗口（左右4:6分栏，可切换Tree/Raw）
11. 右侧详情面板也可切换 Tree/Raw 模式查看
```

**ADB Reverse 命令**：
```bash
# 单设备
adb -s {serial} reverse tcp:9527 tcp:9527

# 查看已映射
adb -s {serial} reverse --list

# 移除映射
adb -s {serial} reverse --remove tcp:9527
```

## 构建与运行

### C# 客户端

**前提**：.NET 8 SDK

```bash
# 构建
dotnet build D:\youzyapp\youzy.android.log.client\LogViewer\LogViewer.csproj

# 运行
dotnet run --project D:\youzyapp\youzy.android.log.client\LogViewer
```

### Android 端

Android 端改动已直接写入 `youzy.mobile.android` 项目，正常编译安装即可。无需额外配置。

## 设置项说明

| 设置项 | 默认值 | 说明 | 生效方式 |
|--------|--------|------|----------|
| Server Port | 9527 | C# TCP Server监听端口 | 需重启服务 |
| Max Logs Per Device | 5000 | 每台设备网络日志上限 | 即时 |
| Max Logs All Devices | 10000 | 合并视图日志上限 | 即时 |
| Max System Logs | 10000 | 每台设备系统日志上限 | 即时 |
| Android Send Queue | 1000 | Android端发送队列大小 | 需改AppConstant重新编译App |
| Body Truncate (KB) | 50 | 超过此大小的响应体截断 | 需改AppConstant重新编译App |
| Auto ADB Reverse | true | 启动服务时自动执行Reverse | 下次启动 |
| Auto Start Logcat | true | 设备连接时自动启动logcat | 即时 |
| Auto Format JSON | true | 日志详情默认折叠显示JSON | 即时 |
| Font Size (pt) | 11 | 列表和详情字体大小 | 即时 |
| Logcat Filter | (空) | adb logcat过滤表达式 | 下次启动logcat |
| ADB Path | (空) | 手动指定adb.exe路径 | 即时 |

标记 * 的设置需修改 Android 端 `AppConstant.java` 并重新编译 App。

## 已知问题与注意事项

### 1. JSON大小写（已修复但需牢记）

Gson 输出小写驼峰，`System.Text.Json` 默认区分大小写。**所有反序列化必须使用 `PropertyNameCaseInsensitive = true`**。如果新增消息类型或模型，务必注意此问题。

### 2. 大端序

Java `ByteBuffer.putInt()` 默认大端序。C# `BitConverter` 在 x86/x64 上是小端序。读取长度字段时必须反转：

```csharp
if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
```

### 3. Socket 连接生命周期

Android 端 `connectLoop` 中使用 `Thread.sleep` 轮询检测连接状态。当 `currentOutputStream` 被 `sendLoop` 设为 null（发送失败）时，`connectLoop` 退出 sleep，关闭旧连接，重试。

**潜在问题**：如果 `connectLoop` sleep 期间连接断开但 `sendLoop` 未触发写入，断开不会被立即检测到。这是可接受的——下次 `sendLoop` 写入时会发现。

### 4. NetworkLogSender 不读取 response body

`NetworkLogSender.send()` 接收的 `NetworkResponseEntity` 已在 `OkHttp3Utils.response()` 中读取完毕（`response.body().string()` 已调用）。不存在 body 未读取的问题。

### 5. ListView VirtualMode 限制

VirtualMode 下不支持 `SelectedItems` 集合的某些操作。当前使用 `SelectedIndices[0]` 获取选中项索引，再从 `RingBuffer` 读取数据。**不要使用 `Items.Add()`**，必须通过 `VirtualListSize` + `RetrieveVirtualItem` 工作。

### 6. 线程安全

- `LogServer` 事件在 accept 线程触发，`MainForm` 通过 `BeginInvoke` 切回 UI 线程
- `LogcatReader` 事件在后台读取线程触发，同样 `BeginInvoke` 切回 UI 线程
- `RingBuffer` 不是线程安全的——所有读写都在 UI 线程（通过 `BeginInvoke` 确保）

### 7. LogcatReader 年份推断

`-v threadtime` 格式不含年份，`LogcatReader.ParseLine` 用当前年份，如果解析月份大于当前月份则减一年。跨年时可能有一小段日期不准确。

### 8. 设备ID与ADB Serial的关联

当前 `DeviceInfo.AdbSerial` 未自动填充（设备通过 TCP 连接时不知道 ADB serial）。ADB Reverse 和 Logcat 操作依赖 ADB devices 列表中的 serial，而非 TCP 连接的 deviceId。如果需要精确关联，需要用户手动匹配或增加匹配逻辑。

### 9. 窗口隐藏降频（待实现）

PLAN.md 中提到最小化时降低 UI 刷新频率，当前代码未实现。高频日志场景下最小化窗口可能仍有不必要的 UI 更新。

### 10. CS8618 警告

WinForms 控件字段在构造函数中通过 `InitializeComponent()` 赋值，但 C# 编译器无法追踪，产生大量 CS8618 "不可为null的字段必须包含非null值" 警告。可忽略，或在字段声明加 `= null!` 消除。

## 扩展指南

### 新增消息类型

1. Android端 `NetworkLogSender.java`：新增 `TYPE_XXX` 常量和内部数据类，在适当位置调用 `packMessage()`
2. C#端 `DeviceConnection.cs`：在 `switch (messageType)` 新增 case，定义对应 Model 类
3. C#端 `LogServer.cs`：新增事件，在 `OnXxx` 方法中触发
4. C#端 `MainForm.cs`：订阅事件，更新 UI

### 新增日志展示列

1. `LogEntry` / `SystemLogEntry` 增加属性
2. `MainForm.CreateNetworkLogTab()` 中 `Columns.Add()` 增加列
3. `OnRetrieveNetworkItem` 中 `item.SubItems.Add()` 填充值

### 修改端口

端口硬编码在 Android 端 `NetworkLogSender.PORT = 9527`。如需可配置：
1. Android端改为从 `AppConstant` 或 `BuildConfig` 读取
2. C#端已有设置项 `ServerPort`

## 依赖

### Android 端
- Gson（项目已有，`otherDependencies.converter_gson`）
- 无新增依赖

### C# 端
- .NET 8 SDK / Runtime
- System.Text.Json（内置）
- System.Net.Sockets（内置）
- System.Diagnostics.Process（内置）
- 无第三方 NuGet 包
