# plan-TabFormExtraction-v1

> **For agentic workers:** 实施本 Plan 前先加载 `rtk` 和 `dotnet-winforms-guidelines`；按 Task 顺序执行，每完成一个 Task 运行 `rtk dotnet build` 验证。

**Goal:** 将 MainForm 的三个 Tab（网络日志、普通日志、系统日志）封装为三个独立的 Form，支持 Rider/VS 设计器独立打开并调整 UI。

**Architecture:** 每个日志类型抽取为独立 Form（`NetworkLogForm`/`NormalLogForm`/`SystemLogForm`），各自拥有 Designer.cs 控件树；MainForm 通过构造函数注入共享数据（RingBuffer/Settings/DeviceId），Tab 切换时内嵌宿主显示对应 Form。Form 依赖方向为 `Form → Models + Static + Utils`，不直接引用 Network 层——Network 层事件（LogReceived/NormalLogReceived/SystemLogReceived）由 MainForm 订阅并 BeginInvoke 到 UI 线程，数据写入 RingBuffer 也在 MainForm 中完成，Form 只接收已处理的数据。

**定位：** 改造现有功能，对 Models/Network/Utils/Static 层零影响，仅重构 UI 层内部结构。

---

## 占位符说明

| 占位符 | 含义 | 示例 |
|--------|------|------|
| `NetworkLogForm` | 网络日志窗体 | `NetworkLogForm.cs` |
| `NormalLogForm` | 普通日志窗体 | `NormalLogForm.cs` |
| `SystemLogForm` | 系统日志窗体 | `SystemLogForm.cs` |

---

## 技术栈

| 类别 | 技术选型 | 说明 |
|------|---------|------|
| **Windows UI** | WinForms + Form 内嵌 | `Form.TopLevel=false` + `FormBorderStyle=None` 宿主嵌入 TabPage |
| **验证** | `rtk dotnet build` | 每个 Task 结束后执行 |

---

## 相关 Skills

| Skill | 用途 | 加载时机 |
|-------|------|---------|
| `rtk` | 命令前缀 | 开始实施前（必需） |
| `dotnet-winforms-guidelines` | WinForms 窗体生命周期、Designer 安全 | 开始实施前（必需） |

---

## 一、功能介绍

### 1.1 背景

当前三个日志 Tab 的所有控件（ListView/FilterPanel/Button/Label）都定义在 `MainForm.Designer.cs` 中，逻辑散布在 `MainForm.NetworkLogs.cs`/`MainForm.NormalLogs.cs`/`MainForm.SystemLogs.cs` 三个 partial class 中。这导致：

1. **Designer 臃肿** — MainForm.Designer.cs 近 900 行，三种日志控件混在一起，设计器加载慢且难以定位
2. **无法独立设计** — 想调整某个 Tab 的 UI 布局，必须打开整个 MainForm
3. **职责不清** — 每个 Tab 的过滤/显示/交互逻辑与 MainForm 的设备管理/服务器事件逻辑耦合

**跨 partial class 依赖（搬迁时必须拆分）：**
- `MainForm.NetworkLogs.cs` 的 `OnExportJson()` / `OnExportTxt()` 直接调用了 `GetCurrentNormalLogBuffer()`（定义在 `MainForm.NormalLogs.cs`），还访问 `_showingNormalLog`（定义在 `MainForm.cs`）。NetworkLogs 的导出逻辑"越界"操作了 Normal 日志数据，搬迁时必须拆分。
- `MainForm.cs` 的 `UpdateLogCount()` 跨 3 个 Tab 操作，访问 `_filteredNetworkIndices`（NetworkLogs）、`_filteredNormalIndices`（NormalLogs）、`_lstNetworkLogs/_lstNormalLogs/_lstSystemLogs` 的 AutoScroll+IsAtBottom 状态。搬迁后这些私有字段/控件不再对 MainForm 可见，必须重新设计计数更新协议（Form 暴露 `IsAutoScrollActive` 属性或 `ScrollStateChanged` 事件，`LogCountChanged` 事件携带计数信息）。

### 1.2 现有架构

```mermaid
graph LR
    A[MainForm.Designer.cs 控件树] --> B[MainForm.cs 共享字段]
    B --> C[MainForm.NetworkLogs.cs]
    B --> D[MainForm.NormalLogs.cs]
    B --> E[MainForm.SystemLogs.cs]
```

### 1.3 新增后架构

```mermaid
graph LR
    A[MainForm] -->|构造注入| B[NetworkLogForm]
    A -->|构造注入| C[NormalLogForm]
    A -->|构造注入| D[SystemLogForm]
    B --> E[Models + Static + Utils]
    C --> E
    D --> E
    A -->|Network事件中转| F[Network]
    F -.->|LogReceived等| A
```

> Form 不直接引用 Network 层。Network 事件由 MainForm 订阅，数据写入 RingBuffer 后通过 Form 公共方法通知 UI 更新。

### 1.4 方案核心

每个日志 Tab 抽取为独立 Form，各自拥有 `*.Designer.cs`。MainForm 在构造时创建三个 Form 实例并嵌入 TabPage。Form 通过构造函数接收共享数据引用（RingBuffer/Settings/设备ID 等），不反向引用 MainForm。

**关键设计决策：Form vs UserControl**

| 方案 | 优点 | 缺点 |
|------|------|------|
| **Form（推荐）** | 老板指定；可独立 Show() 调试；Designer 支持完整 | 内嵌需 `TopLevel=false` hack |
| UserControl | 内嵌更自然；标准 WinForms 组合模式 | 不符合老板要求"封装成 Form" |

**采纳 Form 方案**，内嵌方式：`form.TopLevel = false; form.FormBorderStyle = FormBorderStyle.None; form.Dock = DockStyle.Fill; tabPage.Controls.Add(form); form.Show();`

**TopLevel=false 内嵌已知坑及应对：**
1. **焦点链断裂** — 内嵌 Form 中的控件不参与父 Form 的 `ProcessTabKey`，Tab 键无法从 MainForm 跳入内嵌 Form 的控件。需在 MainForm 重写 `ProcessTabKey` 或用 `SelectNextControl` 手动桥接。
2. **快捷键/菜单合并** — 内嵌 Form 的 MenuStrip/ContextMenuStrip 不与父 Form 合并。当前右键菜单在各 Form 内创建，但非模态子窗口 ContextMenuStrip 有已知的 `ToolStripManager.ModalMenuFilter` 拦截问题（见 MEMORY.md），内嵌 Form 可能加剧此问题。
3. **AutoSize/Docking 异常** — `Form.Dock=Fill` 在 TabPage 中偶尔不随 TabPage 尺寸变化而 relayout，需测试窗口 resize + split 移动场景。

**内嵌 Form 焦点/事件路由流程：**

