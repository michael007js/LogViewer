# android.log.client 项目记忆

> 本文档供 AI 助手阅读。记录项目开发注意事项、协议踩坑、技术决策和经验规则。

## 项目信息

- **项目名称**：LogViewer
- **项目类型**：.NET 8 WinForms 桌面应用
- **支持平台**：Windows
- **项目用途**：Android 网络日志实时查看器，通过 USB 连接多台设备，实时查看网络请求日志和系统日志

## 技术栈

> 完整技术栈详见 [tech-stack.md](tech-stack.md)。本节不再重复。

## 项目结构

> 完整目录树见 [directory-tree.md](directory-tree.md)。

## 关键文件位置

### 入口与配置
| 文件 | 路径 | 说明 |
|------|------|------|
| 程序入口 | `Program.cs` | Application.Run 启动 MainForm |
| 项目文件 | `LogViewer.csproj` | .NET 8 WinForms，无第三方 NuGet |
| 用户设置 Schema | `Properties/Settings.settings` | ServerPort/MaxLogs/ADB路径等设置定义 |
| 设置访问类 | `Properties/Settings.Designer.cs` | 自动生成，类型化 Settings.Default |

### 数据模型层 (Models/)
| 文件 | 说明 |
|------|------|
| `LogEntry.cs` | 网络日志13字段（url/method/code/send/content/headers/isRedirect/isSuccessful/isHttps/protocol/message/sendTime/receiveTime）+ 预览属性 |
| `SystemLogEntry.cs` | 系统日志（date/time/pid/tid/level/tag/msg）+ Level→Color 着色映射 |
| `DeviceInfo.cs` | 设备注册信息（deviceId/deviceModel/androidVersion/appVersion/isQa）+ AdbSerial |
| `AppSettings.cs` | 设置读写 + Properties.Settings.Default 持久化 |
| `RingBuffer.cs` | O(1) 环形缓冲区，非线程安全，UI 线程专用 |

### 通信层 (Network/)
| 文件 | 说明 |
|------|------|
| `LogServer.cs` | TCP Server，AcceptLoop 接受多设备，20s超时检测，REPLACE竞态防护 |
| `DeviceConnection.cs` | 单设备连接 + 协议帧解析 + ArrayPool + 异步Pong回复 + LastActiveTime |
| `LogcatReader.cs` | adb logcat 进程流式读取 + 正则解析 threadtime 格式 + LineReceived 事件 |

### 界面层 (UI/)
| 文件 | 说明 |
|------|------|
| `MainForm.cs` | 主窗口（网络日志/设备面板/详情面板/设置按钮） |
| `MainForm.SystemLogs.cs` | System Logs 的 snapshot/filter/pause + VirtualMode ListView 运行逻辑 |
| `JsonDetailForm.cs` | 双击弹出 JSON 详情窗口（左右 4:6 分栏 + Tree/Raw 切换） |
| `JsonTreeView.cs` | JSON 折叠 + 语法高亮 TreeView（OwnerDrawText 自绘） |
| `DevicePanel.cs` | 左侧 ADB 设备操控面板（设备选择 + scrcpy 宿主 + 控制条） |
| `SystemLogSnapshot.cs` | systemlogs 当前 scope/filter 快照，支持稳定 viewIndex/key |
| `BufferedListView.cs` | ListView 双缓冲/顶部锚点恢复辅助，供 networklogs/systemlogs 共用 |
| `SettingsDialog.cs` | 设置对话框（ADB 路径检测 + scrcpy 自动部署/高级覆盖） |

### 静态资源层 (Static/)
| 文件 | 说明 |
|------|------|
| `Language.cs` | UI 字符串常量（菜单/状态/按钮/错误提示），零依赖，被 UI 引用 |

### 工具类 (Utils/)
| 文件 | 说明 |
|------|------|
| `AdbHelper.cs` | ADB 搜索（5 路径策略）/ 验证 / 设备列表 / Reverse |
| `ScrcpyManager.cs` | scrcpy 搜索/启动/内嵌宿主/窗口生命周期管理 |
| `JsonFormatter.cs` | JSON 格式化 + JSONPath 查询 |
| `SystemLogSessionStore.cs` | systemlogs 会话存储，负责 SequenceId、scope 索引、jsonl 追加写入 |

