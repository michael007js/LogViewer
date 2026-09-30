using LogViewer.Models;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class MainForm
{
    private readonly Dictionary<PagedTextView, int> _rawPreviewStates = new();
    private CancellationTokenSource? _rawPreviewCts;
    private LogEntry? _rawPreviewEntry;
    private int _rawPreviewVersion;

    private bool _detailViewIsRaw => _jsonDetailToolbar.IsRawMode;

    private void InitializeJsonTreeViewsRuntime()
    {
        _jsonHeadersView = CreateRuntimeJsonTreeView(_jsonHeaders, nameof(_jsonHeadersView));
        _jsonRequestBodyView = CreateRuntimeJsonTreeView(_jsonRequestBody, nameof(_jsonRequestBodyView));
        _jsonResponseBodyView = CreateRuntimeJsonTreeView(_jsonResponseBody, nameof(_jsonResponseBodyView));
        _pagedHeadersView = CreateRuntimePagedTextView(_tabHeaders, nameof(_pagedHeadersView));
        _pagedRequestBodyView = CreateRuntimePagedTextView(_tabRequestBody, nameof(_pagedRequestBodyView));
        _pagedResponseBodyView = CreateRuntimePagedTextView(_tabResponseBody, nameof(_pagedResponseBodyView));
    }

    private static JsonTreeView CreateRuntimeJsonTreeView(Control host, string name)
    {
        var view = new JsonTreeView
        {
            Dock = DockStyle.Fill,
            Name = name,
            Margin = Padding.Empty
        };
        host.Controls.Add(view);
        view.BringToFront();
        return view;
    }

    private static PagedTextView CreateRuntimePagedTextView(Control host, string name)
    {
        var view = new PagedTextView
        {
            Dock = DockStyle.Fill,
            Name = name,
            Margin = Padding.Empty,
            Visible = false
        };
        host.Controls.Add(view);
        view.BringToFront();
        return view;
    }

    private JsonTreeView? GetActiveJsonView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _jsonHeadersView;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _jsonRequestBodyView;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _jsonResponseBodyView;
        return null;
    }

    private PagedTextView? GetActiveRawView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _pagedHeadersView;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _pagedRequestBodyView;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _pagedResponseBodyView;
        return null;
    }

    private void SyncDetailViewVisibility()
    {
        var isRaw = _jsonDetailToolbar.IsRawMode;
        if (_jsonHeadersView != null) _jsonHeadersView.Visible = !isRaw;
        if (_pagedHeadersView != null) _pagedHeadersView.Visible = isRaw;
        _rawHeaders.Visible = false;
        if (_jsonRequestBodyView != null) _jsonRequestBodyView.Visible = !isRaw;
        if (_pagedRequestBodyView != null) _pagedRequestBodyView.Visible = isRaw;
        _rawRequestBody.Visible = false;
        if (_jsonResponseBodyView != null) _jsonResponseBodyView.Visible = !isRaw;
        if (_pagedResponseBodyView != null) _pagedResponseBodyView.Visible = isRaw;
        _rawResponseBody.Visible = false;
        if (isRaw) EnsureActiveRawViewLoaded();
    }

    private void ShowLogDetail(LogEntry? entry)
    {
        BeginRawPreviewEntry(entry);
        if (entry == null)
        {
            _jsonHeadersView?.DisplayPlainText("");
            _jsonRequestBodyView?.DisplayPlainText("");
            _jsonResponseBodyView?.DisplayPlainText("");
            return;
        }

        if (_settings.AutoFormatJson)
        {
            _jsonHeadersView?.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBodyView?.DisplayJson(entry.Send ?? "");
            _jsonResponseBodyView?.DisplayJson(entry.Content ?? "");
        }
        else
        {
            _jsonHeadersView?.DisplayPlainText(entry.Headers ?? "");
            _jsonRequestBodyView?.DisplayPlainText(entry.Send ?? "");
            _jsonResponseBodyView?.DisplayPlainText(entry.Content ?? "");
        }

        if (_detailViewIsRaw) EnsureActiveRawViewLoaded();
    }

    private void BeginRawPreviewEntry(LogEntry? entry)
    {
        _rawPreviewCts?.Cancel();
        _rawPreviewCts?.Dispose();
        _rawPreviewCts = new CancellationTokenSource();
        _rawPreviewVersion++;
        _rawPreviewEntry = entry;
        _rawPreviewStates.Clear();
        _pagedHeadersView?.Clear();
        _pagedRequestBodyView?.Clear();
        _pagedResponseBodyView?.Clear();
        _rawHeaders.Clear();
        _rawRequestBody.Clear();
        _rawResponseBody.Clear();
    }

    private void EnsureActiveRawViewLoaded()
    {
        var target = GetActiveRawView();
        var entry = _rawPreviewEntry;
        if (target == null || entry == null || _rawPreviewStates.ContainsKey(target)) return;

        string source;
        bool formatJson;
        if (target == _pagedHeadersView)
        {
            source = entry.Headers ?? string.Empty;
            formatJson = false;
        }
        else if (target == _pagedRequestBodyView)
        {
            source = entry.Send ?? string.Empty;
            formatJson = true;
        }
        else
        {
            source = entry.Content ?? string.Empty;
            formatJson = true;
        }

        _rawPreviewStates[target] = 1;
        int version = _rawPreviewVersion;
        _ = LoadRawPreviewAsync(target, source, formatJson, version, _rawPreviewCts!.Token);
    }

    private async Task LoadRawPreviewAsync(PagedTextView target, string source, bool formatJson, int version,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(60, token);
            var document = await JsonFormatter.RunBackgroundAsync(() =>
            {
                string text = formatJson ? JsonFormatter.FormatJson(source) ?? source : source;
                return PagedTextView.BuildDocument(text, token);
            }, token);

            await InvokePreviewUiAsync(() =>
            {
                if (!IsCurrentRawPreview(version, token) || target.IsDisposed) return;
                target.Display(document);
                _rawPreviewStates[target] = 2;
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task InvokePreviewUiAsync(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return Task.CompletedTask;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Execute()
        {
            try
            {
                if (!IsDisposed) action();
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        try
        {
            if (InvokeRequired) BeginInvoke((Action)Execute);
            else Execute();
        }
        catch (InvalidOperationException)
        {
            completion.TrySetResult(false);
        }
        return completion.Task;
    }

    private bool IsCurrentRawPreview(int version, CancellationToken token) =>
        !token.IsCancellationRequested && !IsDisposed && version == _rawPreviewVersion;

    private void CancelPreviewAsyncOperations()
    {
        _rawPreviewCts?.Cancel();
        _rawPreviewCts?.Dispose();
        _rawPreviewCts = null;
    }
}
