using System.Text.RegularExpressions;
using LogViewer.Static;

namespace LogViewer.UI;

public partial class FilterPanel : UserControl
{
    private bool _regexMode;
    private Regex? _cachedRegex;
    private bool _suppressEvents;

    public FilterPanel()
    {
        InitializeComponent();
        txtKeyword.TextChanged += OnKeywordTextChanged;
        btnRegex.Click += OnRegexClick;
        cmbFilter1.SelectedIndexChanged += OnFilterChanged;
        cmbFilter2.SelectedIndexChanged += OnFilterChanged;
        cmbFilter2.DropDown += (s, e) => Filter2DropDown?.Invoke(this, EventArgs.Empty);
    }

    public string Keyword => txtKeyword.Text.Trim();

    public bool RegexMode => _regexMode;

    public Regex? CachedRegex => _cachedRegex;

    public string? Filter1Value => cmbFilter1.SelectedItem as string;

    public string? Filter2Value => cmbFilter2.SelectedItem as string;

    public int Filter1SelectedIndex
    {
        get => cmbFilter1.SelectedIndex;
        set => cmbFilter1.SelectedIndex = value;
    }

    public int Filter2SelectedIndex
    {
        get => cmbFilter2.SelectedIndex;
        set => cmbFilter2.SelectedIndex = value;
    }

    public bool NotifyRegexError { get; set; }

    public event EventHandler? FilterChanged;

    public event EventHandler? RegexModeChanged;

    public event EventHandler? Filter2DropDown;

    public void SetFilter1Items(object[] items)
    {
        cmbFilter1.Items.Clear();
        cmbFilter1.Items.AddRange(items);
    }

    public void SetFilter2Items(object[] items)
    {
        cmbFilter2.Items.Clear();
        cmbFilter2.Items.AddRange(items);
    }

    public void ApplyLanguage(string keywordPlaceholder, string regexText)
    {
        txtKeyword.PlaceholderText = keywordPlaceholder;
        btnRegex.Text = regexText;
    }

    public void EnsureDefaultSelections()
    {
        if (cmbFilter1.SelectedIndex < 0 && cmbFilter1.Items.Count > 0) cmbFilter1.SelectedIndex = 0;
        if (cmbFilter2.SelectedIndex < 0 && cmbFilter2.Items.Count > 0) cmbFilter2.SelectedIndex = 0;
    }

    public void UpdateFilter2Items(string[] items, string? preserveSelected = null)
    {
        _suppressEvents = true;
        try
        {
            var selected = cmbFilter2.SelectedItem as string ?? preserveSelected ?? Language.All;
            cmbFilter2.Items.Clear();
            cmbFilter2.Items.AddRange(items);
            cmbFilter2.SelectedItem = items.Contains(selected, StringComparer.OrdinalIgnoreCase) ? selected : Language.All;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void UpdateCachedRegex()
    {
        _cachedRegex = null;
        if (!_regexMode) return;
        var kw = Keyword;
        if (string.IsNullOrEmpty(kw)) return;
        try
        {
            _cachedRegex = new Regex(kw, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
        }
        catch (ArgumentException ex)
        {
            _regexMode = false;
            btnRegex.BackColor = DefaultBackColor;
            if (NotifyRegexError)
            {
                MessageBox.Show(ex.Message, Language.RegexErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void OnRegexClick(object? sender, EventArgs e)
    {
        _regexMode = !_regexMode;
        btnRegex.BackColor = _regexMode ? Color.LightSkyBlue : DefaultBackColor;
        UpdateCachedRegex();
        RegexModeChanged?.Invoke(this, EventArgs.Empty);
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeywordTextChanged(object? sender, EventArgs e)
    {
        if (_regexMode) UpdateCachedRegex();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_suppressEvents) FilterChanged?.Invoke(this, EventArgs.Empty);
    }
}
