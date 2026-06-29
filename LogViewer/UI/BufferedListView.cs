using System.Reflection;
using System.Runtime.InteropServices;

namespace LogViewer.UI;

/// <summary>
/// ListView 双缓冲辅助工具，通过 Win32 消息实现无闪烁滚动和精确滚动位置控制。
/// </summary>
internal static class BufferedListViewHelper
{
    // Win32 ListView 消息基址
    private const int LvmFirst = 0x1000;

    // LVM_GETTOPINDEX — 获取列表视图顶部可见项的精确索引
    private const int LvmGetTopIndex = LvmFirst + 39;

    // LVM_SCROLL — 按像素行数滚动列表视图
    private const int LvmScroll = LvmFirst + 20;

    /// <summary>
    /// 启用 ListView 的双缓冲模式，消除重绘闪烁。
    /// </summary>
    /// <param name="listView">目标 ListView 控件。</param>
    public static void EnableDoubleBuffer(ListView listView)
    {
        var property = typeof(Control).GetProperty("DoubleBuffered",
            BindingFlags.Instance | BindingFlags.NonPublic);
        property?.SetValue(listView, true);
    }

    /// <summary>
    /// 通过 Win32 消息获取 ListView 顶部可见项的精确索引，
    /// 比 TopItem 属性更可靠（TopItem 在部分滚动时可能返回不正确的索引）。
    /// </summary>
    /// <param name="listView">目标 ListView 控件。</param>
    /// <returns>顶部可见项的索引；若句柄未创建则返回 0。</returns>
    public static int GetTopIndexExact(ListView listView)
    {
        return !listView.IsHandleCreated
            ? 0
            : (int)SendMessage(listView.Handle, LvmGetTopIndex, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// 在 VirtualListSize 变更后恢复滚动位置，使目标索引成为顶部可见项。
    /// 先 EnsureVisible 确保目标可见，再通过 LVM_SCROLL 精确对齐到顶部。
    /// </summary>
    /// <param name="listView">目标 ListView 控件。</param>
    /// <param name="targetIndex">期望恢复到顶部的项索引。</param>
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

    /// <summary>
    /// 获取单个可见项的高度（像素），用于计算滚动偏移量。
    /// 若 GetItemRect 失败则回退到字体高度 + 6 像素估算。
    /// </summary>
    /// <param name="listView">目标 ListView 控件。</param>
    /// <param name="ensuredIndex">已确保可见的项索引。</param>
    /// <returns>项高度（像素）。</returns>
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

    /// <summary>
    /// Win32 SendMessage P/Invoke 声明，用于发送 ListView 控制消息。
    /// </summary>
    /// <param name="hWnd">目标窗口句柄。</param>
    /// <param name="msg">消息ID。</param>
    /// <param name="wParam">消息参数1。</param>
    /// <param name="lParam">消息参数2。</param>
    /// <returns>消息处理结果。</returns>
    public static bool IsAtBottom(ListView lv)
    {
        if (lv.VirtualListSize == 0) return true;
        var topIndex = lv.TopItem?.Index ?? 0;
        var visibleCount = Math.Max(1, lv.ClientSize.Height / Math.Max(1, lv.Font.Height + 6));
        return topIndex + visibleCount >= lv.VirtualListSize;
    }

    public static void ScrollToBottom(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(lv.VirtualListSize - 1); } catch { }
        }
    }

    public static void ScrollToTop(ListView lv)
    {
        if (lv.VirtualListSize > 0)
        {
            try { lv.EnsureVisible(0); } catch { }
        }
    }

    public static int GetApproxVisibleRowCount(ListView listView)
    {
        return Math.Max(1, listView.ClientSize.Height / Math.Max(1, listView.Font.Height + 6));
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}