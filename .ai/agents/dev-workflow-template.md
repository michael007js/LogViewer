D:\youzyapp\android.log.client

@AGENTS


请为「LogViewer」创建计划：

背景：因App接入日志发送需要在各个地方埋代码，不方便统一管理与维护，因此需要创建一个计划，统一管理日志发送的位置。


核心需求：

一、Android Java客户端：
  1.通过反射获取com.eagersoft.core.utils.LogUtils 的 private static void log(int type, boolean showLeftBorder, String tag, Object... contents) 方法
  2.在 private static void log(int type, boolean showLeftBorder, String tag, Object... contents) 方法内部第一行调用 LogViewerSender.getInstance().send(LogViewerData data) 方法， 这样即不影响原来的日志打印，又可以将日志发送到LogViewerSender
  3.为LogViewerData新增一个类型type系统：通过private static void log(int type, boolean showLeftBorder, String tag, Object... contents)中识别contents中如果包含“网络请求[---]执行完毕,执行日志如下”的则代表是网络日志（TYPE_NETWORK_LOG），否则为普通日志（TYPE_NORMAL_LOG）
  4.发送到C#服务端后解析type字段，根据type字段将日志分类展示到对应的tab中

  附录：
    Android Java客户端项目地址：D:\youzyapp\youzy.mobile.android\
    OkHttp3Utils地址：D:\youzyapp\youzy.mobile.android\app\src\main\java\com\eagersoft\youzy\youzy\data\retrofit\OkHttp3Utils.java
    MyApplication地址：D:\youzyapp\youzy.mobile.android\app\src\main\java\com\eagersoft\youzy\youzy\application\MyApplication.java
    模块目录：D:\youzyapp\youzy.mobile.android\youdebug\src\main\java\com\eagersoft\youzy\youzy\debug\sender

二、C#服务端：
  1.接收Android Java客户端发送的日志数据
  2.重构TCP模块，新增日志分类，包括Network日志（TYPE_NETWORK_LOG）、普通日志（TYPE_NORMAL_LOG）、ADB系统日志不需要改动
  3.现有Network Logs与System Logs两个tab，分别展示网络日志与ADB系统日志，现要求在Network Logs tab后面加一个tab，展示普通日志（TYPE_NORMAL_LOG），并将其命名为Normal Logs，其他tab保持不变。
  4.Network Logs 展示网络日志（TYPE_NETWORK_LOG），Normal Logs 展示普通日志（TYPE_NORMAL_LOG），System Logs 展示ADB系统日志（无需改动）

  附录：
    C#服务端项目地址：D:\youzyapp\LogViewer

三、项目完成后需要更新两端相关文档

请按照 plans/template/plan-template-dotnet.md 格式输出到D:\youzyapp\android.log.client.ai\plans中。
文件名叫plan-appform-v3.md
计划文件需要使用 Rider MCP tools 直接读写文本方式并分段写入。

｛plan.md｝为D:\youzyapp\android.log.client\.ai\plans\plan-ConfigStore-v1-exec.md
直接在 ｛plan.md｝ 里所有觉得有问题的地方使用 Rider MCP tools 直接读写文本方式加批注：

| 批注类型                     | 说明                     |
|--------------------------|------------------------|
| 纠正错误假设                   | 指出计划中不符合实际的地方          |
| 否决不合理方案                  | 明确表示某个方案不可行            |
| 补充约束条件                   | 添加技术或业务约束              |
| 画流程图解释                   | 有疑惑直接让 Agent 画流程图解释并思考 |
| 标记 "don't implement yet" | 标记暂不实施的部分              |

｛plan.md｝D:\youzyapp\android.log.client\.ai\plans\plan-ConfigStore-v1-exec.md
我在你的{plan.md} 里加了点批注，你使用 Rider MCP tools 直接读写文本方式解决一下批注，但不要执行计划，执行前需要先与我确认

｛plan.md｝为D:\youzyapp\LogViewer\.ai\plans\2026-06-26-normal-logs-tab.md
帮我使用 Rider MCP tools 直接读写文本方式生成一份基于{plan.md} 的不带批注的最终可执行计划到D:\youzyapp\android.log.client.ai\plans中

｛plan.md｝为D:\youzyapp\LogViewer\.ai\plans\2026-06-26-normal-logs-tab.md
按照@agents\reviews\review-template.md 帮我使用mcp方式生成一份基于 {plan.md} 产物的review报告

D:\youzyapp\youzy.mobile.android\.ai\reviews\2026-06-26-normal-logs-tab-review.md
{review.md} 这是你的review报告，使用mcp方式解决一下存在的问题

根据 @lib\ui\college\accommodation\campus_accommodation_page.dart 的结构 按照 @lib\ui\components\app_skeleton.dart 为模版
创建骨架屏