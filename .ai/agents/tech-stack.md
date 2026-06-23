# LogViewer 技术栈说明

本文档供 AI 助手阅读。

## 1. 项目定位

- **项目名**：`LogViewer`
- **项目类型**：Android 网络日志实时查看器
- **用途**：通过 USB 连接多台 Android 设备，在 PC 端实时查看网络请求日志和系统日志（Logcat）
- **应用类型**：Windows 桌面程序
- **UI 技术**：WinForms

## 2. 核心技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 语言 | C# | 12 |
| 项目格式 | SDK 风格 `.csproj` | - |
| 目标框架 | .NET | 8.0 (net8.0-windows) |
| 输出类型 | WinExe | - |
| UI 框架 | System.Windows.Forms | .NET 8 内置 |
| 可空引用 | Nullable | enable |
| 隐式全局 using | ImplicitUsings | enable |
| JSON 序列化 | System.Text.Json | 内置 |
| 网络通信 | System.Net.Sockets | 内置（TCP Server） |
| 进程管理 | System.Diagnostics.Process | 内置（adb logcat） |
| 设置持久化 | Properties.Settings | WinForms 内置 |

## 3. 外部依赖

**无第三方 NuGet 包**。项目完全依赖 .NET 8 内置库。  
运行时接入 `adb.exe` 与 `scrcpy.exe` 作为外部工具，统一从程序目录读取并随项目输出复制，不通过 NuGet 分发。

## 4. 通信协议

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

### JSON 大小写（已踩坑）

Android 端 **Gson** 输出**小写驼峰**（`isRedirect`, `sendTime`），C# 端 `System.Text.Json` 默认**区分大小写**。

**必须设置 `PropertyNameCaseInsensitive = true`**，否则所有字段反序列化为默认值。

### 系统日志（独立通道）

C# 端直接启动 `adb logcat` 进程，不走 TCP：

```
adb -s {serial} logcat -v threadtime {filter}
```

### 架构选择

| 决策 | 选择 | 原因 |
|------|------|------|
| C# 做 TCP Server | 是 | 支持多设备同时连接 |
| `adb reverse` vs `adb forward` | `adb reverse` | 多设备只需同一个端口映射命令 |
| 系统日志走 TCP 还是 adb | adb logcat 进程 | 零手机端开销，不走手机 TCP 通道 |

## 5. 核心模块

| 名称 | 路径 | 说明 |
|------|------|------|
| LogEntry | `Models/LogEntry.cs` | 网络日志 13 字段模型 + 预览属性 |
| SystemLogEntry | `Models/SystemLogEntry.cs` | 系统日志模型 + Level→Color 着色 |
| DeviceInfo | `Models/DeviceInfo.cs` | 设备注册信息模型 |
| AppSettings | `Models/AppSettings.cs` | 设置读写 + Properties.Settings 持久化 |
| RingBuffer | `Models/RingBuffer.cs` | O(1) 环形缓冲区（非线程安全，UI 线程专用） |
| LogServer | `Network/LogServer.cs` | TCP Server，AcceptLoop 多设备，事件广播 |
| DeviceConnection | `Network/DeviceConnection.cs` | 单设备连接 + 协议帧解析 + ArrayPool |
| LogcatReader | `Network/LogcatReader.cs` | adb logcat 进程流式读取 + 正则解析 |
| MainForm | `UI/MainForm.cs` | 主窗口（日志列表/详情/设备/设置） |
| JsonDetailForm | `UI/JsonDetailForm.cs` | JSON 详情窗口（左右 4:6 分栏 + Tree/Raw） |
| JsonTreeView | `UI/JsonTreeView.cs` | JSON 折叠 + 语法高亮 TreeView（OwnerDrawText） |
| DevicePanel | `UI/DevicePanel.cs` | 左侧 ADB 设备操控面板（设备选择 + scrcpy 宿主 + 控制条） |
| SettingsDialog | `UI/SettingsDialog.cs` | 设置对话框（ADB 路径检测 + scrcpy 自动部署/高级覆盖） |
| Language | `Static/Language.cs` | UI 字符串常量（菜单/状态/按钮/错误提示） |
| BufferedListView | `UI/BufferedListView.cs` | ListView 双缓冲/精确顶部索引/滚动恢复辅助 |
| ClipboardTextHelper | `UI/ClipboardTextHelper.cs` | 剪贴板安全写入辅助 |
| JsonTreeViewLoader | `UI/JsonTreeViewLoader.cs` | JSON→TreeNode 构建扩展方法 |
| SystemLogSnapshot | `UI/SystemLogSnapshot.cs` | System Logs 当前 scope/filter 只读快照 |
| SystemLogSessionStore | `Utils/SystemLogSessionStore.cs` | System Logs 会话级 jsonl 追加存储+索引+热缓存 |
| AdbHelper | `Utils/AdbHelper.cs` | ADB 搜索/验证/设备列表/Reverse |
| ScrcpyManager | `Utils/ScrcpyManager.cs` | scrcpy 搜索/启动/内嵌宿主/窗口生命周期管理 |
| JsonFormatter | `Utils/JsonFormatter.cs` | JSON 格式化 + JSONPath |

