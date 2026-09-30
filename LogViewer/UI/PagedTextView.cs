using LogViewer.Static;

namespace LogViewer.UI;

/// <summary>
/// 通过 VirtualMode ListView 按需呈现长文本，避免把完整内容写入 TextBox 阻塞 UI。
/// </summary>
internal sealed class PagedTextView : UserControl
{
    private const int MaxSegmentLength = 512;
    private static readonly string MeasureText = new('M', MaxSegmentLength);

    private readonly ListView _listView;
    private readonly ColumnHeader _column;
    private Font _displayFont;
    private PagedTextDocument _document = PagedTextDocument.Empty;

    internal PagedTextView()
    {
        _displayFont = new Font("Consolas", 11f);
        _column = new ColumnHeader();
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            VirtualMode = true,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            LabelWrap = false,
            ShowItemToolTips = false,
            Font = _displayFont
        };
        _listView.Columns.Add(_column);
        _listView.RetrieveVirtualItem += (_, e) =>
            e.Item = new ListViewItem(_document.GetSegment(e.ItemIndex));
        _listView.ContextMenuStrip = CreateContextMenu();
        _listView.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C) CopySelected();
        };
        Controls.Add(_listView);
        Resize += (_, _) => UpdateColumnWidth();
        UpdateColumnWidth();
    }

    internal static PagedTextDocument BuildDocument(string text, CancellationToken token) =>
        PagedTextDocument.Create(text, MaxSegmentLength, token);

    internal void Display(PagedTextDocument document)
    {
        _listView.VirtualListSize = 0;
        _document = document;
        _listView.VirtualListSize = document.Count;
        UpdateColumnWidth();
        _listView.Invalidate();
    }

    internal void Clear() => Display(PagedTextDocument.Empty);

    internal void SetDisplayFont(Font font)
    {
        if (_displayFont.Equals(font)) return;
        var replacement = (Font)font.Clone();
        var previous = _displayFont;
        _displayFont = replacement;
        _listView.Font = replacement;
        previous.Dispose();
        UpdateColumnWidth();
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        var copySelected = new ToolStripMenuItem(Language.CopySelectedText);
        copySelected.Click += (_, _) => CopySelected();
        var copyAll = new ToolStripMenuItem(Language.CopyAllRawText);
        copyAll.Click += (_, _) => _ = ClipboardTextHelper.TrySetTextAsync(_document.Text);
        menu.Items.Add(copySelected);
        menu.Items.Add(copyAll);
        return menu;
    }

    private void CopySelected()
    {
        if (_listView.SelectedIndices.Count == 0) return;
        _ = ClipboardTextHelper.TrySetTextAsync(_document.GetSegment(_listView.SelectedIndices[0]));
    }

    private void UpdateColumnWidth()
    {
        int textWidth = TextRenderer.MeasureText(MeasureText, _listView.Font,
            Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        _column.Width = Math.Max(_listView.ClientSize.Width, textWidth + 24);
    }

    protected override void Dispose(bool disposing)
    {
        var fontToDispose = disposing ? _displayFont : null;
        base.Dispose(disposing);
        fontToDispose?.Dispose();
    }
}

internal sealed class PagedTextDocument
{
    internal static readonly PagedTextDocument Empty = new(string.Empty, Array.Empty<TextSegment>());

    private readonly TextSegment[] _segments;

    private PagedTextDocument(string text, TextSegment[] segments)
    {
        Text = text;
        _segments = segments;
    }

    internal string Text { get; }

    internal int Count => _segments.Length;

    internal string GetSegment(int index)
    {
        if ((uint)index >= (uint)_segments.Length) return string.Empty;
        var segment = _segments[index];
        return segment.Length == 0 ? string.Empty : Text.Substring(segment.Start, segment.Length);
    }

    internal static PagedTextDocument Create(string text, int maxSegmentLength, CancellationToken token)
    {
        if (text.Length == 0) return Empty;
        var segments = new List<TextSegment>();
        int position = 0;

        while (position < text.Length)
        {
            token.ThrowIfCancellationRequested();
            int lineEnd = position;
            while (lineEnd < text.Length && text[lineEnd] is not '\r' and not '\n') lineEnd++;

            int lineLength = lineEnd - position;
            if (lineLength == 0) segments.Add(new TextSegment(position, 0));
            int offset = 0;
            while (offset < lineLength)
            {
                int length = Math.Min(maxSegmentLength, lineLength - offset);
                int segmentEnd = position + offset + length;
                if (segmentEnd < lineEnd && char.IsHighSurrogate(text[segmentEnd - 1]) &&
                    char.IsLowSurrogate(text[segmentEnd])) length--;
                segments.Add(new TextSegment(position + offset, length));
                offset += length;
            }

            if (lineEnd >= text.Length) break;
            position = lineEnd + 1;
            if (text[lineEnd] == '\r' && position < text.Length && text[position] == '\n') position++;
        }

        if (text[^1] is '\r' or '\n') segments.Add(new TextSegment(text.Length, 0));
        return new PagedTextDocument(text, segments.ToArray());
    }

    private readonly record struct TextSegment(int Start, int Length);
}
