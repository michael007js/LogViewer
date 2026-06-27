D:\youzyapp\LogViewer\LogViewer

@AGENTS


请为「LogViewer」创建计划：

描述：因form UI维护需要，将三个tab【网络日志、普通日志、系统日志】 封装成三个form，需要支持设计器友好显示，因为需要在设计器中调整UI。


核心需求：

1. 在D:\youzyapp\LogViewer\LogViewer\UI下创建 NetworkLogForm、NormalLogForm、SystemLogForm，三个用户界面
2. 搬运 D:\youzyapp\LogViewer\LogViewer\UI\MainForm.NetworkLogs.cs、D:\youzyapp\LogViewer\LogViewer\UI\MainForm.NormalLogs.cs、D:\youzyapp\LogViewer\LogViewer\UI\MainForm.SystemLogs.cs中的代码到对应的NetworkLogForm、NormalLogForm、SystemLogForm中
3. 设计器友好支持
4. 项目完成后需要更新两端相关文档

请按照 plans/template/plan-template-dotnet.md 格式输出到D:\youzyapp\LogViewer\.ai\plans中。
文件名你自己起
计划文件需要使用 Rider MCP tools 直接读写文本方式并分段写入。

｛plan.md｝为D:\youzyapp\LogViewer\.ai\plans\plan-ConfigStore-v1-exec.md
直接在 ｛plan.md｝ 里所有觉得有问题的地方使用 Rider MCP tools 直接读写文本方式加批注：

| 批注类型                     | 说明                     |
|--------------------------|------------------------|
| 纠正错误假设                   | 指出计划中不符合实际的地方          |
| 否决不合理方案                  | 明确表示某个方案不可行            |
| 补充约束条件                   | 添加技术或业务约束              |
| 画流程图解释                   | 有疑惑直接让 Agent 画流程图解释并思考 |
| 标记 "don't implement yet" | 标记暂不实施的部分              |

｛plan.md｝D:\youzyapp\LogViewer\.ai\plans\plan-ConfigStore-v1-exec.md
我在你的{plan.md} 里加了点批注，你使用 Rider MCP tools 直接读写文本方式解决一下批注，但不要执行计划，执行前需要先与我确认

｛plan.md｝为D:\youzyapp\LogViewer\.ai\plans\2026-06-26-normal-logs-tab.md
帮我使用 Rider MCP tools 直接读写文本方式生成一份基于{plan.md} 的不带批注的最终可执行计划到D:\youzyapp\LogViewer\.ai\plans中

｛plan.md｝为D:\youzyapp\LogViewer\.ai\plans\2026-06-26-normal-logs-tab.md
按照@agents\reviews\review-template.md 帮我使用mcp方式生成一份基于 {plan.md} 产物的review报告

D:\youzyapp\youzy.mobile.android\.ai\reviews\2026-06-26-normal-logs-tab-review.md
{review.md} 这是你的review报告，使用mcp方式解决一下存在的问题

根据 @lib\ui\college\accommodation\campus_accommodation_page.dart 的结构 按照 @lib\ui\components\app_skeleton.dart 为模版
创建骨架屏