## 6. 测试

| 项 | 值 |
|----|-----|
| 框架 | 暂无（项目未引入测试框架） |
| 说明 | 当前项目无独立测试项目，后续如需测试可引入 xUnit |

## 7. 代码结构

完整目录树见 [directory-tree.md](directory-tree.md)。

核心文件：

- `LogViewer/LogViewer.csproj`：项目文件
- `LogViewer/Program.cs`：程序入口
- `LogViewer/UI/MainForm.cs`：主窗体逻辑
- `LogViewer/Network/DeviceConnection.cs`：协议解析核心

## 8. 启动方式

```
Program.cs → ApplicationConfiguration.Initialize() → Application.Run(new MainForm())
```

1. 启动后点击 "Start Server" 启动 TCP Server
2. 点击 "ADB Reverse" 执行端口映射
3. 手机 App 网络请求 → 日志实时出现在客户端

## 9. 设置项

| 设置项 | 默认值 | 说明 |
|--------|--------|------|
| Server Port | 9527 | TCP Server 监听端口 |
| Max Logs Per Device | 5000 | 每台设备网络日志上限 |
| Max Logs All Devices | 10000 | 合并视图日志上限 |
| Max System Logs | 10000 | 每台设备系统日志上限 |
| Auto ADB Reverse | true | 启动服务时自动执行 Reverse |
| Auto Start Logcat | true | 设备连接时自动启动 logcat |
| Auto Format JSON | true | 日志详情默认折叠显示 JSON |
| Font Size (pt) | 11 | 列表和详情字体大小 |
| Logcat Filter | (空) | adb logcat 过滤表达式 |
| Auto Start Scrcpy For Selected Device | false | 选择具体设备时自动启动左侧镜像 |
| Last Left Panel Width | 340 | 左侧设备操控区上次宽度 |

## 10. 架构分层

```
UI/ ──→ Network/ ──→ Models/
 │          │
 ├──→ Utils/ ──→ Models/
 └──→ Static/（零依赖）
```

| 层 | 目录 | 职责 | 依赖 |
|----|------|------|------|
| 数据模型 | `Models/` | LogEntry/SystemLogEntry/DeviceInfo/AppSettings/RingBuffer | 无 |
| 通信层 | `Network/` | TCP Server + 协议解析 + adb logcat | → Models |
| 界面层 | `UI/` | 主窗口 + JSON 详情 + 设备面板 + 设置 | → Network, Utils, Static, Models |
| 工具层 | `Utils/` | ADB 操作 + scrcpy 宿主 + JSON 格式化 + 会话存储 | → Models |
| 静态资源 | `Static/` | UI 字符串常量 | 无 |
| 运行时工具 | `Runtime/WindowsTools/` | adb.exe + scrcpy.exe（CopyToOutputDirectory） | 无（非代码） |

## 11. 给 AI 的工作约束

- 保持 WinForms 项目习惯
- 设计器生成代码只放在 `*.Designer.cs`
- 手写逻辑放非 Designer partial 类
- 不引入 WPF/MAUI/Avalonia/ASP.NET/第三方 UI 框架
- 不引入第三方 NuGet 包（当前项目零外部依赖）
- 新增数据模型放 `Models/`，通信代码放 `Network/`，界面放 `UI/`，工具放 `Utils/`
- 命名空间 = `LogViewer.` + 目录路径
- 依赖方向不可逆：UI → Network → Models；UI → Utils → Models
- JSON 反序列化必须 `PropertyNameCaseInsensitive = true`
- 协议长度字段必须处理大端序
- ListView 必须用 VirtualMode
- RingBuffer 非线程安全，UI 更新必须 BeginInvoke
- scrcpy 仅作为外部可执行工具接入，不引入第三方 NuGet 包，首次启动自动部署官方 Windows 包
- 左侧设备区现为 ADB 手机操控区，默认内嵌 scrcpy，设备选择与日志 scope 共用一个选择器

## 12. 一句话摘要

`LogViewer` 是一个 `.NET 8 + WinForms` C# 桌面应用：零第三方依赖，通过 TCP Server 接收 Android 端网络日志，通过 adb logcat 进程获取系统日志，VirtualMode ListView + RingBuffer 支持百万级日志流畅展示，支持多设备、JSON 详情窗口和 ADB 端口映射管理。