---

## 通信协议踩坑（最高频，务必牢记）

### 1. JSON 大小写不匹配（已踩坑 ✅）

**问题**：Android 端 Gson 输出小写驼峰（`isRedirect`, `sendTime`），C# 端 `System.Text.Json` 默认区分大小写，属性是 PascalCase（`IsRedirect`, `SendTime`）。

**后果**：不设置 `PropertyNameCaseInsensitive = true` → 所有字段反序列化为默认值（null/0/false），数据完全丢失但不报错。

**修复**：
```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true
};
```

**规则**：**任何新增 JSON 反序列化代码都必须使用此选项**，不可遗漏。

### 2. 大端序长度字段（已踩坑 ✅）

**问题**：Java `ByteBuffer.putInt()` 默认大端序，C# `BitConverter` 在 x86/x64 是小端序。

**后果**：不反转字节 → 长度值错误 → 协议帧解析崩溃。

**修复**：
```csharp
if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
```

**规则**：**任何读取协议长度字段的代码都必须处理大端序**。

### 3. 消息帧格式

```
[4字节大端int=payload长度][1字节消息类型][UTF-8 JSON字节]
```

- 长度 = `1(类型) + JSON字节数`
- 新增消息类型须在 `DeviceConnection.cs` 的 `switch (messageType)` 中新增 case
- 新 case 必须放在 `default` 之前

---

## 技术决策记录

| 决策 | 选择 | 原因 |
|------|------|------|
| UI 框架 | WinForms | 轻量 Windows 桌面工具，不引入 WPF/MAUI/Avalonia |
| 通信架构 | C# TCP Server + Android TCP Client | 支持多设备，C# 做 Server 接受连接 |
| 端口映射 | `adb reverse` 而非 `adb forward` | 多设备只需同一个端口映射命令 |
| 系统日志获取 | C# 端启动 `adb logcat` 进程 | 不走手机 TCP 通道，零手机端开销 |
| 日志存储 | RingBuffer | O(1) 添加和索引，替代 List+RemoveAt(0) 的 O(n) |
| 列表展示 | VirtualMode ListView | 只渲染可见行，百万条数据无卡顿 |
| JSON 反序列化 | System.Text.Json + PropertyNameCaseInsensitive | 内置无依赖，大小写兼容 Gson |
| 配置持久化 | Properties.Settings.Default | WinForms 内置，无需第三方 |
| 设备标识 | deviceId（来自 YouZyUtils） | TCP 连接通过 deviceId 索引，非 ADB serial |

---

## 开发注意事项

### 目录归属与依赖

| 规范 | 禁止 |
|------|------|
| `Models/` 放数据模型 | 模型混入 UI/Network 层 |
| `Network/` 放通信层，零 UI 依赖 | Network 引用 UI 控件 |
| `UI/` 放界面层 | 控件散落在项目根目录 |
| `Utils/` 放工具类 | 工具类混入 Network/UI |
| `Static/` 放静态资源（字符串常量等） | Static 混入 UI/Utils |
| 命名空间 = `LogViewer.` + 目录路径 | 命名空间与目录不匹配 |
| 依赖方向：UI → Network → Models；UI → Utils → Models | Network → UI / Utils → UI / Models → 任何层 |

### 通信协议

| 规范 | 禁止 |
|------|------|
| JSON 反序列化 `PropertyNameCaseInsensitive = true` | 遗漏此选项导致字段全为默认值 |
| 长度字段 `if (BitConverter.IsLittleEndian) Array.Reverse()` | 直接 BitConverter.ToInt32 读大端序数据 |
| 新增消息类型在 `DeviceConnection.cs` switch 中加 case | 遗漏导致消息被丢弃 |
| 帧格式：4字节大端int长度 + 1字节类型 + UTF-8 JSON | 长度计算不包含类型字节 |

