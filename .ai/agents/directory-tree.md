# 项目目录树

本文档供 AI 助手阅读。记录 LogViewer 项目的源码目录结构（排除 bin/obj）。

> **维护规则**：每次新增或删除源码文件/目录后，必须同步更新本文档并保持中文注释。

---

## 目录归属规则

| 类型 | 放置位置 | 命名空间 | 依赖方向 |
|------|---------|---------|---------|
| 数据模型 | `Models/` | `LogViewer.Models` | 零UI依赖，被所有层引用 |
| 通信层 | `Network/` | `LogViewer.Network` | → Models，零UI依赖 |
| 界面层 | `UI/` | `LogViewer.UI` | → Network, Utils, Static, Models |
| 工具类 | `Utils/` | `LogViewer.Utils` | → Models，不引用 UI/Network |
| 静态资源 | `Static/` | `LogViewer.Static` | 零依赖，被 UI 引用 |
| 运行时工具 | `Runtime/WindowsTools/` | 无（非代码） | adb/scrcpy 可执行文件，CopyToOutputDirectory |

**依赖方向不可逆**：UI → Network → Models；UI → Utils → Models；UI → Static。禁止反向引用。

---

```
LogViewer/                            ← 项目根目录
│
├── .ai/                                    ← AI 辅助文档
│   ├── agents/
│   │   ├── component-guide.md              ← 组件创建/更新指南
│   │   ├── dev-workflow.md                 ← 开发工作流
│   │   ├── directory-tree.md               ← 本文件：项目目录树
│   │   ├── MEMORY.md                       ← 项目记忆
│   │   ├── PROFILE.md                      ← 用户画像
│   │   ├── REVIEW.md                       ← 代码审查记录
│   │   └── tech-stack.md                   ← 技术栈说明
│   ├── plans/                              ← 开发计划
│   ├── reviews/                            ← 代码审查
│   ├── scripts/                            ← 辅助脚本
│   └── skills/                             ← AI 技能定义
│       ├── core/                           ← .NET 命名/编码/WinForms规范
│       └── domain/                         ← 领域扩展技能
│
├── Models/                                 ← 数据模型层（5 .cs，零UI依赖）
│   ├── LogEntry.cs                         ← 网络日志13字段模型+预览属性（Url/Method/Code等）
│   ├── SystemLogEntry.cs                   ← 系统日志模型+Level着色+SequenceId/SourceDeviceId
│   ├── DeviceInfo.cs                       ← 设备注册信息模型（deviceId/deviceModel/androidVersion等）
│   ├── AppSettings.cs                      ← 设置模型+Properties.Settings持久化（ADB/scrcpy/左栏宽度）
│   └── RingBuffer.cs                       ← O(1)高性能环形缓冲区（非线程安全，UI线程专用）
│
├── Network/                                ← 通信层（3 .cs，零UI依赖）
│   ├── LogServer.cs                        ← TCP Server，AcceptLoop多设备，事件广播
│   ├── DeviceConnection.cs                 ← 单设备连接+协议解析+ArrayPool+消息类型switch
│   └── LogcatReader.cs                     ← adb logcat进程流式读取+正则解析threadtime格式
│
├── UI/                                     ← 界面层（12 .cs + 1 .resx）
│   ├── MainForm.cs                         ← 主窗口手写逻辑（事件/网络日志/设备/scrcpy宿主/设置）
│   ├── MainForm.SystemLogs.cs              ← System Logs 快照/过滤/Pause/Resume 运行时逻辑
│   ├── MainForm.Designer.cs                ← 主窗口设计器控件树（支持设计器拖动）
│   ├── BufferedListView.cs                 ← ListView 双缓冲/精确顶部索引/滚动恢复辅助
│   ├── ClipboardTextHelper.cs              ← 剪贴板安全写入辅助（统一规避 null/empty 复制崩溃）
│   ├── JsonDetailForm.cs                   ← JSON详情窗口手写逻辑（加载/切换/搜索）
│   ├── JsonDetailForm.Designer.cs          ← JSON详情窗口设计器控件树（左右分栏+工具栏）
│   ├── JsonTreeView.cs                     ← JSON折叠+语法高亮TreeView（OwnerDrawText自绘+渲染/交互）
│   ├── JsonTreeViewLoader.cs               ← JSON→TreeNode构建扩展方法（LoadJson/LoadPlainText/Search/Collapse）
│   ├── DevicePanel.cs                      ← 左侧ADB设备操控面板（设备选择+scrcpy宿主+控制条）
│   ├── SystemLogSnapshot.cs                ← System Logs 当前 scope/filter 只读快照
│   ├── SettingsDialog.cs                   ← 设置对话框（ADB路径检测 + scrcpy自动部署/高级覆盖）
│   └── MainForm.resx                       ← 主窗口资源文件
│
├── Static/                                  ← 静态资源层（1 .cs，零依赖）
│   └── Language.cs                          ← UI字符串常量（菜单/状态/按钮/错误提示等）
│
├── Runtime/                                 ← 运行时外部工具（非代码，CopyToOutputDirectory）
│   └── WindowsTools/                        ← adb.exe + scrcpy.exe + 依赖DLL + 资源图片
│
├── Utils/                                  ← 工具类（4 .cs）
│   ├── AdbHelper.cs                        ← ADB搜索/验证/设备列表/Reverse
│   ├── JsonFormatter.cs                    ← JSON格式化+JSONPath
│   ├── ScrcpyManager.cs                    ← scrcpy搜索/启动/内嵌宿主/窗口生命周期管理
│   └── SystemLogSessionStore.cs            ← System Logs 会话级 jsonl 追加存储+索引+热缓存
│
├── Properties/                             ← 设置资源
│   ├── Settings.settings                   ← 用户设置schema
│   └── Settings.Designer.cs                ← 类型化Settings访问类
│
├── Program.cs                              ← 程序入口
└── LogViewer.csproj                 ← .NET 8 WinForms 项目文件
```

## 命名空间规则

| 目录 | 命名空间 | 说明 |
|------|---------|------|
| `Models/` | `LogViewer.Models` | 数据模型，零UI依赖 |
| `Network/` | `LogViewer.Network` | 通信层，零UI依赖 |
| `UI/` | `LogViewer.UI` | 界面层 |
| `Utils/` | `LogViewer.Utils` | 工具类 |
| `Static/` | `LogViewer.Static` | 静态资源，UI字符串常量 |
| `Runtime/WindowsTools/` | 无（非代码） | 运行时外部工具 |
| `Properties/` | `LogViewer.Properties` | 设置资源（自动生成） |
| 项目根 | `LogViewer` | 入口Program |

**命名空间 = `LogViewer.` + 目录路径（斜杠换点）**

## 依赖方向图

```
UI ──► Network ──► Models
 │        │
 ├──► Utils ──► Models
 └──► Static（零依赖）
```

**禁止**：Network → UI，Utils → UI，Models → 任何层。
