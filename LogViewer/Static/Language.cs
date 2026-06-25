namespace LogViewer.Static;

/// <summary>
/// UI 字符串常量集中管理类。所有界面文本（菜单、状态栏、按钮、错误提示、列标题等）
/// 均定义于此，避免硬编码散落在 UI 层各处，便于统一维护和国际化。
/// </summary>
public static class Language
{
    /// <summary>应用程序主窗口标题。</summary>
    public const string AppTitle = "Android Logcat [Authory By Michael 😏]";

    /// <summary>顶部菜单栏"工具"菜单项文本。</summary>
    public const string ToolsMenu = "工具";

    /// <summary>"设置..."菜单项文本。</summary>
    public const string SettingsMenu = "设置...";

    /// <summary>"ADB 反向代理"菜单/按钮文本。</summary>
    public const string AdbReverse = "ADB 反向代理";

    /// <summary>服务运行中状态文本。</summary>
    public const string Running = "运行中";

    /// <summary>通用错误提示文本。</summary>
    public const string Error = "错误";

    /// <summary>网络日志 Tab 页标题。</summary>
    public const string NetworkLogs = "网络日志";

    /// <summary>系统日志 Tab 页标题。</summary>
    public const string SystemLogs = "系统日志";

    /// <summary>"置顶"滚动按钮文本。</summary>
    public const string ScrollToTop = "置顶";

    /// <summary>"置底"滚动按钮文本。</summary>
    public const string ScrollToBottom = "置底";

    /// <summary>关键词搜索输入框占位文本。</summary>
    public const string KeywordPlaceholder = "关键词...";

    /// <summary>"全部"筛选选项文本。</summary>
    public const string All = "全部";

    /// <summary>"暂停"日志采集按钮文本。</summary>
    public const string Pause = "暂停";

    /// <summary>"继续"恢复采集按钮文本。</summary>
    public const string Resume = "继续";

    /// <summary>JSON 详情窗口"请求头"标签文本。</summary>
    public const string Headers = "请求头";

    /// <summary>JSON 详情窗口"请求体"标签文本。</summary>
    public const string RequestBody = "请求体";

    /// <summary>JSON 详情窗口"响应体"标签文本。</summary>
    public const string ResponseBody = "响应体";

    /// <summary>JSON 搜索框占位文本。</summary>
    public const string SearchJsonPlaceholder = "搜索 JSON...";

    /// <summary>"展开"JSON 节点按钮文本。</summary>
    public const string Expand = "展开";

    /// <summary>"折叠"JSON 节点按钮文本。</summary>
    public const string Collapse = "折叠";

    /// <summary>"二级"折叠级别按钮文本。</summary>
    public const string CollapseLevel2 = "二级";

    /// <summary>JSON 视图"原文"模式切换文本。</summary>
    public const string Raw = "原文";

    /// <summary>JSON 视图"树形"模式切换文本。</summary>
    public const string Tree = "树形";

    /// <summary>"清空"日志按钮文本。</summary>
    public const string Clear = "清空";

    /// <summary>"导出 JSON"按钮文本。</summary>
    public const string ExportJson = "导出 JSON";

    /// <summary>"导出 TXT"按钮文本。</summary>
    public const string ExportTxt = "导出 TXT";

    /// <summary>状态栏：服务未启动。</summary>
    public const string ServerStopped = "服务：未启动";

    /// <summary>状态栏：ADB 未检测到。</summary>
    public const string AdbNotDetected = "ADB：未检测到";

    /// <summary>设备面板占位提示：请选择设备以操控手机。</summary>
    public const string DeviceSelectPrompt = "请选择具体设备以操控手机";

    /// <summary>scrcpy 镜像区域占位文本。</summary>
    public const string ScrcpyHost = "scrcpy 镜像区域";

    /// <summary>"启动"按钮文本。</summary>
    public const string Start = "启动";

