# AGENTS

本文档是给 AI 助手快速读取的项目执行上下文。目标是用最少的阅读成本，建立对 `LogViewer` 的稳定工作认知。

# 启动门控（不可跳过）

在执行任何用户任务之前，你必须完成以下步骤：

# 步骤1：读取4个上下文文件

依次读取以下文件，提取必要上下文：

| # | 文件 | 必须提取的关键信息 |
|---|------|-------------------|
| 1 | [.ai/agents/PROFILE.md](.ai/agents/PROFILE.md) | 用户称呼（老板）、交流语言（中文）、决策风格（给方案说利弊，由他定）、结尾招呼要求 |
| 2 | [.ai/agents/directory-tree.md](.ai/agents/directory-tree.md) | 文件放置规则（Models/ → 数据模型，Network/ → 通信层，UI/ → 界面层，Utils/ → 工具类） |
| 3 | [.ai/agents/tech-stack.md](.ai/agents/tech-stack.md) | 技术栈（.NET 8 + WinForms）、通信协议约束、外部依赖、构建命令、AI工作约束 |
| 4 | [.ai/agents/MEMORY.md](.ai/agents/MEMORY.md) | 开发注意事项（禁止项）、协议踩坑记录、项目经验规则、常用命令 |

⚠️ 读完4个文件后，必须逐条对照下方「强制记忆清单」，确认你真正理解了每条约束的含义和违反后果。不是走过场，是真正内化。

# 步骤2：按任务类型提取规则

执行任务时，根据任务类型从对应文件提取约束规则：

| 任务类型 | 必须遵守的文件规则 |
|----------|-------------------|
| UI / 控件 | directory-tree.md 的放置规则 + tech-stack.md 的主题系统 + MEMORY.md 的开发注意事项 |
| 网络 / 通信 | directory-tree.md 的 Network/ 规则 + MEMORY.md 的协议踩坑（大小写/大端序） |
| 数据模型 | directory-tree.md 的 Models/ 规则 + MEMORY.md 的 RingBuffer 线程安全 |
| ADB / 系统日志 | directory-tree.md 的 Utils/ 规则 + MEMORY.md 的 Logcat 注意事项 |
| 目录操作 | directory-tree.md 的完整结构 + MEMORY.md 的命名空间=目录路径规则 |
| 测试 | tech-stack.md 的测试框架/命令 + MEMORY.md 的测试规范 |
| 文档 | MEMORY.md 的文档同步规则 |

⚠️ 未完成步骤1直接执行任务，将因上下文缺失导致：文件放错目录、JSON反序列化字段全为默认值、大端序读取错误、VirtualMode ListView 误用。

---

# 强制记忆清单（红线 — 违反即错误）

以下是从 MEMORY.md 和项目经验中提炼的**绝对禁止项**。AI 必须逐条理解并遵守，不得以"忘记"、"上下文太长"为由绕过。

## A. 目录归属（放错 = 迁移 + 重构）

| # | 约束 | 违反后果 |
|---|------|---------|
| A1 | 数据模型放 `Models/`（LogEntry, SystemLogEntry, DeviceInfo, AppSettings, RingBuffer） | 模型混入 UI → 依赖方向错误，需迁移 |
| A2 | 通信层放 `Network/`（LogServer, DeviceConnection, LogcatReader），零 UI 依赖 | Network 混入 UI → 无法独立测试，需迁移 |
| A3 | 界面层放 `UI/`（MainForm, JsonDetailForm, JsonTreeView, DevicePanel, SettingsDialog） | 控件散落在项目根目录 → 归属错误 |
| A4 | 工具类放 `Utils/`（AdbHelper, JsonFormatter） | 工具类混入 Network/UI → 归属错误 |
| A5 | 命名空间 = 目录路径（`LogViewer.` + 目录路径斜杠换点） | 命名空间不匹配 → 构建失败 |
| A6 | 依赖方向不可逆：UI → Network → Models；UI → Utils → Models；禁止反向引用 | 反向依赖 → 循环依赖 |

## B. 通信协议（违反 = 数据丢失或反序列化失败）

| # | 约束 | 违反后果 |
|---|------|---------|
| B1 | **JSON 反序列化必须 `PropertyNameCaseInsensitive = true`** | Gson 输出小写驼峰，C# PascalCase 不匹配 → 所有字段为默认值（null/0/false） |
| B2 | **长度字段必须处理大端序**：`if (BitConverter.IsLittleEndian) Array.Reverse()` | x86/x64 小端序直接读 → 长度值错误 → 协议解析崩溃 |
| B3 | 新增消息类型须在 `DeviceConnection.cs` 的 `switch (messageType)` 中新增 case | 遗漏 → 消息被丢弃 |
| B4 | 消息帧格式：`[4字节大端int=长度][1字节类型][UTF-8 JSON]`，长度 = 1 + JSON字节数 | 长度计算错误 → 协议对齐崩溃 |

