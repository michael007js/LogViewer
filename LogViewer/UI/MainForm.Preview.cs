using LogViewer.Models;
using LogViewer.Utils;

namespace LogViewer.UI;

public partial class MainForm
{
    private bool _detailViewIsRaw => _jsonDetailToolbar.IsRawMode;

    private void InitializeJsonTreeViewsRuntime()
    {
        _jsonHeadersView = CreateRuntimeJsonTreeView(_jsonHeaders, nameof(_jsonHeadersView));
        _jsonRequestBodyView = CreateRuntimeJsonTreeView(_jsonRequestBody, nameof(_jsonRequestBodyView));
        _jsonResponseBodyView = CreateRuntimeJsonTreeView(_jsonResponseBody, nameof(_jsonResponseBodyView));
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

    private JsonTreeView? GetActiveJsonView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _jsonHeadersView;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _jsonRequestBodyView;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _jsonResponseBodyView;
        return null;
    }

    private TextBox? GetActiveRawView()
    {
        if (_tabDetail.SelectedTab == _tabHeaders) return _rawHeaders;
        if (_tabDetail.SelectedTab == _tabRequestBody) return _rawRequestBody;
        if (_tabDetail.SelectedTab == _tabResponseBody) return _rawResponseBody;
        return null;
    }

    private void SyncDetailViewVisibility()
    {
        var isRaw = _jsonDetailToolbar.IsRawMode;
        if (_jsonHeadersView != null) _jsonHeadersView.Visible = !isRaw;
        _rawHeaders.Visible = isRaw;
        if (_jsonRequestBodyView != null) _jsonRequestBodyView.Visible = !isRaw;
        _rawRequestBody.Visible = isRaw;
        if (_jsonResponseBodyView != null) _jsonResponseBodyView.Visible = !isRaw;
        _rawResponseBody.Visible = isRaw;
    }

    private void ShowLogDetail(LogEntry? entry)
    {
        if (entry == null)
        {
            _jsonHeadersView?.DisplayPlainText("");
            _jsonRequestBodyView?.DisplayPlainText("");
            _jsonResponseBodyView?.DisplayPlainText("");
            _rawHeaders.Text = "";
            _rawRequestBody.Text = "";
            _rawResponseBody.Text = "";
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

        _rawHeaders.Text = entry.Headers ?? "";
        _rawRequestBody.Text = JsonFormatter.FormatJson(entry.Send) ?? entry.Send ?? "";
        _rawResponseBody.Text = JsonFormatter.FormatJson(entry.Content) ?? entry.Content ?? "";
    }
}
