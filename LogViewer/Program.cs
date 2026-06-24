using LogViewer.UI;

namespace LogViewer;

/// <summary>
/// 应用程序入口点，初始化 WinForms 并启动主窗口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口方法。初始化 WinForms 框架配置并以 STA 模式运行主窗口。
    /// STAThread 属性确保 COM 组件（如剪贴板、文件对话框）在 UI 线程正常工作。
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}