    /// <summary>"停止"按钮文本。</summary>
    public const string Stop = "停止";

    /// <summary>"重连"按钮文本。</summary>
    public const string Reconnect = "重连";

    /// <summary>"旋转"按钮文本（scrcpy 画面旋转）。</summary>
    public const string Rotate = "旋转";

    /// <summary>"截图"按钮文本。</summary>
    public const string Screenshot = "截图";

    /// <summary>"弹出"按钮文本（scrcpy 从内嵌切换为独立窗口）。</summary>
    public const string Popout = "弹出";

    /// <summary>"扫描 ADB"按钮文本。</summary>
    public const string ScanAdb = "扫描 ADB";

    /// <summary>右键菜单"复制消息"文本。</summary>
    public const string CopyMessage = "复制消息";

    /// <summary>右键菜单"复制整行"文本。</summary>
    public const string CopyFullLine = "复制整行";

    /// <summary>设置对话框标题。</summary>
    public const string SettingsTitle = "设置";

    /// <summary>设置项：启动时自动执行 ADB 反向代理。</summary>
    public const string AutoAdbReverse = "启动时自动执行 ADB 反向代理";

    /// <summary>设置项：设备连接时自动启动 Logcat。</summary>
    public const string AutoStartLogcat = "设备连接时自动启动 Logcat";

    /// <summary>设置项：选择设备时自动启动 scrcpy。</summary>
    public const string AutoStartScrcpy = "选择设备时自动启动 scrcpy";

    /// <summary>设置项：自动格式化并折叠 JSON。</summary>
    public const string AutoFormatJson = "自动格式化并折叠 JSON";

    /// <summary>设置项：Logcat 过滤标签文本。</summary>
    public const string LogcatFilter = "Logcat 过滤：";

    /// <summary>设置说明：需要修改 Android 端代码生效。</summary>
    public const string SettingsNote = "* 需要修改 AppConstant.java 并重新编译 Android 应用后生效。";

    /// <summary>Logcat 过滤输入提示说明。</summary>
    public const string LogcatFilterNote = "Logcat 过滤：留空表示全部，例如 ActivityManager:I *:S";

    /// <summary>"取消"按钮文本。</summary>
    public const string Cancel = "取消";

    /// <summary>"确定"按钮文本。</summary>
    public const string Confirm = "确定";

    /// <summary>通用搜索框占位文本。</summary>
    public const string SearchPlaceholder = "搜索...";

    /// <summary>网络日志列表列标题：HTTP 方法。</summary>
    public const string MethodColumn = "方法";

    /// <summary>网络日志列表列标题：请求地址。</summary>
    public const string UrlColumn = "地址";

    /// <summary>网络日志列表列标题：HTTP 状态码。</summary>
    public const string StatusColumn = "状态";

    /// <summary>网络日志列表列标题：请求耗时。</summary>
    public const string DurationColumn = "耗时";

    /// <summary>网络日志列表列标题：请求预览。</summary>
    public const string RequestColumn = "请求";

    /// <summary>网络日志列表列标题：响应预览。</summary>
    public const string ResponseColumn = "响应";

    /// <summary>系统日志列表列标题：时间。</summary>
    public const string TimeColumn = "时间";

    /// <summary>系统日志列表列标题：日志级别。</summary>
    public const string LevelColumn = "级别";

    /// <summary>系统日志列表列标题：标签。</summary>
    public const string TagColumn = "标签";

    /// <summary>系统日志列表列标题：消息正文。</summary>
    public const string MessageColumn = "消息";

    /// <summary>右键菜单：复制请求 URL。</summary>
    public const string CopyUrl = "复制 URL";

    /// <summary>右键菜单：复制 HTTP 方法和 URL。</summary>
    public const string CopyMethodUrl = "复制方法和 URL";

    /// <summary>右键菜单：复制请求体内容。</summary>
    public const string CopyRequestBody = "复制请求体";