## C. UI / 控件（违反 = 性能问题或功能异常）

| # | 约束 | 违反后果 |
|---|------|---------|
| C1 | ListView 使用 **VirtualMode**，禁止 `Items.Add()`，必须用 `VirtualListSize` + `RetrieveVirtualItem` | 非虚拟模式 → 百万条数据卡顿 |
| C2 | RingBuffer **不是线程安全的**——所有读写必须在 UI 线程（通过 `BeginInvoke` 保证） | 跨线程读写 → 数据竞争 |
| C3 | 网络事件/Logcat 事件在后台线程触发，UI 更新必须 `BeginInvoke` 切回 UI 线程 | 直接操作 UI 控件 → 跨线程异常 |
| C4 | 智能自动滚动：先判断 `IsAtBottom`，底部才跟随，非底部不打断 | 始终强制滚动 → 用户查看历史被中断 |
| C5 | JsonTreeView 自绘控件使用 `DrawMode = OwnerDrawText`，颜色着色在 `OnDrawNode` | 非自绘模式 → 折叠/语法高亮失效 |

## D. 代码规范（违反 = 构建失败或设计器崩溃）

| # | 约束 | 违反后果 |
|---|------|---------|
| D1 | 手写逻辑放非 Designer partial 类，不写进 `*.Designer.cs` | Designer.cs 被污染 → 设计器重生成时丢失 |
| D2 | `switch` 新分支放 `default` 之前 | 放在 default 之后 → 永远不命中 |
| D3 | 非可视化数据模型用普通类 + public Dispose()，不继承 Component | 继承 Component → 设计器容器托管异常 |
| D4 | 终端命令必须加 `rtk` 前缀 | 不加 → 输出未被过滤，浪费 token |

## E. 交互与输出（违反 = 被老板判定为冗余错误）

| # | 约束 | 违反后果 |
|---|------|---------|
| E1 | 中文交流，简洁直接 | 英文或冗长 → 被判定为错误 |
| E2 | 给方案说利弊，由老板定决策 | 全盘执行不确认 → 做错方向 |
| E3 | 默认只输出结果，不解释 | 冗长解释 → 被判定为错误 |
| E4 | 代码只输出修改部分，不超过 50 行 | 返回完整文件 → 上下文爆炸 |
| E5 | 每次回复结尾打招呼 + 输出实时时间（精确到秒） | 不打招呼 → 老板认为记忆丢失 |

## F. 工作流程（违反 = 被老板判定为不负责任）

| # | 约束 | 违反后果 |
|---|------|---------|
| F1 | 改动后必须做构建验证 | 不验证 → 可能引入构建失败 |
| F2 | 默认最小正确改动，不主动重构大架构 | 过度重构 → 意外破坏 |
| F3 | 不主动引入 WPF/MAUI/Avalonia/ASP.NET/第三方 UI 框架 | 引入 → 与项目定位冲突 |
| F4 | 有不确定时先确认，不全盘执行 | 不确认 → 做错方向 |
| F5 | 有价值信息先记录再回答（MEMORY.md / PROFILE.md / 每日笔记） | 不记录 → 下次会话重复踩坑 |

## G. 文件写入（违反 = 写入失败或内容丢失）

| # | 约束 | 违反后果 |
|---|------|---------|
| G1 | 小文件（<200行）用 MCP 工具直接读写文本方式 | 用内置 write → 可能截断或失败 |
| G2 | 大文件（≥200行）、Plan 文件、生成代码用 MCP 工具直接读写文本方式 | 内置 write 写大文件 → 内容丢失 |
| G3 | 超大文件分段追加，每次 <200 行 | 一次性写入超大文件 → 内存溢出或失败 |
| G4 | 写入文件必须用 MCP 工具直接读写文本方式 | 单次 write/edit 写整文件 → 不可控 |

## H. 文档同步（违反 = 文档与代码不一致）

