---
name: "dotnet-winforms-guidelines"
description: ".NET WinForms 窗体显示与预览行为规范。Invoke when working on .NET 8 / net8.0-windows Windows Forms projects and need guidance for showing forms, opening modal or non-modal windows, preview behavior, window ownership, positioning, loading lifecycle, resize behavior, closing strategy, and cleanup."
version: "1.0.0"
updatedAt: "2026-06-01"
tags: [WinForms, 窗体, 模态, 非模态, 预览, Designer, 布局, DPI]
---

# .NET WinForms 窗体显示与预览行为规范

适用范围：`.NET 8`、`net8.0-windows`、`Windows Forms`。当前仓库以 `LogViewer.csproj` 的 `net8.0-windows` 为准。

适用边界：

- 本技能聚焦"窗体显示与交互模式"。
- 本技能不覆盖 MDI、多文档布局、复杂跨线程调度。
- 若问题核心是布局系统、绘图或架构分层，应交给对应专项规范处理。
- 调用本技能时，必须同时加载 `rtk` 公共前置技能，不得跳过。

## 项目控件约束

- 在 `LogViewer` 项目内，新窗体继承 `Form`，手写逻辑放在非 Designer 的 partial 类中。
- 自绘控件走 `OnPaint` 离屏绘制路径，颜色使用 `SystemColors` 或项目定义的语义常量。
- 自绘控件放 `UI/` 目录，数据模型放 `Models/`，通信层放 `Network/`，工具类放 `Utils/`。
- 手写窗体逻辑放在非 Designer 的 partial 类中，禁止把手写逻辑写进 `*.Designer.cs`。

## 适用场景

本技能适用于 WinForms 中几乎所有"窗体如何显示、如何交互、如何关闭、如何回收"的场景，包括但不限于：

- 打开编辑窗体、详情窗体、设置窗体、选择窗体、确认窗体、向导窗体
- 打开只读预览窗体、日志窗体、状态窗体、监控窗体、工具窗体、辅助面板
- 决定使用 `ShowDialog(this)`、`Show(this)`、`Show()` 中的哪一种模式
- 判断当前场景应使用模态、非模态、单实例工具窗体还是临时对话框
- 处理 Owner、焦点、窗口激活、最小化恢复、窗口置前、居中与首次显示位置
- 处理重复打开同类窗体时的单实例缓存、激活已有窗口、避免重复 `new`
- 处理窗体输入参数传递、结果返回、`DialogResult`、只读属性回传、结果 DTO 回传
- 处理预览窗体与编辑窗体的职责划分，避免把预览和编辑混在一起
- 处理窗体关闭确认、取消关闭、提交失败后保持窗体打开、加载失败后的错误提示
- 处理模态窗体释放、非模态窗体引用清理、事件解绑、资源回收、泄漏规避
- 处理 UI 线程显示窗体、`Load` / `Shown` 异步加载、加载中占位和 Busy 状态
- 处理窗体尺寸变化、拉伸、最小尺寸、按钮区域固定、主内容区自适应
- 处理 `Dock`、`Anchor`、布局容器在预览窗体和普通窗体中的使用约束
- 处理 DPI 缩放、最大化、还原后内容区仍可访问的显示约束

## 1. 模态窗体显示

用于需要用户先完成操作、确认结果后再继续当前流程的场景。

优先做法：

- 使用 `ShowDialog(owner)`，不要丢失拥有者窗体
- 临时对话框优先使用 `using`
- 用 `DialogResult` 返回用户操作结果

```csharp
using var form = new DeviceEditForm(deviceId);
form.StartPosition = FormStartPosition.CenterParent;

if (form.ShowDialog(this) == DialogResult.OK)
{
    ReloadDevices();
}
```

适合场景：

- 新增/编辑
- 确认操作
- 参数输入
- 只允许单步完成的预览确认

## 2. 非模态窗体显示

用于允许用户一边操作主窗体、一边查看辅助信息的场景。

优先做法：

- 使用 `Show(owner)` 或 `Show()`
- 避免每次点击都创建新实例
- 已打开时优先 `Activate()` 或恢复窗口

```csharp
private LogViewerForm? _logViewerForm;

private void ShowLogViewer()
{
    if (_logViewerForm is { IsDisposed: false })
    {
        if (_logViewerForm.WindowState == FormWindowState.Minimized)
        {
            _logViewerForm.WindowState = FormWindowState.Normal;
        }

        _logViewerForm.Activate();
        return;
    }

    _logViewerForm = new LogViewerForm();
    _logViewerForm.FormClosed += (_, _) => _logViewerForm = null;
    _logViewerForm.Show(this);
}
```