    /// <summary>右键菜单：复制 URL 和请求体。</summary>
    public const string CopyUrlRequestBody = "复制 URL 和请求体";

    /// <summary>右键菜单：复制响应体内容。</summary>
    public const string CopyResponseBody = "复制响应体";

    /// <summary>右键菜单：复制 URL 和响应体。</summary>
    public const string CopyUrlResponseBody = "复制 URL 和响应体";

    /// <summary>右键菜单：查看日志详情。</summary>
    public const string ViewDetail = "查看详情";

    /// <summary>设备面板：无连接设备提示。</summary>
    public const string NoDevicesConnected = "没有连接的设备";

    /// <summary>ADB 未找到提示文本。</summary>
    public const string AdbNotFound = "未找到 ADB";

    /// <summary>"全部设备执行反向代理"菜单文本。</summary>
    public const string AdbReverseAll = "全部设备执行反向代理";

    /// <summary>Logcat 相关对话框标题。</summary>
    public const string LogcatTitle = "Logcat";

    /// <summary>截图对话框标题。</summary>
    public const string ScreenshotTitle = "截图";

    /// <summary>ADB 缺失提示对话框标题。</summary>
    public const string MissingAdbTitle = "未找到 ADB";

    /// <summary>ADB 缺失提示对话框正文。</summary>
    public const string MissingAdbMessage = "程序目录中未找到 ADB。\n\n是否手动设置 ADB 路径？";

    /// <summary>当前设备未匹配 ADB serial，无法启动镜像。</summary>
    public const string MirrorDeviceSerialMissing = "当前设备未匹配 ADB serial，无法启动手机镜像";

    /// <summary>scrcpy 正在准备中的状态文本。</summary>
    public const string ScrcpyPreparing = "正在准备 scrcpy...";

    /// <summary>scrcpy 校验中状态文本。</summary>
    public const string ScrcpyChecking = "scrcpy 校验中...";

    /// <summary>scrcpy 未就绪状态文本。</summary>
    public const string ScrcpyNotReady = "scrcpy：未就绪";

    /// <summary>scrcpy 准备失败状态文本。</summary>
    public const string ScrcpyDeployFailed = "scrcpy：准备失败";

    /// <summary>状态栏：scrcpy 校验中。</summary>
    public const string ScrcpyCheckingStatus = "scrcpy：校验中...";

    /// <summary>状态栏：ADB 校验中。</summary>
    public const string AdbCheckingStatus = "ADB：校验中...";

    /// <summary>状态栏：ADB 未找到。</summary>
    public const string AdbNotFoundStatus = "ADB：未找到";

    /// <summary>JSON 详情窗口标题格式，参数依次为：方法、URL路径、状态码、耗时(ms)。</summary>
    public const string JsonDetailTitle = "{0} {1} {2} {3}ms";

    /// <summary>生成设备数量状态文本。</summary>
    /// <param name="count">已连接设备数量。</param>
    public static string DevicesCount(int count) => $"设备：{count}";

    /// <summary>生成 Logcat 日志条数状态文本。</summary>
    /// <param name="count">当前 Logcat 日志条数。</param>
    public static string LogcatCount(int count) => $"Logcat：{count}";

    /// <summary>生成网络日志计数状态文本，过滤数与总数不同时显示 "filtered/total"。</summary>
    /// <param name="filtered">当前过滤后的日志条数。</param>
    /// <param name="total">日志总条数。</param>
    public static string LogsCount(int filtered, int total) =>
        filtered != total ? $"日志：{filtered}/{total}" : $"日志：{total}";

    /// <summary>生成带最大值和暂停状态的日志计数文本。</summary>
    /// <param name="countText">当前计数文本。</param>
    /// <param name="max">最大日志容量。</param>
    /// <param name="paused">是否暂停跟随。</param>
    public static string LogsCountWithMax(string countText, int max, bool paused) =>
        paused ? $"{countText}/{max} 已暂停跟随" : $"{countText}/{max}";