```mermaid
flowchart TD
    A[用户点击 TabPage] --> B[TabControl.SelectedIndexChanged]
    B --> C[MainForm 设置 _showingXxxLog 标志]
    B --> D[MainForm 调用 targetForm.RefreshFilter]
    D --> E[内嵌 Form 处理过滤逻辑]
    B --> F{内嵌 Form 是否自动获焦?}
    F -->|TopLevel=false 不自动获焦| G[⚠️ 用户需额外点击才能操作 ListView]
    F -->|需手动 form.Select| H[Form.Select → 子控件获焦]
    G --> I[⚠️ 键盘快捷键 Ctrl+C 等可能不响应]
    H --> I2[键盘事件正常路由]
```

### 1.5 功能对比

| 维度 | 现在 | 改造后 |
|------|------|--------|
| 设计器 | 只能打开 MainForm 整体 | 每个 Form 可独立打开 |
| 控件归属 | 全部在 MainForm.Designer.cs | 各 Form 自有 Designer.cs |
| 代码归属 | partial class 共享全部字段 | Form 自有字段 + 构造注入共享数据 |
| 运行时行为 | — | **有变更**：`ProcessCmdKey`（MainForm.cs:1422）当前统一处理 End 键，直接操作 `_lstNetworkLogs/_lstNormalLogs/_lstSystemLogs` 和 `_networkAutoScrollEnabled/_normalAutoScrollEnabled/_systemAutoScrollEnabled`。搬迁后这些控件和字段在各 Form 内部，End 键必须改为委托给当前活动 Form（如 `activeForm.ScrollToEndAndEnableAutoScroll()`） |

---

## 二、UI 设计

### 2.1 设计稿来源

| 项目 | 说明 |
|------|------|
| **设计平台** | 无设计稿，基于现有 WinForms 截图 |
| **补充来源** | 当前运行时截图 + MainForm.Designer.cs |

### 2.2 控件搬迁映射

| 现有控件（MainForm.Designer.cs） | 目标 Form | 说明 |
|------|------|------|
| `_lstNetworkLogs` + `_networkActionBar` + `_networkFilterPanel` + `_btnScrollToTop/Bottom` + `_lblLogCount` + **`_networkTabContainer`** (Panel) | `NetworkLogForm` | 网络 Tab 全部控件（含根容器 `_networkTabContainer`） |
| `_lstNormalLogs` + `_normalActionBar` + `_normalFilterPanel` + `_btnNormalScrollToTop/Bottom` + `_lblNormalLogCount` + **`_normalTabContainer`** (Panel) | `NormalLogForm` | 普通 Tab 全部控件（含根容器 `_normalTabContainer`） |
| `_lstSystemLogs` + `_systemActionBar` + `_systemFilterPanel` + `_btnSystemScrollToTop/Bottom` + `_btnSystemPauseResume` + `_lblSystemBacklog` + **`_systemTabContainer`** (Panel) | `SystemLogForm` | 系统 Tab 全部控件（含根容器 `_systemTabContainer`） |
| `_tabLogType` + 三个 TabPage 容器 | MainForm（保留） | Tab 切换逻辑留在 MainForm，原 TabPage 从"容器+控件"变为只挂一个内嵌 Form |
| 详情面板 / 工具栏 / 状态栏 / 设备面板 | MainForm（保留） | 非本次迁移范围 |

**控件容器说明：** 当前 Designer.cs 中每个 TabPage 的直接子控件是根容器 Panel（`_networkTabContainer`/`_normalTabContainer`/`_systemTabContainer`），ListView/ActionBar/FilterPanel 都嵌在容器内部（见 Designer.cs:32-38）。搬迁时这些容器必须移入各 Form，否则 Form 内部布局无法在 Designer 中独立调整。原 TabPage 将从"容器+控件"变为只挂一个内嵌 Form。

**控件层级对比（当前 vs 搬迁后）：**

```mermaid
flowchart LR
    subgraph 当前["当前 MainForm.Designer.cs 层级"]
        TP1[_tabNetwork TabPage] --> NC[_networkTabContainer Panel]
        NC --> LV1[_lstNetworkLogs ListView]
        NC --> AB1[_networkActionBar Panel]
        NC --> FP1[_networkFilterPanel]
        AB1 --> BTN1[ScrollToTop/Bottom + lblLogCount]
    end
    subgraph 搬迁后["搬迁后层级"]
        TP2[_tabNetwork TabPage] --> NF[NetworkLogForm TopLevel=false]
        NF --> NC2[_networkTabContainer Panel]
        NC2 --> LV2[_lstNetworkLogs]
        NC2 --> AB2[_networkActionBar]
        NC2 --> FP2[_networkFilterPanel]
        AB2 --> BTN2[ScrollToTop/Bottom + lblLogCount]
    end
```

TabPage 与 NetworkLogForm 之间只有一层关系（TabPage.Controls.Add(form)），原 `_networkTabContainer` 成为 Form 的根控件，TabPage 不再拥有它。

**FilterPanel 自定义控件风险：** FilterPanel 是项目自定义控件（`LogViewer.UI.FilterPanel`），不是标准 WinForms 控件。Rider 外部设计器对自定义控件的实例化有已知限制（见 MEMORY.md "Rider 设计器优先用标准控件"）。需验证 FilterPanel 在独立 Form 的 Designer.cs 中能否被 Rider 正确实例化，否则设计器打开会报错。

---

## 三、数据模型设计

### 3.1 构造注入参数

每个 Form 通过构造函数接收以下共享数据：

**NetworkLogForm 注入参数：**

| 参数 | 类型 | 来源 |
|------|------|------|
| `deviceLogs` | `Dictionary<string, RingBuffer<LogEntry>>` | MainForm._deviceLogs |
| `allLogs` | `RingBuffer<LogEntry>` | MainForm._allLogs |
| `settings` | `AppSettings` | MainForm._settings |
| `getCurrentDeviceId` | `Func<string?>` | MainForm._currentDeviceId 动态读取 |

**NormalLogForm 注入参数：** 同上，换为 Normal 版本（`_deviceNormalLogs`, `_allNormalLogs`, `_settings`, `_getCurrentDeviceId`）。

> **注意：** NormalLogForm 还需注入 `GetCurrentNormalLogBuffer()` 的依赖数据（`_currentDeviceId` + `_allNormalLogs` + `_deviceNormalLogs`）。同时，NetworkLogForm 也需要类似的 `GetCurrentLogBuffer()` 逻辑（定义在 `MainForm.cs:554`，依赖 `_currentDeviceId` + `_allLogs` + `_deviceLogs`），两者模式一致，需统一为公共方法模式。

**SystemLogForm 注入参数：**

| 参数 | 类型 | 来源 |
|------|------|------|
| `systemLogStore` | `SystemLogSessionStore` | MainForm._systemLogStore |
| `settings` | `AppSettings` | MainForm._settings |
| `getCurrentDeviceId` | `Func<string?>` | MainForm._currentDeviceId 动态读取 |
| `adbSerialToDeviceId` | `Dictionary<string, string>` | MainForm._adbSerialToDeviceId |

### 3.2 事件通信

Form 向 MainForm 的事件通知：

