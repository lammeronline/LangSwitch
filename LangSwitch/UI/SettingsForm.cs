using LangSwitch.Autocorrect;
using LangSwitch.Core;
using LangSwitch.Settings;
using System.Net.Http;

namespace LangSwitch.UI;

sealed class SettingsForm : Form
{
    readonly AppSettings _settings;
    readonly List<UserDictionary> _dicts;
    readonly LanguageDictionaries _langDicts;

    // ── Languages tab ────────────────────────────────────────────────────────
    List<AvailableLayout> _allLayouts = [];
    readonly Dictionary<string, bool> _checkedState = new(StringComparer.OrdinalIgnoreCase);
    CheckedListBox _layoutList = null!;
    TextBox _searchBox = null!;

    // ── Display tab ──────────────────────────────────────────────────────────
    RadioButton _sizeSmall = null!, _sizeMedium = null!, _sizeLarge = null!;
    RadioButton _cornerTL = null!, _cornerTR = null!, _cornerBL = null!, _cornerBR = null!;
    TextBox _hotkeyBox = null!;
    Keys    _capturedKey;

    // ── Dictionaries tab ─────────────────────────────────────────────────────
    CheckedListBox _dictListBox  = null!;
    TextBox        _dictNameBox  = null!;
    TextBox        _dictLangBox  = null!;
    TextBox        _dictUrlBox   = null!;
    Label          _dictStatus   = null!;
    Button         _dictDlBtn    = null!;
    bool           _suppressDict;

    public SettingsForm(AppSettings settings, List<UserDictionary> dicts, LanguageDictionaries langDicts)
    {
        _settings  = settings;
        _dicts     = dicts;
        _langDicts = langDicts;
        Build();
        LoadAvailableLayouts();
        LoadDisplayValues();
        LoadDictList();
    }

    void Build()
    {
        Text            = "LangSwitch — Settings";
        Size            = new Size(500, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildLanguagesTab());
        tabs.TabPages.Add(BuildDisplayTab());
        tabs.TabPages.Add(BuildDictionariesTab());
        Controls.Add(tabs);
        Controls.Add(BuildButtonBar());
    }

