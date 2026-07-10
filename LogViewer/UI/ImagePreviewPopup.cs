using System.Runtime.InteropServices;

namespace LogViewer.UI;

/// <summary>
/// 图片预览弹窗：无边框、不抢焦点，异步下载图片并显示。
/// 下载在后台线程执行，完成后切回 UI 线程更新 PictureBox，不阻塞主线程。
/// </summary>
internal class ImagePreviewPopup : Form
{
    private static readonly HttpClient HttpClient = new();

    private const int MaxPreviewWidth = 400;
    private const int MaxPreviewHeight = 400;

    private readonly PictureBox _pictureBox;
    private readonly Label _loadingLabel;
    private CancellationTokenSource? _cts;

    public ImagePreviewPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Size = new Size(200, 40);
        DoubleBuffered = true;

        _loadingLabel = new Label
        {
            Text = "Loading...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9f),
            ForeColor = Color.Gray
        };
        Controls.Add(_loadingLabel);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Visible = false
        };
        Controls.Add(_pictureBox);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x00800000; // WS_BORDER
            return cp;
        }
    }

    /// <summary>
    /// 异步下载图片并显示。取消上一次未完成的下载。
    /// </summary>
    public async void LoadImageAsync(string url)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url, token);
            if (token.IsCancellationRequested || IsDisposed) return;

            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            if (token.IsCancellationRequested || IsDisposed) { img.Dispose(); return; }

            var (w, h) = ComputeSize(img.Width, img.Height);
            var screen = Screen.FromPoint(Location).WorkingArea;

            if (IsDisposed) { img.Dispose(); return; }
            BeginInvoke(() =>
            {
                if (IsDisposed) { img.Dispose(); return; }
                _loadingLabel.Visible = false;
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = img;
                _pictureBox.Visible = true;
                ClientSize = new Size(w, h);

                AdjustPosition(screen);
            });
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!IsDisposed && !token.IsCancellationRequested)
            {
                BeginInvoke(() =>
                {
                    if (IsDisposed) return;
                    _loadingLabel.Text = "Failed to load";
                    _loadingLabel.ForeColor = Color.Red;
                });
            }
        }
    }

    private static (int w, int h) ComputeSize(int imgW, int imgH)
    {
        var ratio = Math.Min((double)MaxPreviewWidth / imgW, (double)MaxPreviewHeight / imgH);
        if (ratio >= 1) return (imgW, imgH);
        return ((int)(imgW * ratio), (int)(imgH * ratio));
    }

    private void AdjustPosition(Rectangle screen)
    {
        var x = Location.X;
        var y = Location.Y;
        if (x + Width > screen.Right) x = screen.Right - Width - 4;
        if (y + Height > screen.Bottom) y = screen.Bottom - Height - 4;
        if (x < screen.X) x = screen.X;
        if (y < screen.Y) y = screen.Y;
        Location = new Point(x, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
            try { _cts?.Dispose(); } catch (ObjectDisposedException) { }
            _pictureBox.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