| 事件 | 所属 Form | 说明 |
|------|----------|------|
| `LogEntrySelected` | NetworkLogForm | 选中日志条目，MainForm 更新预览面板 |
| `LogEntryDoubleClicked` | NetworkLogForm | 双击打开 JsonDetailForm |
| `NormalLogEntrySelected` | NormalLogForm | 普通日志选中（预留，当前仅复制 Message） |
| `NormalLogEntryDoubleClicked` | NormalLogForm | 普通日志双击（预留） |
| `LogCountChanged` | 三个 Form | 过滤/总数变化，MainForm 更新状态栏 |
| `SystemLogPausedChanged` | SystemLogForm | Pause/Resume 状态变化 |
| `ExportRequested` | NetworkLogForm + NormalLogForm | 导出请求（JSON/TXT），MainForm 协调实际导出 |
| `ClearRequested` | 三个 Form | 清空请求，MainForm 协调三方数据清除 |
| `ScrollStateChanged` | 三个 Form | AutoScroll 状态变化，MainForm 更新按钮背景色 |

**OnClear() 跨三层协调流程：**

```mermaid
flowchart TD
    A[_btnClear Click] --> B[MainForm.OnClear]
    B --> C{currentDeviceId != null?}
    C -->|是| D[buf.Clear / nBuf.Clear / _systemLogStore.ClearDevice]
    C -->|否| E[遍历 _deviceLogs.Clear / _allLogs.Clear / _allNormalLogs.Clear / _systemLogStore.RotateSession]
    D --> F[_networkLogForm.ClearFilterAndRefresh]
    D --> G[_normalLogForm.ClearFilterAndRefresh]
    D --> H[_systemLogForm.ClearAndRefresh]
    E --> F
    E --> G
    E --> H
    F & G & H --> I[_selectedLogEntry = null]
    I --> J[ShowLogDetail null]
    J --> K[RefreshMirrorPanelState]
```

关键：数据清除（RingBuffer.Clear）在 MainForm，过滤索引清除（_filteredXxxIndices.Clear）在各 Form 内部，两者必须按序执行。MainForm 先清数据，再通知 Form 清索引+刷新 UI。

**事件设计说明：**
> 1. **导出事件** — `OnExportJson`/`OnExportTxt` 当前在 `MainForm.NetworkLogs.cs` 中同时处理网络日志和普通日志导出（通过 `_showingNormalLog` 分支）。搬迁后导出逻辑必须在 MainForm 或独立导出服务中，不能仅放在 NetworkLogForm。各 Form 通过 `ExportRequested` 事件通知 MainForm。
> 2. **清空事件** — `OnClear` 在 MainForm.cs 中同时清除 `_deviceLogs` + `_deviceNormalLogs` + `_systemLogStore`。按钮 `_btnClear` 在 MainForm 底部栏，清空需协调 3 个 Form 的数据。各 Form 通过 `ClearRequested` 事件或 MainForm 直接调用 Form 的公共 `Clear()` 方法。
> 3. **AutoScroll 状态** — `UpdateLogCount()` 需要知道各 Form 的 AutoScroll+IsAtBottom 状态来更新按钮背景色和计数文本。各 Form 暴露 `IsAutoScrollActive` 属性或 `ScrollStateChanged` 事件。

---

## 四、实施阶段

### 依赖关系

```mermaid
graph LR
    A[Task 0: 创建 NetworkLogForm 骨架] --> D[Task 3: 改造 MainForm]
    B[Task 1: 创建 NormalLogForm 骨架] --> D
    C[Task 2: 创建 SystemLogForm 骨架] --> D
    D --> E[Task 4: 文档更新]
```

**推荐顺序：** Task 0/1/2 可并行 → Task 3（依赖全部三个 Form 完成）→ Task 4

> **Task 3 是最大风险点**，不是简单的移除+嵌入，而是需要大量接口协调工作（事件定义、公共方法暴露、跨 Form 数据流重设计）。当前依赖图低估了 Task 3 的工作量。
>
> **缺少 Task 3.5：运行时数据流重构。** OnLogReceived/OnNormalLogReceived 当前在 MainForm 的 BeginInvoke 中做两件事：(1) 写 RingBuffer 数据 (2) 更新 UI。搬迁后 RingBuffer 写入必须仍在 MainForm（因为数据归属 MainForm），但 UI 更新需委托给 Form。这个拆分不是简单地调 `_networkLogForm.AppendLogEntry()`，而是要把原方法中数据写入和 UI 更新两部分逻辑分离开来，保证原子性和顺序性。

---

### Task 0：创建 NetworkLogForm

**Files:**
- Create: `UI/NetworkLogForm.cs`
- Create: `UI/NetworkLogForm.Designer.cs`

**✅ 验收检查点：**

| # | 检查项 | 验证方式 |
|---|--------|---------|
| 1 | 设计器可打开 NetworkLogForm | Rider 双击打开 |
| 2 | 构建通过 | `rtk dotnet build` |

- [ ] **Step 1: 创建 NetworkLogForm.cs 手写逻辑**

从 `MainForm.NetworkLogs.cs` 搬运以下方法：
- `ConfigureLogLists()` → 改为构造函数中调用
- `GetNetworkLogEntryByViewIndex()`, `OnNetworkLogsRetrieveVirtualItem()`, `CreateNetworkLogItem()`
- `GetCurrentLogBuffer()`（定义在 `MainForm.cs:554`，被 `GetNetworkLogEntryByViewIndex()` 和 `RefreshNetworkFilter()` 调用，必须随 NetworkLogForm 迁移）
- `RefreshNetworkLogList()`, `RefreshNetworkFilter()`, `MatchesNetworkFilter()`
- `TryAppendNetworkLogIncrementally()`, `ScheduleNetworkRefresh()`
- `OnNetworkLogMouseUp()`, `OnNetworkLogsMouseWheel()`, `OnNetworkLogSelected()`, `OnNetworkLogDoubleClick()`

**OnNetworkLogsMouseWheel() 的跨层更新：** `OnNetworkLogsMouseWheel()` (:90-94) 仅做 `_networkAutoScrollEnabled = false; UpdateLogCount();`。搬迁后 `UpdateLogCount()` 在 MainForm 中（它需要知道当前 Tab 是哪个来更新正确的 Label 和按钮背景色），但 MouseWheel 事件在 Form 内部触发，Form 不知道其他 Tab 的状态。**方案：** Form 内部设 `_networkAutoScrollEnabled = false` 后触发 `ScrollStateChanged` 事件，MainForm 收到后调自己的 `UpdateLogCount()`。NormalLogForm/SystemLogForm 的 MouseWheel 同理。

- `CreateNetworkLogMenu()`, `ShowNetworkLogMenu()`, `FormatUrlWithBody()`

**JsonDetailForm owner 问题：** `OnNetworkLogDoubleClick()` (:131-138) 和 `CreateNetworkLogMenu()` 中的"查看详情" (:182) 都调用 `new JsonDetailForm(entry, _lstNetworkLogs.Font).Show(this)`。`Show(this)` 把 MainForm 作为 owner，这是非模态子窗口的关键设置（影响 Z-order 和 `ToolStripManager.ModalMenuFilter` 行为）。搬迁后 `this` 变成了 NetworkLogForm（TopLevel=false），Z-order 可能异常，ModalMenuFilter 拦截逻辑可能更不稳定。**解决：** `LogEntryDoubleClicked` 事件触发后由 MainForm 负责创建和 Show JsonDetailForm，不在 Form 内部创建。