### UI / 控件

| 规范 | 禁止 |
|------|------|
| ListView VirtualMode + VirtualListSize + RetrieveVirtualItem | `Items.Add()` 导致百万条卡顿 |
| RingBuffer 读写必须在 UI 线程（BeginInvoke 保证） | 跨线程直接读写 RingBuffer |
| 网络/Logcat 事件 UI 更新用 `BeginInvoke` 切回 UI 线程 | 后台线程直接操作 UI 控件 |
| 智能滚动：先 `IsAtBottom`，底部才 `ScrollToBottom` | 始终强制滚动打断用户查看历史 |
| JsonTreeView `DrawMode = OwnerDrawText` + `OnDrawNode` 着色 | 非自绘模式丢失折叠/高亮 |

### 代码规范

| 规范 | 禁止 |
|------|------|
| 手写逻辑放非 Designer partial 类 | 写入 `.Designer.cs` |
| `switch` 新分支放在 `default` 之前 | 放在 default 之后永远不命中 |
| 非可视化数据模型用普通类 + public Dispose() | 继承 Component 导致设计器托管异常 |
| 终端命令加 `rtk` 前缀 | 不加导致输出未过滤 |

---

## 常用命令

> 所有命令必须加 `rtk` 前缀。

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj              # 构建项目
rtk dotnet run --project .\LogViewer                       # 运行项目
rtk git status                                              # Git 状态
rtk git diff                                                # Git 差异
```

---

## 项目经验

> 从开发实践中提炼的全局经验，后续开发必须遵守。

**目录先定 → 命名空间自动 → 依赖方向画图 → 协议合规确认 → 构建验证 → 文档同步**

| 规则 | 说明 |
|------|------|
| 目录归属前置判断 | 先定属于 `Models/`（数据模型）还是 `Network/`（通信层，零UI）还是 `UI/`（界面）还是 `Utils/`（工具），不要实施时再移 |
| 命名空间 = 目录路径 | `LogViewer.` + 目录路径（斜杠换点），目录确定后命名空间自动确定 |
| 依赖方向不可逆 | UI → Network → Models；UI → Utils → Models；禁止反向引用 |
| 协议改动先更新文档 | 修改消息帧格式/新增消息类型后，必须更新 AGENTS.md 通信协议节 + 本文档协议踩坑节 |
| RingBuffer 非线程安全 | 所有读写必须在 UI 线程，通过 BeginInvoke 保证；Network/Logcat 事件在后台线程触发 |
| VirtualMode 是唯一正确方式 | 网络日志/系统日志列表必须用 VirtualMode，禁止 Items.Add |
| 智能滚动先判位 | IsAtBottom 判断在 VirtualListSize 赋值之前，底部才跟随，非底部不打断 |
| WinForms Designer 优先 | 任何 UI 改动先保证 MainForm/子控件能在 Designer 正常打开，再谈运行时增强 |
| Rider WinForms 设计器宿主 | Rider 外部设计器实际跑在 `dotnet.exe + JetBrains.ReSharper.Features.WinForms.Designer.External.Core.exe`，不能只靠 `devenv/DesignToolsServer` 判断设计期 |
| Rider 设计器优先用标准控件 | 自定义泛型/运行时包装控件容易被 Rider 外部设计器误实例化；主页面日志列表优先保持标准 `ListView` + VirtualMode |
| System Logs 切 tab 禁止同步全量重建 | tab 切换只切最近 snapshot，过滤/补齐放后台线程，不能在 SelectedIndexChanged 里扫全量日志 |
| System Logs 当前会话不丢 | 热缓存上限只约束快速视图，不是最终保留上限；完整回看由 session store 承担 |
| System Logs Pause 只冻结视图 | Pause 后继续采集并累计 backlog，Resume 再按 SequenceId 追平，不阻塞 adb logcat 读取 |
| System Logs 渲染链使用 VirtualMode ListView | 运行时以 `SystemLogSnapshot + SystemLogSessionStore` 驱动 `ListView.VirtualMode`，设计器只看到标准控件 |
| JSON 大小写是第一检查项 | 凡涉及反序列化，第一反应检查 PropertyNameCaseInsensitive |
| 大端序是第二检查项 | 凡涉及协议长度字段读取，第二反应检查 Array.Reverse |
| DeviceInfo.AdbSerial 未自动填充 | TCP 连接时不知道 ADB serial，ADB Reverse 和 Logcat 依赖 ADB devices 列表 |
| LogcatReader 年份推断 | threadtime 格式不含年份，用当前年份，跨年可能有小段日期不准确 |
| LogEntry.content 截断 | Android 端超过 50KB 截断并追加 `[truncated, original: XXX KB]` |
| 非模态子窗口 ContextMenuStrip 吃掉主窗口右键消息 | JsonDetailForm.Show() 打开后，ToolStripManager.ModalMenuFilter 拦截 WM_CONTEXTMENU，导致主窗口其他控件右键菜单弹不出。修复：给需要右键的控件设置固定的 ContextMenuStrip，不用 MouseUp+new ContextMenuStrip |
| 新建 ADB 设备须自动 adb reverse | 拔出重插后，ADB 扫描检测到设备但 adb reverse 未执行 → Android 端无法连 TCP → 面板不更新。ApplyAdbDevices 对新建设备后台 Task.Run 执行 reverse |
| Ping/Pong 保活机制 | 客户端每 5s 发 Ping(0x03)，服务端回 Pong(0x04)；服务端超 20s 无消息断开连接触发重连；解决静默断连后设备列表不出现 |
| 重连风暴根因与修复 | connectLoop 内循环退出后无退避等待 → 立即建新 Socket → 同 deviceId REPLACE 杀旧连接 → readLoop EOF → handleClose 置空 → 内循环又退出 → 循环。修复：内循环退出后强制 sleep(RECONNECT_DELAY_MS) |
| handleClose vs closeCurrentStream | readLoop/sendLoop 调用 closeCurrentStream() 会无条件置空 currentSocket，误杀 connectLoop 已创建的新连接。handleClose(Socket) 通过 CAS 式检查 currentSocket==socket 只关闭旧连接。stop() 中仍可用 closeCurrentStream() |
| writeLock 必要性 | connectLoop 首次注册消息直写 OutputStream，sendLoop 写队列数据，两者并发写同一流会导致协议帧交错损坏。writeLock 保证串行写入 |
| Ping 走 sendQueue 不走直写 | Ping 通过 sendQueue 发送由 sendLoop 统一写出，避免 connectLoop/sendLoop/Ping 三路并发写入同一流增加锁竞争与帧交错风险 |
| 服务端 REPLACE 竞态防护 | 新连接 Registered 事件替换旧连接后，旧连接的 Disconnected 事件可能后到。OnDeviceDisconnected 通过引用比较 existing==conn 确保旧事件不会误删新连接 |
| DeviceId 为空时必须用稳定 key | 不能用 `unknown_{endpoint}`（含端口），端口每次重连都变 → 同一设备产生不同 key → DevicePanel 无限添加。改用 IP 地址部分（去端口） |
| SendPong 必须异步 | 同步 _stream.Write + Flush 在 TCP 发送缓冲区满时等 ACK，阻塞接收循环导致 UI 卡顿 3-4 秒。改为 await _stream.WriteAsync + FlushAsync
| EnsureServerStarted 必须后台执行 | 构造函数中同步调用 adb start-server（WaitForExit 5s）阻塞 UI 线程导致启动卡顿。改为 Task.Run 后台执行，完成后再 BeginInvoke 启动扫描循环
| ADB 扫描循环依赖 ADB Server 就绪 | EnsureServerStarted 改后台后，StartAdbScanLoop 不能立即启动，否则首次扫描时 ADB 未就绪 → 设备列表空 → 不启动 logcat → 系统日志列表为空。必须在 EnsureServerStarted 完成后再启动扫描循环
| Language 常量必须全部引用 | Language.cs 定义的常量/方法如果未在项目中引用，说明某处有硬编码字符串需要替换。所有 UI 文本必须走 Language 常量，包括右键菜单、列标题、按钮文本、状态栏、提示框
| 左侧设备区已升级为 scrcpy 宿主 | DevicePanel 不再只是设备下拉框；设备选择与日志 scope 共用一个选择器，选择 `All` 时左侧只显示占位提示，不启动镜像 |
| scrcpy 自动部署优先 | 保持项目零 NuGet 依赖；启动时自动从官方 GitHub Releases 部署到 LocalAppData，本地路径输入只作为高级覆盖/修复兜底 |
| 切换设备前先停旧 scrcpy | 内嵌镜像与当前设备强绑定；切换设备必须先停旧实例，再起新实例，避免窗口错绑和孤儿进程 |
| Normal Logs 复用 0x02 消息类型+type字段区分 | 不新增协议消息类型，通过 LogEntry.Type（type=1网络/type=2普通）在 DeviceConnection switch 0x02 内分发。旧版不发 type 字段时 Type=0 走 LogReceived（向后兼容）。type 扩展到 3~4 种尚可接受，超过 5 种考虑新增消息类型 |
| Normal Logs 独立 RingBuffer 双写 | `_deviceNormalLogs[id]` + `_allNormalLogs` 必须同时写入（和 Network Logs 双写模式一致）。OnDeleteDevice 不重建 `_allNormalLogs`（RingBuffer 追加写入无法安全删除中间条目） |
| Normal Logs 高频防抖必须和 Network 一致 | ScheduleNormalRefresh 使用 Task.Run + Task.Delay 防抖（80ms），不使用 Timer。对照 ScheduleNetworkRefresh |
| Normal Logs ForeColor 着色 | 使用 ForeColor（和 System Logs 一致），不用 BackColor 避免深浅色主题对比度问题 |
| Tab Form 内嵌架构 | 三个日志 Tab 已抽取为独立 Form（NetworkLogForm/NormalLogForm/SystemLogForm），各自拥有 Designer.cs。MainForm 通过 `TopLevel=false + FormBorderStyle=None + Dock=Fill` 嵌入 TabPage。Form 不引用 Network 层，事件通知 MainForm 协调 |
| TopLevel=false 内嵌注意事项 | 1) 内嵌 Form 的控件不参与父 Form 的 ProcessTabKey，Tab 键无法自动跳入；2) End 键通过 ProcessCmdKey 委托给活动 Form 的 HandleEndKey()；3) 内嵌 Form 的 Handle 未创建时 BeginInvoke 会失败，SystemLog 入队逻辑留在 MainForm |
| Form 生命周期管理 | 内嵌 Form 必须在 MainForm.OnFormClosing 中显式 Dispose，否则 GDI 对象泄漏。SystemLogForm 在自己 Dispose 时取消 CTS |
| ShowLogDetail 归属 | ShowLogDetail 操作预览面板控件，从 NetworkLogs.cs 移入 MainForm.Preview.cs。NetworkLogForm 通过 LogEntrySelected 事件通知 MainForm |
| 导出逻辑归属 | OnExportJson/OnExportTxt 留在 MainForm，因为需要协调 Network/Normal 两种缓冲区，通过 Form.GetCurrentLogBuffer()/GetCurrentNormalLogBuffer() 获取数据 |
| ScrollToBottom/IsAtBottom/ScrollToTop/GetApproxVisibleRowCount | 已提取为 BufferedListViewHelper 的 public static 方法，三个 Form 和 MainForm 共用 |

---

## 文档同步规则

- 新增或删除源码文件/目录后，更新 `directory-tree.md` 并保证中文注释
- 修改通信协议后，更新 `AGENTS.md` 通信协议节
- 修改技术栈/依赖后，更新 `tech-stack.md`
- 有踩坑经验可更新到本文件（`MEMORY.md`）
- `tech-stack.md` 是技术栈的唯一全文来源，其他文件只放指针
- 各文档遵循"单一来源"原则
