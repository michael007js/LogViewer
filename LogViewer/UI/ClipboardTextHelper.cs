namespace LogViewer.UI;

/// <summary>
/// 安全剪贴板写入辅助类，避免 null/空字符串写入和剪贴板占用异常导致的崩溃。
/// </summary>
internal static class ClipboardTextHelper
{
    /// <summary>
    /// 尝试将文本写入系统剪贴板。空文本或剪贴板被占用时返回 false，不抛异常。
    /// </summary>
    /// <param name="text">要写入剪贴板的文本，允许 null。</param>
    /// <returns>成功写入返回 true；文本为空或剪贴板异常返回 false。</returns>
    public static bool TrySetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}