- `GetSelectedNetworkEntry()`

**ShowLogDetail() 必须留在 MainForm：** `ShowLogDetail()` 定义在 `MainForm.NetworkLogs.cs:224`，操作的全是 MainForm 预览面板控件（`_jsonHeadersView`/`_jsonRequestBodyView`/`_jsonResponseBodyView`/`_rawHeaders`/`_rawRequestBody`/`_rawResponseBody`），这些控件定义在 `MainForm.Preview.cs` 和 `MainForm.Designer.cs`，不属于 NetworkLogForm。`ShowLogDetail()` 被以下位置调用：
- `OnNetworkLogSelected()` (:125) — NetworkLogForm 内部，搬迁后改为触发 `LogEntrySelected` 事件
- `OnDeviceSelected()` (MainForm.cs:1076) — MainForm 调用 `ShowLogDetail(null)`，此处不经过 Form
- `OnDeleteDevice()` (MainForm.cs:1113) — MainForm 调用 `ShowLogDetail(null)`
- `OnClear()` (MainForm.cs:1337) — MainForm 调用 `ShowLogDetail(null)`

因此 `ShowLogDetail()` 必须留在 MainForm，不能搬入 NetworkLogForm。NetworkLogForm 只负责触发事件，MainForm 接收事件后调用自己保留的 `ShowLogDetail()`。

- `RefreshNetworkVisibleRows()`, `OnNetworkFilterChanged()`

**不可整体迁移的方法（需拆分）：**
- `OnExportJson()` / `OnExportTxt()` — 当前通过 `_showingNormalLog` 分支同时处理普通日志导出，调用 `GetCurrentNormalLogBuffer()`（定义在 NormalLogs.cs）。搬迁时必须把 Normal 日志导出分支拆回 NormalLogForm 或 MainForm，NetworkLogForm 只处理网络日志导出。
- `ShowLogDetail()` — 操作 MainForm 的预览面板控件（`_selectedLogEntry`/`_tabDetail`/JsonTreeView），不是 NetworkLogForm 的内部逻辑，需改为触发 `LogEntryDoubleClicked` 事件让 MainForm 处理。

**私有字段迁移：**
- `_networkRefreshScheduled`, `_networkRefreshNeedsFullFilter`, `_filteredNetworkIndices`, `_networkAutoScrollEnabled`

**`_selectedLogEntry` 不迁移：** 定义在 `MainForm.cs:210`，但被 `OnNetworkLogSelected()`（NetworkLogs.cs:123-124）直接读写。这个字段属于 MainForm 预览面板的状态，搬迁后 NetworkLogForm 的 `LogEntrySelected` 事件把 entry 传回 MainForm，由 MainForm 维护 `_selectedLogEntry` 并调用 `ShowLogDetail()`。

**共享数据通过构造函数注入（引用传递，非拷贝）：**
- `_deviceLogs`, `_allLogs`, `_settings`
- `_getCurrentDeviceId` (Func<string?>) — 动态读取当前设备

**新增事件：**
- `event Action<LogEntry?>? LogEntrySelected`
- `event Action<LogEntry?>? LogEntryDoubleClicked`

- [ ] **Step 2: 创建 NetworkLogForm.Designer.cs**

从 `MainForm.Designer.cs` 搬运以下控件声明和初始化：
- `_networkTabContainer` (Panel) — 根布局容器，`_lstNetworkLogs`/`_networkActionBar`/`_networkFilterPanel` 都嵌在它内部
- `_lstNetworkLogs` (ListView)
- `_networkFilterPanel` (FilterPanel)
- `_btnScrollToTop`, `_btnScrollToBottom` (Button)
- `_lblLogCount` (Label)
- `_networkActionBar` (Panel)

布局保持与原 MainForm 中 `_networkTabContainer` 内部布局一致。

- [ ] **Step 3: 运行构建验证**

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

Expected: Build succeeds（此时尚未接入 MainForm，仅验证 Form 本身编译）

**最小运行时验证建议：** Task 0 完成后在 MainForm 中临时嵌入空的 NetworkLogForm 到一个 TabPage，确认 Form.TopLevel=false 内嵌 + Rider 设计器打开都正常，再继续 Task 1/2。避免三个 Form 都做完后才发现嵌入方案有根本性问题。

---

### Task 1：创建 NormalLogForm

**Files:**
- Create: `UI/NormalLogForm.cs`
- Create: `UI/NormalLogForm.Designer.cs`

**✅ 验收检查点：**

| # | 检查项 | 验证方式 |
|---|--------|---------|
| 1 | 设计器可打开 NormalLogForm | Rider 双击打开 |
| 2 | 构建通过 | `rtk dotnet build` |

- [ ] **Step 1: 创建 NormalLogForm.cs 手写逻辑**

从 `MainForm.NormalLogs.cs` 搬运以下方法：
- `ConfigureNormalLogList()`, `GetCurrentNormalLogBuffer()`
- `GetNormalLogEntryByViewIndex()`, `OnNormalLogsRetrieveVirtualItem()`, `CreateNormalLogItem()`
- `LevelToDisplayText()`, `LevelFromDisplayText()`, `ExtractLocation()`, `TruncateNormalPreview()`, `LevelToColor()`
- `OnNormalLogsDoubleClick()`, `GetSelectedNormalEntry()`, `CreateNormalLogMenu()`

**OnNormalLogsDoubleClick() owner 问题：** `OnNormalLogsDoubleClick()` (:116-148) 用 `form.Show(this)` 打开了一个动态创建的 Form 来展示普通日志全文。和 NetworkLogForm 的 JsonDetailForm 问题一样——搬迁后 `this` 变成 NormalLogForm（TopLevel=false），`Show(this)` 的 owner 语义异常。需通过事件 `NormalLogEntryDoubleClicked` 由 MainForm 打开详情窗口。

- `RefreshNormalLogList()`, `RefreshNormalFilter()`, `MatchesNormalFilter()`
- `TryAppendNormalLogIncrementally()`, `ScheduleNormalRefresh()`
- `OnNormalFilterChanged()`, `OnNormalLogsMouseWheel()`
- `RefreshNormalVisibleRows()`

**GetApproxVisibleRowCount() 共享方法：** `GetApproxVisibleRowCount()` 是 static 方法，在 `MainForm.SystemLogs.cs:932` 定义，被三个 Tab 的 `RefreshXxxVisibleRows()` 分别调用（NormalLogs.cs:284, NetworkLogs.cs:419, SystemLogs.cs:412）。搬迁后三个 Form 都需要此方法。决策 #6 说提取到 `BufferedListViewHelper`，文件清单需补充此项修改。

**私有字段迁移：**
- `_normalRefreshScheduled`, `_normalRefreshNeedsFullFilter`, `_filteredNormalIndices`, `_normalAutoScrollEnabled`

