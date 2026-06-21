# 组件与模块创建/更新指南

本文档供 AI 助手阅读。规定在 LogViewer 中新增或修改代码模块时必须遵守的规范、步骤和检查清单。

---

## 1. 目录结构

完整项目目录树见 [directory-tree.md](directory-tree.md)。

```
LogViewer/
├── Models/          ← 数据模型层（零UI依赖）
├── Network/         ← 通信层（零UI依赖）
├── UI/              ← 界面层
├── Utils/           ← 工具类
└── Properties/      ← 设置资源（自动生成）
```

### 1.1 放置规则

| 类型 | 放置位置 | 命名空间 | 判断依据 |
|------|---------|---------|---------|
| 数据模型类 | `Models/` | `LogViewer.Models` | LogEntry/DeviceInfo/RingBuffer 等纯数据结构，零UI依赖 |
| 通信层代码 | `Network/` | `LogViewer.Network` | TCP Server/协议解析/adb logcat，零UI依赖 |
| 窗体/控件 | `UI/` | `LogViewer.UI` | Form/UserControl/自绘控件，可引用 Network+Utils+Models |
| 工具类 | `Utils/` | `LogViewer.Utils` | ADB操作/JSON格式化等纯逻辑，不引用 UI/Network |

### 1.2 命名空间规则

**命名空间 = `LogViewer.` + 目录路径（斜杠换点）**

| 目录 | 命名空间 |
|------|---------|
| `Models/` | `LogViewer.Models` |
| `Network/` | `LogViewer.Network` |
| `UI/` | `LogViewer.UI` |
| `Utils/` | `LogViewer.Utils` |
| 项目根 | `LogViewer` |

### 1.3 文件命名规则

- 一个文件一个公共类型，文件名与类型名相同
- 枚举若仅服务于单个类型，可放在同一文件内
- 枚举若被多个类型共用，独立成文件放到对应目录

### 1.4 依赖方向

```
UI ──→ Network ──→ Models
 │        │
 └──→ Utils ──→ Models
```

**禁止反向引用**：Network → UI，Utils → UI，Models → 任何层。

---

## 2. 新增模块清单

### 2.1 新增数据模型（Models/）

**步骤**：

1. 在 `Models/` 下创建 `.cs` 文件
2. 命名空间设为 `LogViewer.Models`
3. 编写模型类，添加 `/// <summary>` XML 文档注释
4. 若需反序列化，**必须**使用 `PropertyNameCaseInsensitive = true` 的 JsonSerializerOptions
5. 非可视化数据模型用普通类 + public Dispose()，不继承 Component
6. 构建验证

**检查清单**：

- [ ] 命名空间为 `LogViewer.Models`
- [ ] 不引用 `LogViewer.UI` 或 `LogViewer.Network`
- [ ] 若涉及 JSON 反序列化，已设置 `PropertyNameCaseInsensitive = true`
- [ ] 不继承 Component
- [ ] 构建通过

### 2.2 新增通信层代码（Network/）

**步骤**：

1. 在 `Network/` 下创建 `.cs` 文件
2. 命名空间设为 `LogViewer.Network`
3. 编写逻辑，通过事件向 UI 层通知（不直接引用 UI 控件）
4. 若涉及协议解析，遵守帧格式：`[4字节大端int长度][1字节类型][UTF-8 JSON]`
5. 长度字段必须处理大端序：`if (BitConverter.IsLittleEndian) Array.Reverse()`
6. 新增消息类型须在 `DeviceConnection.cs` 的 `switch (messageType)` 中加 case（放在 `default` 之前）
7. 构建验证

**检查清单**：

- [ ] 命名空间为 `LogViewer.Network`
- [ ] 不引用 `LogViewer.UI`
- [ ] 若涉及协议长度字段，已处理大端序
- [ ] 若涉及 JSON 反序列化，已设置 `PropertyNameCaseInsensitive = true`
- [ ] 新增消息类型已在 DeviceConnection.cs switch 中加 case
- [ ] 事件在后台线程触发，UI 更新由订阅方通过 BeginInvoke 处理
- [ ] 构建通过

### 2.3 新增界面层代码（UI/）

**步骤**：

1. 在 `UI/` 下创建 `.cs` 文件
2. 命名空间设为 `LogViewer.UI`
3. 手写逻辑放非 Designer partial 类，不写进 `*.Designer.cs`
4. 订阅 Network/Logcat 事件时，UI 更新必须通过 `BeginInvoke` 切回 UI 线程
5. 日志列表必须使用 VirtualMode（`VirtualListSize` + `RetrieveVirtualItem`），禁止 `Items.Add()`
6. 日志存储使用 RingBuffer，**RingBuffer 非线程安全**，所有读写必须在 UI 线程
7. 自动滚动先判断 `IsAtBottom`，底部才跟随，非底部不打断
8. 自绘控件（如 JsonTreeView）使用 `DrawMode = OwnerDrawText` + `OnDrawNode`
9. 构建验证

**检查清单**：

- [ ] 命名空间为 `LogViewer.UI`
- [ ] 手写逻辑不在 Designer.cs 中
- [ ] Network/Logcat 事件回调中使用 `BeginInvoke` 更新 UI
- [ ] 日志列表用 VirtualMode，无 `Items.Add()`
- [ ] RingBuffer 读写在 UI 线程
- [ ] 自动滚动先 `IsAtBottom` 判断
- [ ] 构建通过