适合场景：

- 日志窗口
- 实时监控窗口
- 辅助工具窗口
- 长时间存在的状态面板

## 3. 预览窗体显示

预览窗体默认应当是只读的，重点是"展示结果"，不是"编辑数据"。

除非用户任务本质是"审阅后确认"，否则不要在预览窗体中承载编辑职责。

优先做法：

- 通过构造函数传入预览数据或 ViewModel
- 标题显示预览对象名称
- 预览区域使用只读控件
- 若预览后还要确认，使用模态窗体
- 不要把"编辑 + 保存"逻辑混进预览窗体

```csharp
using var previewForm = new ReportPreviewForm(reportViewModel);
previewForm.StartPosition = FormStartPosition.CenterParent;
previewForm.ShowDialog(this);
```

推荐场景：

- 报表预览
- 文本/配置预览
- 图片或截图预览
- 操作前结果确认

## 4. Owner 与窗口定位

优先做法：

- 子窗体优先指定 Owner
- 子对话框优先 `CenterParent`
- 应用首次主窗体可使用 `CenterScreen`
- 不要手写像素坐标做普通居中

```csharp
using var settingsForm = new SettingsForm();
settingsForm.StartPosition = FormStartPosition.CenterParent;
settingsForm.ShowDialog(this);
```

说明：

- `CenterParent` 更符合桌面应用交互习惯
- 没有 Owner 的弹窗更容易丢到后台或焦点异常

## 5. 数据传递与结果返回

优先做法：

- 输入数据走构造函数、属性初始化或工厂方法
- 返回结果优先用 `DialogResult`、只读属性或结果 DTO
- 不要让子窗体直接操作父窗体控件

```csharp
using var form = new RenameDeviceForm(currentName);

if (form.ShowDialog(this) == DialogResult.OK)
{
    var newName = form.DeviceName;
    RenameDevice(newName);
}
```

避免：

- `owner.Controls["xxx"]` 这类跨窗体直接取控件
- 子窗体里直接改父窗体状态字段

## 6. 生命周期与资源释放

优先做法：

- 模态窗体优先 `using`
- 非模态窗体关闭后清空引用
- 订阅的事件在关闭时解绑
- 不再使用的窗体及时 `Close()`
- 若子窗体订阅了长生命周期对象事件，关闭时必须解绑

```csharp
private PreviewForm? _previewForm;

private void OpenPreview()
{
    _previewForm = new PreviewForm();
    _previewForm.FormClosed += OnPreviewFormClosed;
    _previewForm.Show(this);
}

private void OnPreviewFormClosed(object? sender, FormClosedEventArgs e)
{
    if (_previewForm is not null)
    {
        _previewForm.FormClosed -= OnPreviewFormClosed;
        _previewForm = null;
    }
}
```

说明：

- 若子窗体订阅了服务、单例、主窗体或其他长生命周期对象事件，关闭时必须解绑，否则窗体可能无法被 GC 回收。
- 对非模态窗体，`FormClosed` 是清理引用、解绑事件、释放外部句柄的首选时机。

## 7. 线程与加载时机

必须遵守：

- 窗体创建和显示必须在 UI 线程执行
- 不要在后台线程直接 `Show()` / `ShowDialog()`
- `Load` 只做轻量同步初始化；首次异步加载优先放在 `Shown` 或显式的可取消初始化入口
- 不要在 `Load` / `Shown` 中做长时间阻塞工作

推荐做法：

- 先显示窗体，再异步加载数据
- 为首次加载预留 `CancellationToken`
- 加载期间显示 Busy 状态或占位内容

```csharp
private CancellationTokenSource? _loadCts;

private async void PreviewForm_Shown(object? sender, EventArgs e)
{
    if (_loadCts is not null)
    {
        return;
    }

    _loadCts = new CancellationTokenSource();
    loadingLabel.Visible = true;

    try
    {
        previewTextBox.Text = await _previewService.LoadTextAsync(_loadCts.Token);
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
        loadingLabel.Visible = false;
    }
}

protected override void OnFormClosed(FormClosedEventArgs e)
{
    _loadCts?.Cancel();
    _loadCts?.Dispose();
    _loadCts = null;
    base.OnFormClosed(e);
}
```