**共享数据注入：** `_deviceNormalLogs`, `_allNormalLogs`, `_settings`, `_getCurrentDeviceId`

**跨 Form 数据访问问题：** `GetCurrentNormalLogBuffer()` 定义在 `MainForm.NormalLogs.cs:26`，依赖 `_currentDeviceId` + `_allNormalLogs` + `_deviceNormalLogs`。此方法被 NetworkLogs.cs 的导出方法跨文件调用（`MainForm.NetworkLogs.cs:445,472`）。搬迁时此方法移入 NormalLogForm，但 NetworkLogForm 的导出逻辑也需访问 NormalLogBuffer——NormalLogForm 需暴露公共 `GetCurrentBuffer()` 方法，或导出逻辑上提至 MainForm。

- [ ] **Step 2: 创建 NormalLogForm.Designer.cs**

搬运控件：`_normalTabContainer` (Panel根容器), `_lstNormalLogs`, `_normalFilterPanel`, `_btnNormalScrollToTop`, `_btnNormalScrollToBottom`, `_lblNormalLogCount`, `_normalActionBar`

- [ ] **Step 3: 构建验证**

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

---

### Task 2：创建 SystemLogForm

**Files:**
- Create: `UI/SystemLogForm.cs`
- Create: `UI/SystemLogForm.Designer.cs`

**✅ 验收检查点：**

| # | 检查项 | 验证方式 |
|---|--------|---------|
| 1 | 设计器可打开 SystemLogForm | Rider 双击打开 |
| 2 | 构建通过 | `rtk dotnet build` |

- [ ] **Step 1: 创建 SystemLogForm.cs 手写逻辑**

从 `MainForm.SystemLogs.cs` 搬运全部方法（约 40 个）和字段（约 15 个），这是最复杂的 Form。

**关键方法组：**
- 快照管理：`RequestSystemSnapshotRefresh()`, `BuildSystemLogSnapshotAsync()`, `TryCatchUpCurrentSnapshot()`, `ApplySystemSnapshot()`

**IsSystemLogRuntimeReady() 拆分：** 定义在 `MainForm.SystemLogs.cs:922`，被 SystemLogs.cs 内部 7 处调用，也被 `MainForm.cs` 的 4 处调用（TryMatchAdbSerial/OnDeleteDevice/OnClear/OnSettingsClick）。搬迁后 MainForm 需在调 SystemLogForm 方法前判断就绪状态，SystemLogForm 也需要自己的版本。建议拆为：MainForm 保留 `_systemLogStore != null` 简单判断，SystemLogForm 内部自己维护更详细的就绪检查。

- 数据写入：`OnSystemLogReceived()`（入队+防抖调度留 MainForm，见线程安全设计决策），`FlushPendingSystemLogsAsync()`（批量 UI 处理委托给 Form 的 `ProcessPendingLogs`）
- UI 刷新：`ScheduleSystemUiRefresh()`, `RefreshSystemVisibleRows()`, `RequestSystemVisibleRefresh()`
- 过滤：`OnSystemFilterChanged()`, `MatchesSystemRecord()`, `MatchesIncomingSystemEntry()`, `CaptureSystemLogQuery()`
- 暂停/恢复：`OnSystemPauseResumeClick()`, `UpdateSystemLogUiState()`
- 右键菜单：`CreateSystemLogMenu()`, `CopySelectedSystemLogAsync()`, `ShowSystemLogMenu()`
- 预取：`ScheduleSystemLogPrefetch()`, `ScheduleVisibleSystemPrefetch()`, `WarmSystemEntryAsync()`
- 标签更新：`RefreshSystemTagOptions()`, `UpdateSystemTagOptionsFromSnapshot()`

**私有字段迁移：** `_systemLogStore`, `_systemLogSnapshot`, `_systemSnapshotCts`, `_systemPrefetchCts`, `_systemSnapshotVersion`, `_systemLogPaused`, `_systemFreezeSequenceId`, `_systemPausedBacklog`, `_systemVisibleRefreshPending`, `_systemContextSequenceId`, `_systemUiRefreshScheduled`, `_systemUiRefreshNeedsSnapshot`, `_systemAutoScrollEnabled`, `_pendingSystemLogs`, `_pendingSystemLogsLock`, `_systemLogFlushScheduled`

**线程安全队列设计决策：** `_pendingSystemLogs` + `_pendingSystemLogsLock` + `_systemLogFlushScheduled` 是线程安全队列相关字段，当前定义在 `MainForm.cs:53-59`，用途是 LogcatReader 后台线程 `OnSystemLogReceived` → lock 入队 → 防抖 flush → BeginInvoke 批量处理。**入队逻辑留在 MainForm**（因为 LogcatReader 事件在 MainForm 订阅），SystemLogForm 只暴露 `ProcessPendingLogs(List<SystemLogEntry>)` 供 MainForm 调用。如果随 SystemLogForm 迁移，Form 内部就要处理后台线程到 UI 线程的切换，但 Form 的 `BeginInvoke` 依赖 `Handle` 已创建，而 MainForm 构造时 SystemLogForm 可能还未 Show()，Handle 未创建，`BeginInvoke` 会失败。

**SystemLog 数据流（后台线程 → UI 完整链路）：**

```mermaid
flowchart TD
    A[LogcatReader.SystemLogReceived<br/>后台线程] --> B[MainForm.OnSystemLogReceived<br/>后台线程回调]
    B --> C[lock _pendingSystemLogsLock]
    C --> D[_pendingSystemLogs.Enqueue entry]
    D --> E{首次入队?}
    E -->|是| F[ScheduleFlush<br/>Task.Run + Task.Delay 80ms]
    E -->|否| G[返回，等下次 flush]
    F --> H[BeginInvoke → FlushPendingSystemLogsAsync<br/>UI线程]
    H --> I[lock 取出全部 pending]
    I --> J[调 _systemLogForm.ProcessPendingLogs<br/>传 List&lt;SystemLogEntry&gt;]
    J --> K[SystemLogForm 内部：<br/>1. _systemLogStore.AddRange<br/>2. 累计 _systemPausedBacklog<br/>3. ScheduleSystemUiRefresh]
    
    style A fill:#f96
    style B fill:#f96
    style C fill:#f96
    style D fill:#f96
    style H fill:#9cf
    style J fill:#9f9
    style K fill:#9f9
```

红色 = 后台线程，蓝色 = MainForm UI线程，绿色 = SystemLogForm。关键分界点在 `FlushPendingSystemLogsAsync` → `ProcessPendingLogs`。

**共享数据注入：** `_systemLogStore`, `_settings`, `_getCurrentDeviceId`, `_adbSerialToDeviceId`, `Func<bool> isShowingSystemLog`

