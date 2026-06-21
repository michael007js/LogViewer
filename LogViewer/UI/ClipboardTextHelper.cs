namespace LogViewer.UI;

internal static class ClipboardTextHelper
{
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