    Panel BuildButtonBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        var btnOk     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Width = 84, Height = 28 };
        var btnCancel = new Button { Text = "Cancel",  DialogResult = DialogResult.Cancel, Width = 84, Height = 28 };
        btnOk.Click += (_, _) => ApplyAll();
        void Place()
        {
            btnCancel.Location = new Point(bar.Width - 92, 9);
            btnOk.Location     = new Point(btnCancel.Left - 90, 9);
        }
        Place();
        bar.Resize += (_, _) => Place();
        bar.Controls.AddRange([btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        return bar;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Languages tab
    // ═══════════════════════════════════════════════════════════════════════

    TabPage BuildLanguagesTab()
    {
        var page  = new TabPage("Languages");
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(6),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var searchLabel = new Label
        {
            Text = "Keyboard layouts (check to install, uncheck to remove):",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        };

        _searchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search…" };
        _searchBox.TextChanged += (_, _) => FilterList(_searchBox.Text);

        _layoutList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = false, IntegralHeight = false };
        _layoutList.ItemCheck += OnItemCheck;

        var hint = new Label
        {
            Text = "Some changes may require signing out and back in to take full effect.",
            ForeColor = SystemColors.GrayText, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        table.Controls.Add(searchLabel, 0, 0);
        table.Controls.Add(_searchBox,  0, 1);
        table.Controls.Add(_layoutList, 0, 2);
        table.Controls.Add(hint,        0, 3);
        page.Controls.Add(table);
        return page;
    }

    void LoadAvailableLayouts()
    {
        _allLayouts = [.. LayoutManager.GetAvailableLayouts()];
        foreach (var layout in _allLayouts)
            _checkedState[layout.Klid] = layout.IsInstalled;
        FilterList(string.Empty);
    }

    void FilterList(string filter)
    {
        _layoutList.ItemCheck -= OnItemCheck;
        _layoutList.BeginUpdate();
        _layoutList.Items.Clear();

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allLayouts
            : _allLayouts.Where(l => l.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var layout in filtered)
        {
            int idx = _layoutList.Items.Add(layout);
            _layoutList.SetItemChecked(idx, _checkedState.GetValueOrDefault(layout.Klid));
        }

        _layoutList.EndUpdate();
        _layoutList.ItemCheck += OnItemCheck;
    }

    void OnItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_layoutList.Items[e.Index] is AvailableLayout layout)
            _checkedState[layout.Klid] = e.NewValue == CheckState.Checked;
    }

    void ApplyLanguageChanges()
    {
        var toInstall   = _allLayouts.Where(l => !l.IsInstalled &&  _checkedState.GetValueOrDefault(l.Klid)).ToList();
        var toUninstall = _allLayouts.Where(l =>  l.IsInstalled && !_checkedState.GetValueOrDefault(l.Klid)).ToList();

        foreach (var layout in toInstall)
        {
            try { LayoutManager.Install(layout.Klid); }
            catch (Exception ex) { AppLog.Error(ex); }
        }

        var installed = WindowsLayoutDetector.GetInstalledLayouts();
        var failed    = new List<string>();

        foreach (var layout in toUninstall)
        {
            int targetLangId = Convert.ToInt32(layout.Klid[4..], 16);
            var liveLayout   = installed.FirstOrDefault(l => ((int)(l.Handle.ToInt64() & 0xFFFF)) == targetLangId);
            if (liveLayout is null) continue;
            if (!LayoutManager.Uninstall(liveLayout))
                failed.Add(layout.Name);
        }

        if (failed.Count > 0)
            MessageBox.Show(
                "Could not remove the following layouts (they may be in use):\n\n" +
                string.Join("\n", failed) + "\n\nTry signing out and back in.",
                "LangSwitch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Display tab
    // ═══════════════════════════════════════════════════════════════════════

    TabPage BuildDisplayTab()
    {
        var page  = new TabPage("Display");
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(6),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(BuildSizeGroup(),     0, 0);
        table.Controls.Add(BuildPositionGroup(), 0, 1);
        table.Controls.Add(BuildHotkeyGroup(),   0, 2);
        table.Controls.Add(new Panel(),          0, 3);
        page.Controls.Add(table);
        return page;
    }

    GroupBox BuildSizeGroup()
    {
        var g = new GroupBox { Text = "Flag size", Dock = DockStyle.Fill };
        _sizeSmall  = Radio("Small  (64 px)",   14, 26);
        _sizeMedium = Radio("Medium (96 px)",  150, 26);
        _sizeLarge  = Radio("Large  (128 px)", 290, 26);
        g.Controls.AddRange([_sizeSmall, _sizeMedium, _sizeLarge]);
        return g;
    }

    GroupBox BuildPositionGroup()
    {
        var g = new GroupBox { Text = "Popup position", Dock = DockStyle.Fill };
        _cornerTL = Radio("↖  Top Left",      14, 26);
        _cornerTR = Radio("↗  Top Right",    230, 26);
        _cornerBL = Radio("↙  Bottom Left",   14, 56);
        _cornerBR = Radio("↘  Bottom Right", 230, 56);
        g.Controls.AddRange([_cornerTL, _cornerTR, _cornerBL, _cornerBR]);
        return g;
    }

    GroupBox BuildHotkeyGroup()
    {
        var g = new GroupBox { Text = "Convert word hotkey", Dock = DockStyle.Fill };
        var hint = new Label { Text = "Press key:", Location = new Point(14, 24), AutoSize = true };

        _capturedKey = _settings.ConvertHotKey;
        _hotkeyBox   = new TextBox
        {
            Location  = new Point(90, 21),
            Width     = 130,
            ReadOnly  = true,
            BackColor = SystemColors.Window,
            Text      = KeyLabel(_capturedKey),
        };
        _hotkeyBox.GotFocus  += (_, _) => _hotkeyBox.Text = "Press hotkey…";
        _hotkeyBox.LostFocus += (_, _) => _hotkeyBox.Text = KeyLabel(_capturedKey);
        _hotkeyBox.KeyDown   += (_, e) =>
        {
            // Ignore pure modifier presses — wait for the actual key
            if (e.KeyCode is Keys.ShiftKey or Keys.ControlKey or Keys.Menu
                          or Keys.LShiftKey or Keys.RShiftKey
                          or Keys.LControlKey or Keys.RControlKey
                          or Keys.LMenu or Keys.RMenu)
                return;
            e.SuppressKeyPress = true;
            _capturedKey       = e.KeyCode | e.Modifiers;
            _hotkeyBox.Text    = KeyLabel(_capturedKey);
            _hotkeyBox.Parent?.Focus();
        };

        var btnClear = new Button { Text = "Clear", Location = new Point(228, 19), Width = 56, Height = 24 };
        btnClear.Click += (_, _) => { _capturedKey = Keys.None; _hotkeyBox.Text = KeyLabel(Keys.None); };

        g.Controls.AddRange([hint, _hotkeyBox, btnClear]);
        return g;
    }

    static string KeyLabel(Keys k)
    {
        if (k == Keys.None) return "(none)";
        var parts = new List<string>();
        if ((k & Keys.Control) != 0) parts.Add("Ctrl");
        if ((k & Keys.Shift)   != 0) parts.Add("Shift");
        if ((k & Keys.Alt)     != 0) parts.Add("Alt");
        var baseKey = k & Keys.KeyCode;
        if (baseKey != Keys.None) parts.Add(baseKey.ToString());
        return string.Join("+", parts);
    }

    static RadioButton Radio(string text, int x, int y) =>
        new() { Text = text, Location = new Point(x, y), AutoSize = true };

    void LoadDisplayValues()
    {
        (_settings.PopupSize switch
        {
            FlagDisplaySize.Small => _sizeSmall,
            FlagDisplaySize.Large => _sizeLarge,
            _                     => _sizeMedium,
        }).Checked = true;

        (_settings.PopupPosition switch
        {
            ScreenCorner.TopLeft    => _cornerTL,
            ScreenCorner.TopRight   => _cornerTR,
            ScreenCorner.BottomLeft => _cornerBL,
            _                       => _cornerBR,
        }).Checked = true;
    }

    void SaveDisplayValues()
    {
        _settings.PopupSize = _sizeSmall.Checked ? FlagDisplaySize.Small
            : _sizeLarge.Checked ? FlagDisplaySize.Large
            : FlagDisplaySize.Medium;

        _settings.PopupPosition = _cornerTL.Checked ? ScreenCorner.TopLeft
            : _cornerTR.Checked ? ScreenCorner.TopRight
            : _cornerBL.Checked ? ScreenCorner.BottomLeft
            : ScreenCorner.BottomRight;

        _settings.ConvertHotKey = _capturedKey;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dictionaries tab
    // ═══════════════════════════════════════════════════════════════════════

    TabPage BuildDictionariesTab()
    {
        var page  = new TabPage("Dictionaries");
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(6),
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

        outer.Controls.Add(BuildDictToolbar(),  0, 0);
        outer.Controls.Add(BuildDictListPanel(), 0, 1);
        outer.Controls.Add(BuildDictDetail(),   0, 2);

        page.Controls.Add(outer);
        return page;
    }

    Panel BuildDictToolbar()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var label  = new Label  { Text = "Word list dictionaries:", AutoSize = true, Top = 7 };
        var btnAdd = new Button { Text = "+ Add",     Width = 60,  Height = 24, Left = 160, Top = 2 };
        var btnDel = new Button { Text = "− Remove",  Width = 72,  Height = 24, Left = 226, Top = 2 };

        btnAdd.Click += (_, _) => AddDictionary();
        btnDel.Click += (_, _) => RemoveDictionary();

        panel.Controls.AddRange([label, btnAdd, btnDel]);
        return panel;
    }

    Panel BuildDictListPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        _dictListBox = new CheckedListBox
        {
            Dock = DockStyle.Fill, CheckOnClick = false, IntegralHeight = false,
        };
        _dictListBox.SelectedIndexChanged += (_, _) => RefreshDictDetail();
        _dictListBox.ItemCheck += (_, e) =>
        {
            if (_suppressDict) return;
            if (_dictListBox.Items[e.Index] is UserDictionary d)
                d.Enabled = e.NewValue == CheckState.Checked;
        };

        panel.Controls.Add(_dictListBox);
        return panel;
    }

    Panel BuildDictDetail()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        // Row 1 — Name / Lang
        var lblName = new Label { Text = "Name:", Left = 0, Top = 8, AutoSize = true };
        _dictNameBox = new TextBox { Left = 46, Top = 5, Width = 158 };
        var lblLang  = new Label  { Text = "Lang:", Left = 214, Top = 8, AutoSize = true };
        _dictLangBox = new TextBox { Left = 254, Top = 5, Width = 38 };

        // Row 2 — URL + download button
        var lblUrl = new Label { Text = "URL:", Left = 0, Top = 36, AutoSize = true };
        _dictUrlBox  = new TextBox { Left = 46, Top = 33, Width = 290 };
        _dictDlBtn   = new Button  { Text = "⬇ Download", Left = 344, Top = 31, Width = 100, Height = 24 };
        _dictDlBtn.Click += async (_, _) => await DownloadSelectedDict();

        // Row 3 — Status
        _dictStatus = new Label { Left = 0, Top = 62, AutoSize = true, ForeColor = SystemColors.GrayText };

        _dictNameBox.TextChanged += (_, _) =>
        {
            if (_suppressDict || SelectedDict() is not { } d) return;
            d.Name = _dictNameBox.Text;
            _dictListBox.Refresh();
        };
        _dictLangBox.TextChanged += (_, _) =>
        {
            if (_suppressDict || SelectedDict() is not { } d) return;
            d.Lang = _dictLangBox.Text.Trim().ToLowerInvariant();
            _dictListBox.Refresh();
            RefreshDictStatus(d);
        };
        _dictUrlBox.TextChanged += (_, _) =>
        {
            if (_suppressDict || SelectedDict() is not { } d) return;
            d.Url = _dictUrlBox.Text.Trim();
        };

        panel.Controls.AddRange([lblName, _dictNameBox, lblLang, _dictLangBox,
                                  lblUrl,  _dictUrlBox,  _dictDlBtn, _dictStatus]);
        return panel;
    }

    void LoadDictList()
    {
        _suppressDict = true;
        _dictListBox.Items.Clear();
        foreach (var d in _dicts)
        {
            int idx = _dictListBox.Items.Add(d);
            _dictListBox.SetItemChecked(idx, d.Enabled);
        }
        _suppressDict = false;

        if (_dictListBox.Items.Count > 0) _dictListBox.SelectedIndex = 0;
        RefreshDictDetail();
    }

    void RefreshDictDetail()
    {
        var dict    = SelectedDict();
        bool hasSel = dict is not null;

        _dictNameBox.Enabled = hasSel;
        _dictLangBox.Enabled = hasSel;
        _dictUrlBox.Enabled  = hasSel;
        _dictDlBtn.Enabled   = hasSel;

        _suppressDict = true;
        _dictNameBox.Text = dict?.Name ?? "";
        _dictLangBox.Text = dict?.Lang ?? "";
        _dictUrlBox.Text  = dict?.Url  ?? "";
        _suppressDict = false;

        if (dict is not null) RefreshDictStatus(dict);
        else _dictStatus.Text = "";
    }

    void RefreshDictStatus(UserDictionary dict)
    {
        var count = string.IsNullOrEmpty(dict.Lang) ? 0 : _langDicts.WordCount(dict.Lang);
        _dictStatus.Text      = count > 0 ? $"{count:N0} words loaded ✓" : "Not downloaded";
        _dictStatus.ForeColor = count > 0 ? Color.SeaGreen : SystemColors.GrayText;
    }

    void AddDictionary()
    {
        var dict = new UserDictionary { Name = "New Dictionary", Enabled = true };
        _dicts.Add(dict);

        _suppressDict = true;
        int idx = _dictListBox.Items.Add(dict);
        _dictListBox.SetItemChecked(idx, true);
        _suppressDict = false;

        _dictListBox.SelectedIndex = idx;
    }

    void RemoveDictionary()
    {
        int idx = _dictListBox.SelectedIndex;
        if (idx < 0) return;
        if (_dictListBox.Items[idx] is UserDictionary d) _dicts.Remove(d);
        _dictListBox.Items.RemoveAt(idx);
        if (_dictListBox.Items.Count > 0)
            _dictListBox.SelectedIndex = Math.Min(idx, _dictListBox.Items.Count - 1);
        else
            RefreshDictDetail();
    }

    async Task DownloadSelectedDict()
    {
        var dict = SelectedDict();
        if (dict is null) return;

        var lang = _dictLangBox.Text.Trim().ToLowerInvariant();
        var url  = _dictUrlBox.Text.Trim();

        if (string.IsNullOrEmpty(lang))
        {
            MessageBox.Show("Enter a language code (e.g. en, ru, uk).", "LangSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show("Enter a valid http:// or https:// URL.", "LangSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _dictDlBtn.Enabled = false;
        var origText = _dictDlBtn.Text;
        try
        {
            var progress = new Progress<string>(msg => _dictDlBtn.Text = msg);
            int count = await WordListDownloader.DownloadAsync(lang, url, progress);
            _langDicts.Reload();
            RefreshDictStatus(dict);
            MessageBox.Show($"Downloaded {count:N0} words for {lang.ToUpperInvariant()}.",
                "LangSwitch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Network error:\n{ex.Message}", "LangSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error:\n{ex.Message}", "LangSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _dictDlBtn.Text    = origText;
            _dictDlBtn.Enabled = true;
        }
    }

    UserDictionary? SelectedDict() => _dictListBox.SelectedItem as UserDictionary;

    // ═══════════════════════════════════════════════════════════════════════
    // Commit all tabs
    // ═══════════════════════════════════════════════════════════════════════

    void ApplyAll()
    {
        ApplyLanguageChanges();
        SaveDisplayValues();
    }
}