**`_adbSerialToDeviceId` 注入补充：** 仅注入 `_adbSerialToDeviceId` 不够。`ScheduleVisibleSystemPrefetch()` (:940-952) 访问了 `_showingSystemLog`（定义在 MainForm.cs:68）。搬迁后 SystemLogForm 无法知道 `_showingSystemLog`。当前设计中 SystemLogForm 只有 `isActiveView` 参数（由 MainForm 在日志接收时传入），但 prefetch 调度发生在系统日志内部事件中，不由 MainForm 触发。需要 `Func<bool> isShowingSystemLog` 注入让 Form 在运行时读取。

**_systemLogStore 生命周期说明：** `_systemLogStore` 当前在 `MainForm.cs` 中是 `new()` 创建的私有字段。注入给 SystemLogForm 后，MainForm 中的 `OnClear()`（清除设备数据）和 `InitializeSystemLogRuntime()` 仍需访问 `_systemLogStore`。MainForm 和 SystemLogForm 共享同一个 Store 引用，双方都能修改——**MainForm 拥有 Store 的生命周期（创建/销毁），SystemLogForm 只是消费者。**

- [ ] **Step 2: 创建 SystemLogForm.Designer.cs**

搬运控件：`_systemTabContainer` (Panel根容器), `_lstSystemLogs`, `_systemFilterPanel`, `_btnSystemScrollToTop`, `_btnSystemScrollToBottom`, `_btnSystemPauseResume`, `_lblSystemBacklog`, `_systemActionBar`

- [ ] **Step 3: 构建验证**

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

---

### Task 3：改造 MainForm — 移除已迁移控件 + 嵌入 Form

**Files:**
- Modify: `UI/MainForm.cs`
- Modify: `UI/MainForm.Designer.cs`
- Delete: `UI/MainForm.NetworkLogs.cs`
- Delete: `UI/MainForm.NormalLogs.cs`
- Delete: `UI/MainForm.SystemLogs.cs`

**✅ 验收检查点：**

| # | 检查项 | 验证方式 |
|---|--------|---------|
| 1 | 构建通过 | `rtk dotnet build` |
| 2 | 运行时 Tab 切换正常 | 手动 smoke |
| 3 | 日志接收 + 过滤 + 滚动正常 | 手动 smoke |
| 4 | 设计器可打开 MainForm | Rider 双击 |

- [ ] **Step 1: MainForm.Designer.cs — 移除已迁移控件声明和初始化**

移除以下控件从 Designer.cs（含其根容器 Panel 及全部子控件的完整声明和初始化代码）：
- `_networkTabContainer` + `_lstNetworkLogs`, `_networkFilterPanel`, `_btnScrollToTop`, `_btnScrollToBottom`, `_lblLogCount`, `_networkActionBar`
- `_normalTabContainer` + `_lstNormalLogs`, `_normalFilterPanel`, `_btnNormalScrollToTop`, `_btnNormalScrollToBottom`, `_lblNormalLogCount`, `_normalActionBar`
- `_systemTabContainer` + `_lstSystemLogs`, `_systemFilterPanel`, `_btnSystemScrollToTop`, `_btnSystemScrollToBottom`, `_btnSystemPauseResume`, `_lblSystemBacklog`, `_systemActionBar`

TabPage 容器 (`_tabNetwork`, `_tabNormal`, `_tabSystem`) 保留，内部清空（移除 TabContainer 及其子控件，同步移除相关的 `SuspendLayout/ResumeLayout` 调用）。

- [ ] **Step 2: MainForm.cs — 新增 Form 实例字段和嵌入逻辑**

```csharp
private NetworkLogForm _networkLogForm;
private NormalLogForm _normalLogForm;
private SystemLogForm _systemLogForm;
```

在 `InitializeComponent()` 后嵌入 Form（初始化顺序：`InitializeComponent()` → 创建 3 个 Form + EmbedFormInTab → `WireComponentEvents()` 绑定 Form 暴露的事件）：

**`_showingSystemLog`/`_showingNormalLog` 隐式协议：** 这两个布尔字段仍在 MainForm（Tab 切换时设置 :321-322），用于 `UpdateLogCount()` 和 `OnLogReceived` 中判断 isActiveView。它们是 MainForm 和 Form 之间的隐式协议——Form 不知道它们存在，MainForm 通过它们决定是否通知 Form 更新。这是可接受的设计，但必须明确记录。滚动按钮在各 Form 内部管理 AutoScroll 状态，MainForm 只通过事件感知状态变化。

```csharp
_networkLogForm = new NetworkLogForm(_deviceLogs, _allLogs, _settings, () => _currentDeviceId);
EmbedFormInTab(_networkLogForm, _tabNetwork);

_normalLogForm = new NormalLogForm(_deviceNormalLogs, _allNormalLogs, _settings, () => _currentDeviceId);
EmbedFormInTab(_normalLogForm, _tabNormal);

// SystemLogForm 需在 InitializeSystemLogRuntime() 之后创建（该调用在 WireComponentEvents 内部 :303）
_systemLogForm = new SystemLogForm(_systemLogStore, _settings, () => _currentDeviceId, _adbSerialToDeviceId);
EmbedFormInTab(_systemLogForm, _tabSystem);
```

> **创建时机说明：** `InitializeSystemLogRuntime()` 在 `WireComponentEvents()` 中调用（MainForm.cs:303），`ConfigureLogLists()`/`ConfigureNormalLogList()`/`ConfigureSystemLogList()` 也在 `WireComponentEvents()` 中调用（:379-381），这些方法操作 `_lstNetworkLogs` 等控件。搬迁后这些控件在各 Form 内部，ConfigureXxx 方法也移入 Form。因此 Form 必须在 `WireComponentEvents()` 之前创建并嵌入，但 `WireComponentEvents()` 又依赖 Form 实例来绑定事件。

辅助方法（非 static，确保 Form 实例可追踪生命周期）：
```csharp
private void EmbedFormInTab(Form form, TabPage tab)
{
    form.TopLevel = false;
    form.FormBorderStyle = FormBorderStyle.None;
    form.Dock = DockStyle.Fill;
    tab.Controls.Clear();
    tab.Controls.Add(form);
    form.Show();
}
```

**Form 生命周期管理：** 内嵌的 Form 实例需在 MainForm 的 `Dispose(bool)` 中显式 Dispose，否则不会随 MainForm 关闭而释放，可能造成 GDI 对象泄漏。

- [ ] **Step 3: MainForm.cs — 重构事件绑定**

将原 `WireComponentEvents()` 中的 Tab 相关事件改为委托给 Form：
- `_lstNetworkLogs.SelectedIndexChanged` → `_networkLogForm.LogEntrySelected`
- `_lstNetworkLogs.DoubleClick` → `_networkLogForm.LogEntryDoubleClicked`
- 网络日志滚动/过滤按钮 → 已在 NetworkLogForm 内部绑定
- 系统日志 Pause/Resume → 已在 SystemLogForm 内部绑定

