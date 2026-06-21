# Plan 快速指南

面向 LogViewer 的 `.NET 8 / WinForms / TCP通信 / ADB日志` 计划创建速查。

## 一句话创建 Plan

```text
请为新功能「{{功能}}」创建开发 Plan，设计稿：{{MasterGo 链接}}
```

---

## 完整提示词模板

参见：`ai/plans/plan-prompt-template.md`

- 本仓库内实际用于落地的 Plan 正文模板是：`ai/plans/plan-template-dotnet.md`
- 你给出的 `plans/template/plan_prompt_template.md`，在本仓库里对应的就是上面这个文件

---

## 创建流程（AI 视角）

```text
用户需求 → 获取设计稿(getDsl) → 保存截图 → 创建 Plan → 等待确认
```

**关键步骤：**

1. 有设计稿时，先用 `mcp__getDsl` 获取结构
2. 保存截图到 `ai/plans/screenshots/{{模块名}}/`
3. 按 `ai/plans/plan-template-dotnet.md` 填充
4. 列出相关 Skills

**补充说明：**

- `{{模块名}}` 优先使用仓库现有模块目录名，通常沿用 `PascalCase`，例如 `Iptv`、`FileOnlinePlayer`、`M3U8StreamGetter`
- 没有设计稿的任务，也可以直接按现有代码、配置、README 和宿主结构创建 Plan

**常见相关 Skills：**

- `rtk`（`.opencode/skills/rtk/`）
- `dotnet-guidelines`
- `donet-naming`
- `dotnet-winforms-guidelines`

---

## 常用场景

| 场景 | 提示词 |
|------|--------|
| 新功能 | `请为新功能「{{功能}}」创建开发 Plan，设计稿：{{MasterGo 链接}}` |
| 改造 | `改造 {{功能}}，现有代码：{{路径}}` |
| 纯设计 | `从设计稿创建 Plan：{{链接}}` |

---

## 输出位置

- Plan：`ai/plans/plan-{{模块名}}-v{{版本}}.md`
- 截图：`ai/plans/screenshots/{{模块名}}/`

---

**详细文档：** `ai/plans/plan-prompt-template.md`