| # | 约束 | 违反后果 |
|---|------|---------|
| H1 | 新增/删除源码文件后更新 `directory-tree.md` + 中文注释 | 目录树过期 → AI 放错文件位置 |
| H2 | 修改通信协议后更新 `AGENTS.md` 通信协议节 | 协议文档过期 → AI 违反大小写/大端序规则 |
| H3 | 修改技术栈/依赖/测试后更新 `tech-stack.md` | 技术栈文档过期 → AI 引入错误依赖 |
| H4 | 有踩坑经验可更新到 `MEMORY.md` | 记忆文档过时 → AI 反复踩坑 |

## I. 测试规范（违反 = 测试失效或归属错误）

| # | 约束 | 违反后果 |
|---|------|---------|
| I1 | 测试文件放 `Tests/` 目录 | 测试散落主项目根目录 → 归属错误 |
| I2 | 测试隔离：每个测试类独立，不依赖共享状态 | 无隔离 → 测试状态残留，断言失败 |
| I3 | 集成测试（如 ADB 相关）连接信息用环境变量，禁止硬编码 | 硬编码 → 不可移植 |

## J. AI 执行流程（违反 = 流程混乱导致遗漏）

| # | 约束 | 违反后果 |
|---|------|---------|
| J1 | 改动前：判断任务类型 → 选正确目录 → 通信相关判断协议约束 | 跳过 → 文件放错或协议违反 |
| J2 | 改动时：最小改动 + 不破坏 Designer + 不引入新框架 | 破坏 Designer → 设计器崩溃 |
| J3 | 改动后：构建验证 → 测试验证 → 结构变化同步 agents 文档 | 不验证 → 引入构建失败 |
| J4 | 自检5项不通过不得跳过（目录/命名空间/依赖方向/协议合规/最小改动） | 跳过自检 → 约束违反 |

## K. 记忆与记录（违反 = 信息丢失或覆盖）

| # | 约束                                                   | 违反后果 |
|---|------------------------------------------------------|---------|
| K1 | 长期记忆 = `MEMORY.md`，每日笔记 = `C/_worklog/yyyy_MM/DD.md` | 不记录 → 下次会话重复踩坑 |
| K2 | 先读取原内容再更新文件，避免信息覆盖                                   | 直接覆盖 → 历史记忆丢失 |
| K3 | 临时文件存 `C:/_worklog/`，按 `yyyy_MM/DD` 组织，不提交 git       | 临时文件进 git → 仓库污染 |
| K4 | 不等用户说"记住这个"，有价值信息主动记录                                | 不主动记录 → 关键信息遗漏 |

## L. 上下文使用（违反 = token浪费或重复劳动）

| # | 约束 | 违反后果 |
|---|------|---------|
| L1 | 不总结历史对话，不重复上下文，只关注当前任务 | 重复上下文 → token浪费 |
| L2 | 禁止复述用户输入 | 复述 → 被判定为冗余错误 |
| L3 | 仅在用户要求时解释，最多 3 行 | 冗长解释 → 被判定为错误 |

## M. 工具使用（违反 = 操作绕过MCP导致不可控）

| # | 约束 | 违反后果 |
|---|------|---------|
| M1 | 对项目的所有操作（读文件、写文件、搜索、构建、测试等）必须通过 MCP 工具执行 | 绕过 MCP 直接操作 → 操作不可追踪、不可审计 |
| M2 | 严禁用 `bash` 替代 MCP 专用工具做项目内文件操作 | bash 做文件操作 → 绕过 MCP 管控层，行为不可观测 |

---

# 步骤3：约束确认输出（强制 — 不可用占位符）

读完4个上下文文件后，首次回复必须输出以下确认标记。**禁止用 "xxx" 占位符**，必须写出每条约束的具体内容关键词：

```
上下文已加载: PROFILE ✓ 目录树 ✓ 技术栈 ✓ 记忆 ✓  约束 ✓
我已经确认了老板对我的约束：
1. [A类] Models/数据模型 Network/通信层零UI UI/界面 Utils/工具 命名空间=目录路径 依赖方向不可逆
2. [B类] JSON反序列化PropertyNameCaseInsensitive=true 大端序Array.Reverse 新消息类型加case 帧格式4+1+JSON
3. [C类] VirtualMode禁止Items.Add RingBuffer非线程安全需BeginInvoke 智能滚动IsAtBottom JsonTreeView OwnerDrawText
4. [D类] 手写逻辑不进Designer.cs switch新分支在default前 数据模型不继承Component rtk前缀
5. [E类] 中文简洁 给方案说利弊 只输出结果 代码≤50行 结尾招呼+时间
6. [F类] 改动后构建验证 最小改动 不引入新框架 不确定先确认 有价值先记录
7. [G类] 小文件MCP读写 大文件MCP读写 超大文件分段 禁止单次write整文件
8. [H类] 新删文件更新directory-tree.md 改协议更新AGENTS.md 改技术栈更新tech-stack.md 踩坑更新MEMORY.md
9. [I类] 测试放Tests/ 隔离不依赖共享状态 集成测试环境变量不硬编码
10. [J类] 改动前判类型选目录 不破坏Designer 改动后构建验证 自检5项
11. [K类] 长期记忆MEMORY.md 先读再更新 临时文件C:/_worklog/ 主动记录
12. [L类] 不重复上下文 不复述输入 最多3行解释
13. [M类] 所有操作走MCP bash不做文件操作
```