### 2.4 新增工具类（Utils/）

**步骤**：

1. 在 `Utils/` 下创建 `.cs` 文件
2. 命名空间设为 `LogViewer.Utils`
3. 编写纯逻辑工具，不引用 UI 控件或 Network 类型
4. 构建验证

**检查清单**：

- [ ] 命名空间为 `LogViewer.Utils`
- [ ] 不引用 `LogViewer.UI` 或 `LogViewer.Network`
- [ ] 构建通过

---

## 3. 修改现有模块

### 3.1 修改 Models

| 操作 | 注意事项 |
|------|---------|
| 新增属性 | 若参与 JSON 反序列化，确认 Gson 字段名与 C# 属性名大小写兼容（`PropertyNameCaseInsensitive = true`） |
| 新增模型类 | 放 `Models/`，命名空间 `LogViewer.Models`，零 UI 依赖 |

### 3.2 修改 Network

| 操作 | 注意事项 |
|------|---------|
| 新增消息类型 | DeviceConnection.cs switch 中加 case（放在 `default` 之前） |
| 修改帧格式 | 更新 AGENTS.md 通信协议节 + MEMORY.md 协议踩坑节 |
| 修改事件 | 确认 UI 订阅方通过 BeginInvoke 更新，不直接操作控件 |

### 3.3 修改 UI

| 操作 | 注意事项 |
|------|---------|
| 新增日志展示列 | LogEntry 增属性 → MainForm CreateNetworkLogTab Columns.Add → OnRetrieveNetworkItem 填值 |
| 新增 Tab/面板 | MainForm 中创建，日志数据走 RingBuffer + VirtualMode |
| 修改 Designer | 只用设计器修改 Designer.cs，手写逻辑放非 Designer partial |

### 3.4 修改 Utils

| 操作 | 注意事项 |
|------|---------|
| 修改 AdbHelper | 注意 ADB 命令行参数格式，`adb -s {serial}` 指定设备 |
| 修改 JsonFormatter | 保持 `PropertyNameCaseInsensitive = true` |

---

## 4. 模块类型决策树

```
新增代码属于哪类？
├─ 数据模型（纯数据/零UI依赖）→ Models/
│   └─ 若需 JSON 反序列化 → 必须 PropertyNameCaseInsensitive = true
├─ 通信/协议（TCP/adb/零UI依赖）→ Network/
│   └─ 新增消息类型 → DeviceConnection.cs 加 case
├─ 界面（Form/控件/用户交互）→ UI/
│   ├─ 日志列表 → VirtualMode + RingBuffer + BeginInvoke
│   └─ 自绘控件 → OwnerDrawText + OnDrawNode
└─ 纯工具（无UI/无Network依赖）→ Utils/
```

---

## 5. 注释规范

### 5.1 必须注释

| 目标 | 格式 |
|------|------|
| 所有公共类型 | `/// <summary>` 描述职责 |
| 所有公共属性 | `/// <summary>` 描述语义 |
| 所有公共方法 | `/// <summary>` 描述行为 |
| 枚举成员 | `/// <summary>` 描述含义 |

### 5.2 禁止

- 无意义的重复注释
- 把注释当版本控制

---

## 6. 设计器兼容规范

- 手写逻辑放非 Designer partial 类，不写进 `*.Designer.cs`
- 公共属性加 `[DefaultValue]` 特性
- 自定义属性加 `[Browsable(true/false)]` 控制属性面板显示

---

## 7. 构建验证

每次新增或修改后，**必须**执行构建验证：

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

构建失败优先检查：
1. 命名空间是否正确
2. 是否缺少 `using` 指令
3. 依赖方向是否违规
4. 可空引用类型是否正确标注

---

## 8. 常见错误速查

| 错误现象 | 原因 | 修复 |
|------|------|------|
| JSON 反序列化后字段全为默认值 | 未设 `PropertyNameCaseInsensitive = true` | 添加此选项 |
| 协议解析长度值异常 | 未处理大端序 | `if (BitConverter.IsLittleEndian) Array.Reverse()` |
| 新消息类型被丢弃 | DeviceConnection.cs switch 中未加 case | 加 case 在 `default` 之前 |
| 百万条日志卡顿 | ListView 用了 `Items.Add()` | 改用 VirtualMode |
| 跨线程异常 | 后台线程直接操作 UI 控件 | 用 `BeginInvoke` 切回 UI 线程 |
| RingBuffer 数据竞争 | 跨线程读写 RingBuffer | 所有读写在 UI 线程（BeginInvoke 保证） |
| 用户查看历史被滚动打断 | 始终强制 ScrollToBottom | 先 `IsAtBottom` 判断，底部才跟随 |
| Designer.cs 被覆盖 | 手写逻辑写入了 Designer.cs | 移到非 Designer partial 类 |
| 构建失败命名空间错误 | 命名空间与目录不匹配 | 命名空间 = `LogViewer.` + 目录路径 |

---

## 9. 一句话摘要

新增代码按类型放 `Models/`（数据模型）/ `Network/`（通信层，零UI）/ `UI/`（界面）/ `Utils/`（工具）；新增后同步更新 [directory-tree.md](directory-tree.md)；JSON 反序列化必须 `PropertyNameCaseInsensitive = true`；协议长度字段必须处理大端序；日志列表必须 VirtualMode；RingBuffer 读写必须在 UI 线程；每次改动后构建验证。
