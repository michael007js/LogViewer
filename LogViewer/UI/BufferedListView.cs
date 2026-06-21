namespace LogViewer.UI;

internal static class BufferedListViewHelper
{
    private const int LvmFirst = 0x1000;
    private const int LvmGetTopIndex = LvmFirst + 39;
    private const int LvmScroll = LvmFirst + 20;

    public static void EnableDoubleBuffer(ListView listView)
    {
        var property = typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        property?.SetValue(listView, true);
    }

    public static int GetTopIndexExact(ListView listView)
    {
        return !listView.IsHandleCreated ? 0 : (int)SendMessage(listView.Handle, LvmGetTopIndex, IntPtr.Zero, IntPtr.Zero);
    }

    public static void RestoreTopIndexExact(ListView listView, int targetIndex)
    {
        if (!listView.IsHandleCreated || listView.VirtualListSize <= 0 || targetIndex < 0)
        {
            return;
        }

        targetIndex = Math.Min(targetIndex, listView.VirtualListSize - 1);
        listView.EnsureVisible(targetIndex);

        var currentTopIndex = GetTopIndexExact(listView);
        if (currentTopIndex == targetIndex)
        {
            return;
        }

        var itemHeight = GetVisibleItemHeight(listView, targetIndex);
        if (itemHeight <= 0)
        {
            return;
        }

        var deltaRows = targetIndex - currentTopIndex;
        if (deltaRows != 0)
        {
            SendMessage(listView.Handle, LvmScroll, IntPtr.Zero, (IntPtr)(deltaRows * itemHeight));
        }
    }

    private static int GetVisibleItemHeight(ListView listView, int ensuredIndex)
    {
        try
        {
            return listView.GetItemRect(ensuredIndex).Height;
        }
        catch
        {
            return listView.Font.Height + 6;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