    /// <summary>生成积压日志条数状态文本。</summary>
    /// <param name="count">积压的日志条数。</param>
    public static string BufferedCount(long count) => $"积压：{count}";

    /// <summary>生成服务端口状态文本。</summary>
    /// <param name="port">当前监听端口号。</param>
    public static string ServerPort(int port) => $"服务：{port}";

    /// <summary>生成服务错误状态文本。</summary>
    /// <param name="error">错误描述。</param>
    public static string ServerError(string error) => $"服务：{error}";

    /// <summary>生成服务启动失败提示文本。</summary>
    /// <param name="error">失败原因。</param>
    public static string FailedToStartServer(string error) => $"启动服务失败：{error}";

    /// <summary>生成 ADB 就绪状态文本。</summary>
    /// <param name="fileName">ADB 可执行文件名。</param>
    public static string AdbStatusReady(string fileName) => $"ADB：{fileName}";

    /// <summary>生成 scrcpy 就绪状态文本。</summary>
    /// <param name="fileName">scrcpy 可执行文件名。</param>
    public static string ScrcpyStatusReady(string fileName) => $"scrcpy：{fileName}";

    /// <summary>生成 scrcpy 就绪确认文本。</summary>
    /// <param name="fileName">scrcpy 可执行文件名。</param>
    public static string ScrcpyReady(string fileName) => $"scrcpy 已就绪：{fileName}";

    /// <summary>生成 scrcpy 未找到提示文本。</summary>
    /// <param name="path">期望的 scrcpy 路径。</param>
    public static string ScrcpyNotFound(string path) => $"未找到 scrcpy，请放到程序目录：{path}";

    /// <summary>生成单设备 ADB 反向代理结果文本。</summary>
    /// <param name="serial">设备序列号。</param>
    /// <param name="ok">是否成功。</param>
    /// <param name="output">命令输出。</param>
    public static string AdbReverseResult(string serial, bool ok, string output) =>
        ok ? $"ADB 反向代理成功：{serial}" : $"执行失败：{output}";

    /// <summary>生成批量反向代理单设备结果文本。</summary>
    /// <param name="deviceName">设备名称。</param>
    /// <param name="ok">是否成功。</param>
    /// <param name="output">命令输出。</param>
    public static string ReverseAllDeviceResult(string deviceName, bool ok, string output) =>
        $"{deviceName}: {(ok ? "成功" : output)}";

    /// <summary>无法启动 Logcat 提示文本（ADB 不可用）。</summary>
    public static string CannotStartLogcat => "无法启动 Logcat：ADB 不可用。";

    /// <summary>生成镜像已连接状态文本。</summary>
    /// <param name="serial">设备序列号。</param>
    public static string MirrorConnected(string serial) => $"镜像已连接：{serial}";

    /// <summary>生成镜像就绪、可启动状态文本。</summary>
    /// <param name="serial">设备序列号。</param>
    public static string MirrorReady(string serial) => $"已就绪，可启动镜像：{serial}";

    /// <summary>生成镜像正在启动状态文本。</summary>
    /// <param name="serial">设备序列号。</param>
    public static string MirrorStarting(string serial) => $"正在启动镜像：{serial}";

    /// <summary>生成镜像启动失败提示文本。</summary>
    /// <param name="error">失败原因。</param>
    public static string MirrorStartFailed(string error) => $"镜像启动失败：{error}";

    /// <summary>生成截图保存成功提示文本。</summary>
    /// <param name="fileName">保存的文件名。</param>
    public static string ScreenshotSaved(string fileName) => $"截图已保存：{fileName}";

    /// <summary>生成截图失败提示文本。</summary>
    /// <param name="error">失败原因。</param>
    public static string ScreenshotFailed(string error) => $"截图失败：{error}";
}