**ApplyLanguage() 适配：** `ApplyLanguage()` (MainForm.cs:254-296) 当前统一设置三个 Tab 的语言文本，包括 FilterPanel 的 `ApplyLanguage`/`SetFilter1Items`/`SetFilter2Items`、滚动按钮文本、Pause 按钮文本、日志计数 Label。搬迁后这些控件在各 Form 内部，`ApplyLanguage()` 无法直接操作。各 Form 需暴露 `ApplyLanguage()` 公共方法，MainForm 的 `ApplyLanguage()` 调用 `_networkLogForm.ApplyLanguage()` 等。这也是测试计划遗漏的验证点：设置对话框修改语言/字体后，各 Form 的语言文本和字体是否正确更新。

**MainForm 仍需监听的事件（需 Form 暴露）：**
- Tab 切换时调用 `_networkLogForm.RefreshNetworkFilter()` 等
- 设备切换时调用各 Form 的刷新方法
- 日志接收时调用各 Form 的增量追加方法

**跨 Form 协调方法（Form 必须暴露）：**
- `ClearFilterAndRefresh()` — `OnDeviceSelected()` (MainForm.cs:1069-1085) 调用 `RefreshNetworkFilter()` + `RefreshNormalFilter()` + `RefreshSystemLogList()` + `ShowLogDetail(null)` + `RefreshMirrorPanelState()`。搬迁后前三者需委托给 Form。同理 `OnDeleteDevice()` (:1091-1120) 和 `OnClear()` (:1311-1342) 也调用了这些方法 + 清理 `_filteredNetworkIndices/_filteredNormalIndices`（搬迁后这些字段在各 Form 内部），MainForm 无法直接清理。
- `OnBufferResized()` / `RebuildFilter()` — `OnSettingsClick()` (MainForm.cs:1351-1383) 在设置变更后调用 `_allLogs.Resize()` / `_allNormalLogs.Resize()` / `_systemLogStore.UpdateHotCapacity()` + `RefreshSystemLogList()`。RingBuffer Resize 是破坏性操作（可能丢弃尾部数据），搬迁后 Form 必须在 Resize 后立即重建过滤索引。

- [ ] **Step 4: MainForm.cs — 重构 OnLogReceived / OnNormalLogReceived / OnSystemLogReceived**

这三个方法目前直接操作 UI 控件，改造后 MainForm 保留数据写入，UI 更新委托给 Form：

```csharp
// OnLogReceived 中（MainForm 保留数据写入 + 计算 isActiveView）：
// 1. 判断 showingCurrentNetwork（依赖 _showingSystemLog + _currentDeviceId）
// 2. 计算追加前的 activeViewCountBeforeAdd 和 activeViewWasFull
// 3. 写入 _deviceLogs[id].Add(entry) 和 _allLogs.Add(entry)
// 4. 更新 _devicePanel.UpdateLogCount()
// 5. 委托 Form 处理 UI 更新：
var isActiveView = showingCurrentNetwork;
_networkLogForm.OnLogAdded(entry, isActiveView, activeViewCountBeforeAdd, activeViewWasFull);

// OnNormalLogReceived 中：同理
_normalLogForm.OnNormalLogAdded(entry, isActiveView, ...);

// OnSystemLogReceived 中：入队逻辑留 MainForm（见 Task 2 线程安全设计决策）
_systemLogForm.ProcessPendingLogs(pendingLogs);
```

> **数据流拆分设计：** 不能简单拆为 `_networkLogForm.AppendLogEntry()`，因为原方法中数据写入和 UI 更新是耦合的。MainForm 保留数据写入和"是否当前视图"判断（依赖 `_showingSystemLog` 状态），通过 `Form.OnLogAdded(entry, isActiveView)` 接口将 UI 更新委托给 Form。Form 内部根据 `isActiveView` 决定增量/全量刷新和 AutoScroll 行为。

**OnLogReceived 数据流拆分流程：**

```mermaid
flowchart TD
    A[LogServer.LogReceived 后台线程] --> B[MainForm.BeginInvoke]
    B --> C{showingCurrentNetwork?}
    C -->|计算| D[activeViewCountBeforeAdd / activeViewWasFull]
    B --> E[_deviceLogs[id].Add entry]
    B --> F[_allLogs.Add entry]
    B --> G[_devicePanel.UpdateLogCount]
    E & F & G --> H[MainForm 保留数据写入]
    H --> I[_networkLogForm.OnLogAdded entry, isActiveView]
    I --> J[Form 内部决定增量/全量刷新]
    I --> K[Form 内部判断 AutoScroll]
    J & K --> L[Form 更新自有 ListView]
    C -.->|依赖 _showingSystemLog| M[isActiveView 由 MainForm 计算]
    M --> I
```

- [ ] **Step 5: 删除三个 partial class 文件**

- 删除 `MainForm.NetworkLogs.cs`
- 删除 `MainForm.NormalLogs.cs`
- 删除 `MainForm.SystemLogs.cs`

从 MainForm.cs 中移除对应的 `#region` 占位符和注释。

**不可简单删除的依赖处理：**
- `OnFormClosing()` (:1390-1416) 直接访问 `_systemSnapshotCts`/`_systemPrefetchCts`（定义在 SystemLogs.cs）来 Cancel+Dispose。搬迁后这些字段在 SystemLogForm 内部，SystemLogForm 需暴露 `CancelAsyncOperations()` 公共方法，或在 Form 自己 Dispose 时自行清理。
- `ProcessCmdKey()` (:1422-1446) 直接操作三个 ListView 和三个 AutoScroll 标志。搬迁后必须改为委托给当前活动 Form 的公共方法（如 `activeForm.HandleEndKey()`）。

