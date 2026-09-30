namespace LogViewer.UI;

/// <summary>
/// 安全剪贴板写入辅助类，避免 null/空字符串写入和剪贴板占用异常导致的崩溃。
/// </summary>
internal static class ClipboardTextHelper
{
    private static readonly object SyncRoot = new();
    private static readonly AutoResetEvent WorkSignal = new(false);
    private static ClipboardRequest? _pendingRequest;

    static ClipboardTextHelper()
    {
        var worker = new Thread(ProcessClipboardQueue)
        {
            IsBackground = true,
            Name = "LogViewer.Clipboard"
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

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

    /// <summary>由单一 STA 工作线程串行写入剪贴板，等待中的旧请求由最新请求替换。</summary>
    public static Task<bool> TrySetTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Task.FromResult(false);
        var request = new ClipboardRequest(text);
        lock (SyncRoot)
        {
            _pendingRequest?.Completion.TrySetResult(false);
            _pendingRequest = request;
        }
        WorkSignal.Set();
        return request.Completion.Task;
    }

    private static void ProcessClipboardQueue()
    {
        while (true)
        {
            WorkSignal.WaitOne();
            ClipboardRequest? request;
            lock (SyncRoot)
            {
                request = _pendingRequest;
                _pendingRequest = null;
            }
            if (request != null) request.Completion.TrySetResult(TrySetText(request.Text));
        }
    }

    private sealed class ClipboardRequest(string text)
    {
        internal string Text { get; } = text;
        internal TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}