## 8. 异常与关闭策略

优先做法：

- 预览加载失败时，在窗体内给出明确错误信息，不要静默失败。
- 模态提交失败时，优先保留窗体打开状态，让用户修正后继续提交。
- 需要阻止用户误关窗体时，在 `FormClosing` 中显式判断并给出确认。
- 非模态窗体关闭时，只做清理与解绑；不要在关闭路径里塞复杂业务提交。

```csharp
private void EditorForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    if (!_isSaved && e.CloseReason == CloseReason.UserClosing)
    {
        var result = MessageBox.Show(
            this,
            "尚未保存，确认关闭？",
            "提示",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
        }
    }
}
```

## 9. 窗体尺寸变化与布局约束

必须遵守：

- 窗体尺寸变化时，控件位置和尺寸必须保持可用，不能出现遮挡、越界、重叠或操作区丢失。
- 不要依赖 `Resize` 事件里手写大量像素坐标去维持常规布局。
- 预览窗体在缩放后，核心内容区、关闭按钮、确认按钮必须仍可访问。

推荐做法：

- 优先使用 `Dock`、`Anchor`、`TableLayoutPanel`、`FlowLayoutPanel` 处理布局变化。
- 需要最小可用区域的窗体，设置 `MinimumSize`。
- 预览区域优先独立成主内容区，底部操作按钮固定在单独区域。
- 若窗口允许拉伸，先验证最小尺寸、最大化、还原、DPI 缩放后的显示效果。
- 启用了 `AutoScale` / 自动缩放时，额外验证缩放后控件是否仍可见、可点、可滚动、可完整显示文本。

```csharp
previewTextBox.Dock = DockStyle.Fill;
buttonPanel.Dock = DockStyle.Bottom;

btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
btnConfirm.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

MinimumSize = new Size(800, 500);
```

## 10. 常见显示模式

### 编辑窗体

- 用 `ShowDialog(this)`
- 关闭后根据 `DialogResult.OK` 刷新列表或详情

### 详情窗体

- 若只读且允许多任务并行，可用 `Show(this)`
- 若详情必须阻塞当前操作，可用 `ShowDialog(this)`

### 预览窗体

- 内容只读
- 标题清晰
- 支持复制、关闭、确认

### 工具窗体

- 单实例优先
- 重复打开时只激活，不重复 new

## 11. 不推荐的做法

```csharp
// 没有 owner，容易焦点混乱
new SettingsForm().ShowDialog();

// 模态窗体不释放
var editForm = new DeviceEditForm(deviceId);
editForm.ShowDialog(this);

// 每次都 new 一个非模态窗体
private void btnLog_Click(object sender, EventArgs e)
{
    new LogViewerForm().Show();
}

// 子窗体直接操作父窗体控件
owner.txtName.Text = "changed";

// 用 Resize 手写大量坐标修布局
private void PreviewForm_Resize(object sender, EventArgs e)
{
    previewTextBox.Width = Width - 137;
    btnClose.Left = Width - 120;
}

// 窗体直接继承 Form（本项目无主题基类）
public sealed class MyForm : Form { }
```

## 12. 决策表

| 问题 | 推荐模式 |
|------|----------|
| 阻塞当前流程并收集结果 | `ShowDialog(this)` |
| 需要避免主窗体被误操作 | `ShowDialog(this)` |
| 辅助观察，不阻塞主界面 | `Show(this)` |
| 同类工具窗口重复打开 | 单实例缓存 + `Activate()` |
| 窗口可能已最小化 | 先恢复 `WindowState = Normal`，再 `Activate()` |
| 临时交互完成即销毁 | `using` + `DialogResult` |
| 需要结果确认的预览 | 模态预览窗体 |
| 需要持续观察的预览 | 非模态单实例窗体 |
| 非模态窗体关闭后的资源处理 | `FormClosed` 清理引用 + 解绑事件 |
| 窗体允许拉伸 | `Dock` / `Anchor` / 布局容器 + `MinimumSize` |

## 13. 项目窗体基类说明

当前项目 `LogViewer` 窗体直接继承 `Form`（本项目无主题系统基类）。

窗体应遵循本技能前文规则：

- 首次显示后再异步加载
- `Load` 只做轻量初始化，首次异步加载走 `Shown`
- 优先使用 `Dock` / `Anchor` / 布局容器
- 提供 Busy 状态、错误反馈、关闭清理扩展点
- 保持最小抽象，不混入具体业务逻辑