- [ ] **Step 6: 构建验证 + 手动 smoke**

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
rtk dotnet run --project .\LogViewer
```

验证：Tab 切换正常、日志实时显示、过滤/滚动/右键菜单/导出功能正常。

**回归断言清单（操作-预期对）：**
1. 启动 → 网络日志 Tab 显示空列表 → 切到系统日志 Tab → 切回网络日志 → 列表仍为空
2. 连接设备 → 网络日志实时追加 → 切到普通日志 → 收到普通日志 → 切回网络 → 日志未丢失
3. 网络日志列表 MouseWheel → AutoScroll 关闭 → 滚动到底部按钮变灰 → 点 End 键 → AutoScroll 恢复 → 按钮变蓝
4. 右键网络日志 → 复制 URL → 粘贴到记事本验证
5. 点导出 JSON → 文件内容正确
6. 双击网络日志 → JsonDetailForm 弹出 → 关闭 JsonDetailForm → 主窗口右键菜单仍正常
7. 设置对话框改字体 → 应用 → 三个 Tab 列表字体都更新

---

## 五、测试计划

- 本地构建：`rtk dotnet build .\LogViewer\LogViewer.csproj`
- WinForms smoke：启动程序，验证三种日志 Tab 的显示、过滤、滚动、右键菜单
- Designer 验证：Rider 分别打开 NetworkLogForm / NormalLogForm / SystemLogForm / MainForm，确认设计器正常
- 回归范围：仅 UI 层内部重构，对 Models/Network/Utils/Static 零影响

**额外验证场景：**
1. **End 键快捷键** — `ProcessCmdKey` 处理 End 键滚动到底部，搬迁后需验证 End 键在各 Tab 下是否仍生效
2. **Ctrl+C 复制** — NormalLogs 的 Ctrl+C 复制 Message（MainForm.cs:393-399），键盘事件是否正确路由到内嵌 Form
3. **窗口 Resize + Splitter 移动** — 内嵌 Form 的 Dock=Fill 是否正确跟随
4. **设置变更后语言/字体更新** — `ApplySettings()` 是否正确传播到各 Form
5. **多设备场景** — 设备切换后三个 Form 的缓冲区切换是否正确，OnDeleteDevice 后其他 Form 是否正常
6. **非模态 JsonDetailForm 打开后右键菜单** — 已知 `ToolStripManager.ModalMenuFilter` 问题（MEMORY.md），内嵌 Form 是否加剧

---

## 六、附录

### A. 文件清单

| 文件路径 | 操作 | 行数估计 | 说明 |
|----------|------|---------|------|
| `UI/NetworkLogForm.cs` | 新增 | ~300 | 网络日志 Form 手写逻辑 |
| `UI/NetworkLogForm.Designer.cs` | 新增 | ~150 | 网络日志 Form 控件树 |
| `UI/NormalLogForm.cs` | 新增 | ~250 | 普通日志 Form 手写逻辑 |
| `UI/NormalLogForm.Designer.cs` | 新增 | ~130 | 普通日志 Form 控件树 |
| `UI/SystemLogForm.cs` | 新增 | ~800 | 系统日志 Form 手写逻辑 |
| `UI/SystemLogForm.Designer.cs` | 新增 | ~150 | 系统日志 Form 控件树 |
| `UI/MainForm.cs` | 修改 | ~-200 | 移除已迁移逻辑，新增 Form 嵌入 |
| `UI/MainForm.Designer.cs` | 修改 | ~-300 | 移除已迁移控件声明 |
| `UI/MainForm.NetworkLogs.cs` | 删除 | -502 | 逻辑已迁移至 NetworkLogForm |
| `UI/MainForm.NormalLogs.cs` | 删除 | -294 | 逻辑已迁移至 NormalLogForm |
| `UI/MainForm.SystemLogs.cs` | 删除 | -1011 | 逻辑已迁移至 SystemLogForm |
| `UI/BufferedListViewHelper.cs` | 修改 | ~+40 | 添加 `GetApproxVisibleRowCount()` 和 `ScrollToBottom/ScrollToTop/IsAtBottom` 三个静态方法（从 MainForm.cs:1206-1246 搬入），三个 Form 都需引用 |
| `UI/MainForm.Preview.cs` | 修改 | ~+30 | `ShowLogDetail()` 从 NetworkLogs.cs 移入（它操作的全是预览面板控件） |
| `Static/Language.cs` | 可能修改 | ~+10 | 各 Form 的 `ApplyLanguage()` 需引用 Language 常量，可能需新增面向 Form 的语言方法 |

**预计总改动：** 新增 ~1780 行，修改 ~-500 行，删除 ~1807 行

> **行数估算修正：** 上述估算偏乐观，未计入以下新增工作量：
> 1. **接口/事件定义** — Form 暴露的公共方法、事件、属性（`OnLogAdded`/`RefreshFilter`/`IsAutoScrollActive`/`LogCountChanged` 等），约 ~100 行
> 2. **MainForm 适配代码** — Task 3 的协调逻辑（Tab 切换委托、设备切换通知、UpdateLogCount 重写、导出逻辑重拆分），约 ~150 行
> 3. **焦点/键盘桥接** — ProcessTabKey 重写等修复，约 ~30 行
> 实际新增约 ~2060 行，比原估算多 ~280 行。

### B. 已确认的决策记录

| # | 决策 | 结论 | 日期 |
|---|------|------|------|
| 1 | Form vs UserControl | Form — 老板指定 | 2026-06-27 |
| 2 | 数据共享方式 | 构造函数注入引用（非拷贝） | 2026-06-27 |
| 3 | 事件通信方向 | Form → MainForm 通过 event | 2026-06-27 |
| 4 | Form 内嵌方式 | TopLevel=false + FormBorderStyle=None | 2026-06-27 |
| 5 | 预览面板归属 | 预留 MainForm（NetworkLogForm 通过事件通知 MainForm 更新预览） | 2026-06-27 |
| 6 | ScrollToBottom/ScrollToTop/IsAtBottom 辅助方法 | 提取到 `BufferedListViewHelper` 共享工具类（各 Form 独立副本亦可，但推荐共享） | 2026-06-27 |
| 7 | _selectedLogEntry 字段归属 | 留在 MainForm，Form 通过事件传递选中项 | 2026-06-27 |
| 8 | ProcessCmdKey 的 End 键处理 | 委托给活动 Form 的公共方法（如 `activeForm.HandleEndKey()`） | 2026-06-27 |
| 9 | OnFormClosing 中的 CTS 清理 | SystemLogForm 自行管理 _systemSnapshotCts/_systemPrefetchCts 生命周期 | 2026-06-27 |

### C. 风险与缓解

| 风险 | 缓解 |
|------|------|
| Form 内嵌 TabPage 后 Designer 可能不渲染子 Form | 设计器只看各 Form 自身，运行时动态嵌入 |
| 共享数据引用可能导致跨线程访问 | 保持现有 BeginInvoke 模式，Form 内部不新增后台线程 |
| SystemLogForm 最复杂（1011行），搬迁可能引入 bug | 逐方法搬迁，每步构建验证 |
| Tab 切换后内嵌 Form 的 Resize 不触发 | Tab 切换事件中主动调 `form.PerformAutoScale()` 或 `form.Invalidate()` |
| 设计期 IsDesignTimeMode 检查 | 各 Form 构造函数中运行时逻辑需加 `LicenseManager.UsageMode == LicenseUsageMode.Designtime` 守卫 |
| MainForm.Preview.cs 依赖 | **低优先级**：`OnToggleDetailView()`/`SyncDetailViewVisibility()` 只操作预览面板控件，不访问 `_lstNetworkLogs.Focused`。但 `ShowLogDetail()` 定义在 NetworkLogs.cs，搬迁后需移入 Preview.cs 或 MainForm.cs |
| 设备切换时多 Form 协调 | `OnDeviceSelected` 同时影响 3 个 Form，需 MainForm 主动通知刷新，注意顺序和延迟 |

---

## 当前阶段跟踪

| 阶段 | 状态 | 开始时间 | 完成时间 | 备注 |
|------|------|---------|---------|------|
| Task 0 | 未开始 | - | - | NetworkLogForm |
| Task 1 | 未开始 | - | - | NormalLogForm |
| Task 2 | 未开始 | - | - | SystemLogForm |
| Task 3 | 未开始 | - | - | MainForm 改造 |

**Task 完成清单：**

- [ ] Task 0: 创建 NetworkLogForm
- [ ] Task 1: 创建 NormalLogForm
- [ ] Task 2: 创建 SystemLogForm
- [ ] Task 3: 改造 MainForm
- [ ] Task 4: 文档更新

**当前进行阶段：** 规划中

---

**创建时间**：2026-06-27
**状态**：规划中