---

# 任务执行前自检（每次改动前必须过）

在执行任何代码改动前，逐项自检：

| # | 自检项 | ✅ 通过条件 |
|---|--------|------------|
| 1 | 目录归属 | 文件放在正确目录（Models/Network/UI/Utils/） |
| 2 | 命名空间 | 命名空间 = LogViewer. + 目录路径 |
| 3 | 依赖方向 | 无反向依赖（Network 不引用 UI，Utils 不引用 UI） |
| 4 | 协议合规 | 涉及 JSON 反序列化时 `PropertyNameCaseInsensitive = true`；涉及长度字段时大端序处理 |
| 5 | Designer 安全 | 手写逻辑不在 Designer.cs 中 |
| 6 | 最小改动 | 只改必要的，不重构不相关部分 |

自检不通过的项目，必须先修正再继续执行，不得跳过。

---

# 通信协议速查（最高频踩坑）

## 消息帧格式

```
[4字节大端int=payload长度][1字节消息类型][UTF-8 JSON字节]
```

- 长度字段 = `1(类型) + JSON字节数`
- 大端序：Java `ByteBuffer.putInt()` 默认大端；C# 端 `if (BitConverter.IsLittleEndian) Array.Reverse()`

## 消息类型

| 类型码 | 名称 | 方向 | 数据结构 |
|--------|------|------|----------|
| `0x01` | 设备注册 | Android→PC | `DeviceRegisterInfo` |
| `0x02` | 网络日志 | Android→PC | `LogData` |

## JSON 大小写（已踩坑，务必牢记）

Android 端 **Gson** 序列化输出**小写驼峰**（`isRedirect`, `sendTime`）。
C# 端 `System.Text.Json` 默认**区分大小写**，属性是 PascalCase（`IsRedirect`, `SendTime`）。

**必须设置 `PropertyNameCaseInsensitive = true`**，否则所有字段反序列化为默认值。

---

# 项目架构速查

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

## C# 项目结构

```
LogViewer/
├── Program.cs                          入口
├── LogViewer.csproj             .NET 8 WinForms
├── Models/                             数据模型层（零UI依赖）
│   ├── LogEntry.cs                     网络日志13字段模型+预览属性
│   ├── SystemLogEntry.cs               系统日志模型+Level着色
│   ├── DeviceInfo.cs                   设备注册信息模型
│   ├── AppSettings.cs                  设置+Properties.Settings持久化
│   └── RingBuffer.cs                   O(1)高性能环形缓冲区
├── Network/                            通信层（零UI依赖）
│   ├── LogServer.cs                    TCP Server，AcceptLoop多设备
│   ├── DeviceConnection.cs             单设备连接+协议解析+ArrayPool
│   └── LogcatReader.cs                 adb logcat进程流式读取+正则解析
├── UI/                                 界面层
│   ├── MainForm.cs                     主窗口全部逻辑
│   ├── JsonDetailForm.cs               双击弹出的JSON详情窗口
│   ├── JsonTreeView.cs                 JSON折叠+语法高亮TreeView
│   ├── DevicePanel.cs                  设备列表+切换面板
│   └── SettingsDialog.cs               设置对话框
├── Static/                             静态资源层（零依赖，被UI引用）
│   └── Language.cs                     UI字符串常量
├── Utils/                              工具类
│   ├── AdbHelper.cs                    ADB搜索/验证/设备列表/Reverse
│   └── JsonFormatter.cs                JSON格式化+JSONPath
└── Properties/
    ├── Settings.settings               用户设置schema
    └── Settings.Designer.cs            类型化Settings访问类
```

## 依赖方向

```
UI ──► Network ──► Models
 │        │
 ├──► Utils ──► Models
 └──► Static（零依赖）
```

禁止：Network → UI，Utils → UI，Models → 任何层，Static → 任何层。
