using LangSwitch.Core;
using LangSwitch.Settings;
using System.Net.Http;
using static LangSwitch.Core.Localization;

namespace LangSwitch.UI;

sealed class SettingsForm : Form
{
    readonly AppSettings _settings;
    readonly List<UserDictionary> _dicts;
    readonly List<Abbreviation> _abbreviations;
    readonly LanguageDictionaries _langDicts;
    readonly Stats _stats;
    readonly Dictionary<string, int> _undoCounts;
    readonly Action _resetUndoLearning;

    // ── Sidebar nav ─────────────────────────────────────────────────────────
    const int NavWidth = 150;
    static readonly Color NavPanelColor    = Color.FromArgb(243, 243, 243);
    static readonly Color NavIdleColor     = Color.FromArgb(243, 243, 243);
    static readonly Color NavHoverColor    = Color.FromArgb(230, 230, 230);
    static readonly Color NavSelectedColor = Color.FromArgb(221, 234, 248);
    static readonly Color NavAccentColor   = Color.FromArgb(0, 95, 184);
    readonly List<(RadioButton Nav, Panel Section)> _navEntries = [];
    static readonly Font RegularFont = new("Segoe UI", 9f);

    // ── Languages section ───────────────────────────────────────────────────
    List<AvailableLayout> _allLayouts = [];
    readonly Dictionary<string, bool> _checkedState = new(StringComparer.OrdinalIgnoreCase);
    CheckedListBox _layoutList = null!;
    TextBox _searchBox = null!;

    // ── General section ──────────────────────────────────────────────────────
    CheckBox _autocorrectBox = null!;
    CheckBox _notifyWrongLayoutBox = null!;
    CheckBox _fixCapsLockBox = null!;
    CheckBox _autoPunctuationBox = null!;
    CheckBox _launchAtStartupBox = null!;
    ComboBox _languageBox = null!;
    List<(string Code, string DisplayName)> _languages = [];
    TextBox  _hotkeyBox = null!;
    NumericUpDown _clipDelayBox = null!;
    NumericUpDown _undoThresholdBox = null!;
    Label    _learnedWordsLabel = null!;
    Keys     _capturedKey;

    // ── Appearance section ───────────────────────────────────────────────────
    RadioButton _sizeSmall = null!, _sizeMedium = null!, _sizeLarge = null!;
    RadioButton _cornerTL = null!, _cornerTR = null!, _cornerBL = null!, _cornerBR = null!;

    // ── Exclusions section ───────────────────────────────────────────────────
    ListBox _exclusionsList = null!;
    TextBox _exclusionBox   = null!;

    // ── Abbreviations section ────────────────────────────────────────────────
    CheckedListBox _abbrevListBox  = null!;
    TextBox        _abbrevTriggerBox   = null!;
    TextBox        _abbrevExpansionBox = null!;
    bool           _suppressAbbrev;

    // ── Dictionaries section ─────────────────────────────────────────────────
    CheckedListBox _dictListBox  = null!;
    TextBox        _dictNameBox  = null!;
    TextBox        _dictLangBox  = null!;
    TextBox        _dictUrlBox   = null!;
    Label          _dictStatus   = null!;
    Button         _dictDlBtn    = null!;
    bool           _suppressDict;

    public SettingsForm(AppSettings settings, List<UserDictionary> dicts, LanguageDictionaries langDicts,
        List<Abbreviation> abbreviations, Stats stats, Dictionary<string, int> undoCounts, Action resetUndoLearning)
    {
        _settings          = settings;
        _dicts             = dicts;
        _langDicts         = langDicts;
        _abbreviations     = abbreviations;
        _stats             = stats;
        _undoCounts        = undoCounts;
        _resetUndoLearning = resetUndoLearning;
        Build();
        LoadAvailableLayouts();
        LoadDisplayValues();
        LoadDictList();
        LoadAbbrevList();
        LoadExclusionsList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Shell: sidebar nav + content area
    // ═══════════════════════════════════════════════════════════════════════

    void Build()
    {
        Text            = T("Settings.Title");
        Size            = new Size(580, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NavWidth));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var nav     = BuildNav();
        var content = BuildContent();

        body.Controls.Add(nav,     0, 0);
        body.Controls.Add(content, 1, 0);

        root.Controls.Add(body,              0, 0);
        root.Controls.Add(BuildButtonBar(),  0, 1);
        Controls.Add(root);

        _navEntries[0].Nav.Checked = true;
    }

    Panel BuildNav()
    {
        var nav = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = NavPanelColor,
            Padding   = new Padding(8, 12, 8, 8),
        };

        RadioButton MakeNavButton(string text)
        {
            var b = new RadioButton
            {
                Text         = text,
                Appearance   = Appearance.Button,
                FlatStyle    = FlatStyle.Flat,
                TextAlign    = ContentAlignment.MiddleLeft,
                Padding      = new Padding(14, 0, 0, 0),
                Dock         = DockStyle.Fill,
                Margin       = new Padding(0, 0, 0, 2),
                BackColor    = NavIdleColor,
                ForeColor    = Color.FromArgb(32, 32, 32),
                Cursor       = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize         = 0;
            b.FlatAppearance.CheckedBackColor   = NavSelectedColor;
            b.FlatAppearance.MouseOverBackColor = NavHoverColor;
            b.FlatAppearance.MouseDownBackColor = NavSelectedColor;
            b.CheckedChanged += (_, _) =>
                b.ForeColor = b.Checked ? NavAccentColor : Color.FromArgb(32, 32, 32);
            return b;
        }

        var btnGeneral      = MakeNavButton(T("Nav.General"));
        var btnAppearance   = MakeNavButton(T("Nav.Appearance"));
        var btnExclusions   = MakeNavButton(T("Nav.Exclusions"));
        var btnAbbreviations = MakeNavButton(T("Nav.Abbreviations"));
        var btnStatistics   = MakeNavButton(T("Nav.Statistics"));
        var btnLanguages    = MakeNavButton(T("Nav.Languages"));
        var btnDictionaries = MakeNavButton(T("Nav.Dictionaries"));

        var buttons = new[] { btnGeneral, btnAppearance, btnExclusions, btnAbbreviations, btnStatistics, btnLanguages, btnDictionaries };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = buttons.Length + 1,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var b in buttons)
        {
            b.CheckedChanged += OnNavChanged;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.Controls.Add(b);
        }
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // filler

        nav.Controls.Add(table);

        _navEntries.Add((btnGeneral,       BuildGeneralSection()));
        _navEntries.Add((btnAppearance,    BuildAppearanceSection()));
        _navEntries.Add((btnExclusions,    BuildExclusionsSection()));
        _navEntries.Add((btnAbbreviations, BuildAbbreviationsSection()));
        _navEntries.Add((btnStatistics,    BuildStatisticsSection()));
        _navEntries.Add((btnLanguages,     BuildLanguagesSection()));
        _navEntries.Add((btnDictionaries,  BuildDictionariesSection()));

        return nav;
    }

    Panel BuildContent()
    {
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        foreach (var (_, section) in _navEntries)
        {
            section.Dock    = DockStyle.Fill;
            section.Visible = false;
            content.Controls.Add(section);
        }
        return content;
    }

    void OnNavChanged(object? sender, EventArgs e)
    {
        if (sender is not RadioButton { Checked: true }) return;
        foreach (var (nav, section) in _navEntries)
            section.Visible = ReferenceEquals(nav, sender);
    }

    Panel BuildButtonBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 9, 12, 0) };
        var btnOk     = new Button { Text = T("Common.OK"),     DialogResult = DialogResult.OK,     Width = 84, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var btnCancel = new Button { Text = T("Common.Cancel"), DialogResult = DialogResult.Cancel, Width = 84, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnOk.Click += (_, _) => ApplyAll();
        void Place()
        {
            btnCancel.Location = new Point(bar.Width - bar.Padding.Right - btnCancel.Width, 9);
            btnOk.Location     = new Point(btnCancel.Left - 8 - btnOk.Width, 9);
        }
        Place();
        bar.Resize += (_, _) => Place();
        bar.Controls.AddRange([btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        return bar;
    }

    static GroupBox Card(string title) => new()
    {
        Text         = title,
        Font         = new Font("Segoe UI", 9f, FontStyle.Bold),
        Dock         = DockStyle.Fill,
        AutoSize     = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding      = new Padding(10, 6, 10, 12),
        Margin       = new Padding(0, 0, 0, 12),
    };

    /// <summary>
    /// Stacks a set of GroupBox "cards" vertically with a filler row at the bottom.
    /// Replaces FlowLayoutPanel, which does not support Dock on its children.
    /// </summary>
    static Panel CardStack(params GroupBox[] cards)
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = cards.Length + 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var card in cards)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(card);
        }
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // filler
        page.Controls.Add(table);
        return page;
    }

    /// <summary>
    /// Stacks controls vertically within a card. Replaces FlowLayoutPanel, which does
    /// not support Dock on its children.
    /// </summary>
    static TableLayoutPanel VStack(params Control[] controls)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = controls.Length, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var c in controls)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c.Dock = DockStyle.Fill;
            c.Font = RegularFont;
            c.Margin = new Padding(c.Margin.Left, Math.Max(c.Margin.Top, 3), c.Margin.Right, 3);
            table.Controls.Add(c);
        }
        return table;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // General section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildGeneralSection()
    {
        var iface = Card(T("General.Interface.Title"));
        var ifaceInner = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Font = RegularFont };
        var lblLanguage = new Label { Text = T("General.Interface.Language"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        _languages = GetAvailableLanguages().ToList();
        _languageBox = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 0, 0) };
        foreach (var (_, displayName) in _languages) _languageBox.Items.Add(displayName);
        ifaceInner.Controls.Add(lblLanguage, 0, 0);
        ifaceInner.Controls.Add(_languageBox, 1, 0);
        iface.Controls.Add(ifaceInner);

        var behavior = Card(T("General.Behavior.Title"));
        _autocorrectBox = new CheckBox { Text = T("General.Behavior.EnableAutocorrect"), AutoSize = true };
        _notifyWrongLayoutBox = new CheckBox { Text = T("General.Behavior.NotifyWrongLayout"), AutoSize = true };
        _fixCapsLockBox = new CheckBox { Text = T("General.Behavior.FixCapsLock"), AutoSize = true };
        _autoPunctuationBox = new CheckBox { Text = T("General.Behavior.AutoPunctuation"), AutoSize = true };
        _launchAtStartupBox = new CheckBox { Text = T("General.Behavior.LaunchAtStartup"), AutoSize = true };
        behavior.Controls.Add(VStack(_autocorrectBox, _notifyWrongLayoutBox, _fixCapsLockBox, _autoPunctuationBox, _launchAtStartupBox));

        var hotkey = Card(T("General.Hotkey.Title"));
        var hotkeyInner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Font = RegularFont,
        };
        hotkeyInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        hotkeyInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        hotkeyInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var hint = new Label { Text = T("General.Hotkey.PressKey"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        _capturedKey = _settings.ConvertHotKey;
        _hotkeyBox = new TextBox
        {
            Width     = 130,
            ReadOnly  = true,
            BackColor = SystemColors.Window,
            Text      = KeyLabel(_capturedKey),
            Margin    = new Padding(0, 3, 8, 0),
        };
        _hotkeyBox.GotFocus  += (_, _) => _hotkeyBox.Text = T("General.Hotkey.PressHotkeyPlaceholder");
        _hotkeyBox.LostFocus += (_, _) => _hotkeyBox.Text = KeyLabel(_capturedKey);
        _hotkeyBox.KeyDown   += (_, e) =>
        {
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
        var btnClear = new Button { Text = T("Common.Clear"), Width = 56, Height = 24, Margin = new Padding(0, 1, 0, 0) };
        btnClear.Click += (_, _) => { _capturedKey = Keys.None; _hotkeyBox.Text = KeyLabel(Keys.None); };

        hotkeyInner.Controls.Add(hint, 0, 0);
        hotkeyInner.Controls.Add(_hotkeyBox, 1, 0);
        hotkeyInner.Controls.Add(btnClear, 2, 0);
        hotkey.Controls.Add(hotkeyInner);

        var advanced = Card(T("General.Advanced.Title"));
        var advInner = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2, Font = RegularFont };
        var lblDelay = new Label { Text = T("General.Advanced.ClipboardDelay"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        _clipDelayBox = new NumericUpDown { Width = 70, Minimum = 10, Maximum = 1000, Increment = 10, Margin = new Padding(0, 3, 8, 0) };
        var lblDelayHint = new Label
        {
            Text = T("General.Advanced.ClipboardDelayHint"),
            AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0),
        };
        advInner.Controls.Add(lblDelay, 0, 0);
        advInner.Controls.Add(_clipDelayBox, 1, 0);
        advInner.Controls.Add(lblDelayHint, 2, 0);

        var lblUndo = new Label { Text = T("General.Advanced.UndoThreshold"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 8, 0) };
        _undoThresholdBox = new NumericUpDown { Width = 70, Minimum = 2, Maximum = 10, Margin = new Padding(0, 7, 8, 0) };
        _learnedWordsLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 0, 0) };
        advInner.Controls.Add(lblUndo, 0, 1);
        advInner.Controls.Add(_undoThresholdBox, 1, 1);
        advInner.Controls.Add(_learnedWordsLabel, 2, 1);
        advanced.Controls.Add(advInner);

        return CardStack(iface, behavior, hotkey, advanced);
    }

    static string KeyLabel(Keys k)
    {
        if (k == Keys.None) return T("General.Hotkey.None");
        var parts = new List<string>();
        if ((k & Keys.Control) != 0) parts.Add("Ctrl");
        if ((k & Keys.Shift)   != 0) parts.Add("Shift");
        if ((k & Keys.Alt)     != 0) parts.Add("Alt");
        var baseKey = k & Keys.KeyCode;
        if (baseKey != Keys.None) parts.Add(baseKey.ToString());
        return string.Join("+", parts);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Appearance section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildAppearanceSection()
    {
        var sizeCard = Card(T("Appearance.FlagSize.Title"));
        _sizeSmall  = new RadioButton { Text = T("Appearance.FlagSize.Small"),  AutoSize = true };
        _sizeMedium = new RadioButton { Text = T("Appearance.FlagSize.Medium"), AutoSize = true };
        _sizeLarge  = new RadioButton { Text = T("Appearance.FlagSize.Large"), AutoSize = true };
        sizeCard.Controls.Add(VStack(_sizeSmall, _sizeMedium, _sizeLarge));

        var posCard = Card(T("Appearance.Position.Title"));
        var posTable = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2, Font = RegularFont };
        _cornerTL = new RadioButton { Text = T("Appearance.Position.TopLeft"),     AutoSize = true, Margin = new Padding(0, 4, 20, 4) };
        _cornerTR = new RadioButton { Text = T("Appearance.Position.TopRight"),    AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        _cornerBL = new RadioButton { Text = T("Appearance.Position.BottomLeft"),  AutoSize = true, Margin = new Padding(0, 4, 20, 4) };
        _cornerBR = new RadioButton { Text = T("Appearance.Position.BottomRight"), AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        posTable.Controls.Add(_cornerTL, 0, 0);
        posTable.Controls.Add(_cornerTR, 1, 0);
        posTable.Controls.Add(_cornerBL, 0, 1);
        posTable.Controls.Add(_cornerBR, 1, 1);
        posCard.Controls.Add(posTable);

        return CardStack(sizeCard, posCard);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Exclusions section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildExclusionsSection()
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var hint = new Label
        {
            Text = T("Exclusions.Hint"),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
        };

        _exclusionsList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

        var addRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _exclusionBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = T("Exclusions.Placeholder"), Margin = new Padding(0, 1, 6, 0) };
        var btnAddExcl  = new Button { Text = T("Common.Add"), Width = 60, Height = 24 };
        var btnPickExcl = new Button { Text = T("Exclusions.PickRunningApp"), Width = 110, Height = 24, Margin = new Padding(6, 0, 0, 0) };
        var btnDelExcl  = new Button { Text = T("Common.Remove"), Width = 72, Height = 24, Margin = new Padding(6, 0, 0, 0) };

        void AddExclusionText(string name)
        {
            if (name.Length == 0) return;
            _settings.ExcludedApps.Add(name);
            _exclusionsList.Items.Add(name);
        }
        void AddExclusion()
        {
            AddExclusionText(_exclusionBox.Text.Trim());
            _exclusionBox.Clear();
        }
        btnAddExcl.Click += (_, _) => AddExclusion();
        _exclusionBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddExclusion(); } };
        btnPickExcl.Click += (_, _) =>
        {
            using var picker = new AppPickerForm();
            if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedProcessName is { } name)
                AddExclusionText(name);
        };
        btnDelExcl.Click += (_, _) =>
        {
            int idx = _exclusionsList.SelectedIndex;
            if (idx < 0) return;
            _settings.ExcludedApps.RemoveAt(idx);
            _exclusionsList.Items.RemoveAt(idx);
        };

        var addRowButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
        addRowButtons.Controls.AddRange([btnAddExcl, btnPickExcl, btnDelExcl]);
        addRow.Controls.Add(_exclusionBox, 0, 0);
        addRow.Controls.Add(addRowButtons, 1, 0);

        table.Controls.Add(hint,     0, 0);
        table.Controls.Add(_exclusionsList, 0, 1);
        table.Controls.Add(addRow,   0, 2);
        page.Controls.Add(table);
        return page;
    }

    void LoadExclusionsList()
    {
        _exclusionsList.Items.Clear();
        foreach (var app in _settings.ExcludedApps) _exclusionsList.Items.Add(app);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Abbreviations section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildAbbreviationsSection()
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var label  = new Label  { Text = T("Abbreviations.Hint"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 0) };
        var btnAdd = new Button { Text = T("Common.Add"),    Width = 60, Height = 24, Margin = new Padding(4, 2, 4, 0) };
        var btnDel = new Button { Text = T("Common.Remove"), Width = 72, Height = 24, Margin = new Padding(0, 2, 0, 0) };
        toolbar.Controls.Add(label,  0, 0);
        toolbar.Controls.Add(btnAdd, 1, 0);
        toolbar.Controls.Add(btnDel, 2, 0);

        _abbrevListBox = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = false, IntegralHeight = false };
        _abbrevListBox.SelectedIndexChanged += (_, _) => RefreshAbbrevDetail();
        _abbrevListBox.ItemCheck += (_, e) =>
        {
            if (_suppressAbbrev) return;
            if (_abbrevListBox.Items[e.Index] is Abbreviation a)
                a.Enabled = e.NewValue == CheckState.Checked;
        };
        var listPanel = new Panel { Dock = DockStyle.Fill };
        listPanel.Controls.Add(_abbrevListBox);

        var detail = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 8, 0, 8),
        };
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTrigger = new Label { Text = T("Abbreviations.Trigger"), AutoSize = true, Anchor = AnchorStyles.Left };
        _abbrevTriggerBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 4) };
        var lblExpansion = new Label { Text = T("Abbreviations.ExpandsTo"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
        _abbrevExpansionBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 2, 0, 0) };

        detail.Controls.Add(lblTrigger,         0, 0);
        detail.Controls.Add(_abbrevTriggerBox,  1, 0);
        detail.Controls.Add(lblExpansion,       0, 1);
        detail.Controls.Add(_abbrevExpansionBox, 1, 1);

        btnAdd.Click += (_, _) =>
        {
            var a = new Abbreviation { Trigger = "new", Expansion = "", Enabled = true };
            _abbreviations.Add(a);
            _suppressAbbrev = true;
            int idx = _abbrevListBox.Items.Add(a);
            _abbrevListBox.SetItemChecked(idx, true);
            _suppressAbbrev = false;
            _abbrevListBox.SelectedIndex = idx;
        };
        btnDel.Click += (_, _) =>
        {
            int idx = _abbrevListBox.SelectedIndex;
            if (idx < 0) return;
            if (_abbrevListBox.Items[idx] is Abbreviation a) _abbreviations.Remove(a);
            _abbrevListBox.Items.RemoveAt(idx);
            if (_abbrevListBox.Items.Count == 0) RefreshAbbrevDetail();
        };
        _abbrevTriggerBox.TextChanged += (_, _) =>
        {
            if (_suppressAbbrev || SelectedAbbrev() is not { } a) return;
            a.Trigger = _abbrevTriggerBox.Text.Trim();
            _abbrevListBox.Refresh();
        };
        _abbrevExpansionBox.TextChanged += (_, _) =>
        {
            if (_suppressAbbrev || SelectedAbbrev() is not { } a) return;
            a.Expansion = _abbrevExpansionBox.Text;
        };

        outer.Controls.Add(toolbar,   0, 0);
        outer.Controls.Add(listPanel, 0, 1);
        outer.Controls.Add(detail,    0, 2);
        page.Controls.Add(outer);
        return page;
    }

    void LoadAbbrevList()
    {
        _suppressAbbrev = true;
        _abbrevListBox.Items.Clear();
        foreach (var a in _abbreviations)
        {
            int idx = _abbrevListBox.Items.Add(a);
            _abbrevListBox.SetItemChecked(idx, a.Enabled);
        }
        _suppressAbbrev = false;

        if (_abbrevListBox.Items.Count > 0) _abbrevListBox.SelectedIndex = 0;
        RefreshAbbrevDetail();
    }

    void RefreshAbbrevDetail()
    {
        var a = SelectedAbbrev();
        bool hasSel = a is not null;
        _abbrevTriggerBox.Enabled   = hasSel;
        _abbrevExpansionBox.Enabled = hasSel;

        _suppressAbbrev = true;
        _abbrevTriggerBox.Text   = a?.Trigger   ?? "";
        _abbrevExpansionBox.Text = a?.Expansion ?? "";
        _suppressAbbrev = false;
    }

    Abbreviation? SelectedAbbrev() => _abbrevListBox.SelectedItem as Abbreviation;

    // ═══════════════════════════════════════════════════════════════════════
    // Statistics section
    // ═══════════════════════════════════════════════════════════════════════

    Label _statWords = null!, _statChars = null!, _statManual = null!, _statTime = null!, _statLearned = null!;

    Panel BuildStatisticsSection()
    {
        var card = Card(T("Statistics.Title"));
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        (Label Key, Label Value) Row(string label)
        {
            var key = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 8, 0) };
            var val = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            grid.Controls.Add(key); grid.Controls.Add(val);
            return (key, val);
        }

        (_, _statWords)   = Row(T("Statistics.WordsCorrected"));
        (_, _statChars)   = Row(T("Statistics.CharactersFixed"));
        (_, _statManual)  = Row(T("Statistics.ManualConversions"));
        (_, _statTime)    = Row(T("Statistics.TimeSaved"));
        (_, _statLearned) = Row(T("Statistics.LearnedWords"));

        var hint = new Label
        {
            Text = T("Statistics.PrivacyHint"),
            AutoSize = true, ForeColor = SystemColors.GrayText, Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 10),
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var btnResetStats   = new Button { Text = T("Statistics.ResetStats"), Width = 110, Height = 24 };
        var btnResetLearned = new Button { Text = T("Statistics.ResetLearned"), Width = 130, Height = 24, Margin = new Padding(8, 0, 0, 0) };
        btnResetStats.Click += (_, _) =>
        {
            _stats.WordsCorrected = 0;
            _stats.CharactersFixed = 0;
            _stats.ManualConversions = 0;
            RefreshStats();
        };
        btnResetLearned.Click += (_, _) =>
        {
            _resetUndoLearning(); // clears the shared _undoCounts dictionary itself
            RefreshStats();
            RefreshLearnedWordsLabel();
        };
        buttons.Controls.AddRange([btnResetStats, btnResetLearned]);

        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
        inner.Controls.Add(grid);
        inner.Controls.Add(hint);
        inner.Controls.Add(buttons);
        card.Controls.Add(inner);

        RefreshStats();
        return CardStack(card);
    }

    void RefreshStats()
    {
        _statWords.Text  = _stats.WordsCorrected.ToString("N0");
        _statChars.Text  = _stats.CharactersFixed.ToString("N0");
        _statManual.Text = _stats.ManualConversions.ToString("N0");

        var seconds = _stats.CharactersFixed * 0.3;
        _statTime.Text = seconds < 60 ? $"~{seconds:N0}s" : $"~{seconds / 60:N0}m";

        _statLearned.Text = _undoCounts.Count(kv => kv.Value >= _settings.UndoLearnThreshold).ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Languages section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildLanguagesSection()
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var searchLabel = new Label
        {
            Text = T("Languages.Hint"),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        };

        _searchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = T("Languages.SearchPlaceholder") };
        _searchBox.TextChanged += (_, _) => FilterList(_searchBox.Text);

        _layoutList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = false, IntegralHeight = false };
        _layoutList.ItemCheck += OnItemCheck;

        var hint = new Label
        {
            Text = T("Languages.SignOutHint"),
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
                T("Languages.RemoveFailedPrefix") +
                string.Join("\n", failed) + T("Languages.RemoveFailedSuffix"),
                T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dictionaries section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildDictionariesSection()
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        outer.Controls.Add(BuildDictToolbar(),   0, 0);
        outer.Controls.Add(BuildDictListPanel(), 0, 1);
        outer.Controls.Add(BuildDictDetail(),    0, 2);

        page.Controls.Add(outer);
        return page;
    }

    Panel BuildDictToolbar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label  = new Label  { Text = T("Dictionaries.Hint"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 0) };
        var btnAdd = new Button { Text = T("Common.Add"),    Width = 60, Height = 24, Margin = new Padding(4, 2, 4, 0) };
        var btnDel = new Button { Text = T("Common.Remove"), Width = 72, Height = 24, Margin = new Padding(0, 2, 0, 0) };

        btnAdd.Click += (_, _) => AddDictionary();
        btnDel.Click += (_, _) => RemoveDictionary();

        panel.Controls.Add(label,  0, 0);
        panel.Controls.Add(btnAdd, 1, 0);
        panel.Controls.Add(btnDel, 2, 0);
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
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 8, 0, 8),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblName = new Label { Text = T("Dictionaries.Name"), AutoSize = true, Anchor = AnchorStyles.Left };
        _dictNameBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 4) };
        var lblLang  = new Label  { Text = T("Dictionaries.Lang"), AutoSize = true, Anchor = AnchorStyles.Left };
        _dictLangBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 4) };

        var lblUrl   = new Label { Text = T("Dictionaries.Url"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
        _dictUrlBox  = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 2, 4, 4) };
        _dictDlBtn   = new Button  { Text = T("Dictionaries.Download"), Dock = DockStyle.Fill, Height = 24, Margin = new Padding(0, 2, 0, 4) };
        _dictDlBtn.Click += async (_, _) => await DownloadSelectedDict();

        _dictStatus = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };

        panel.Controls.Add(lblName,      0, 0);
        panel.Controls.Add(_dictNameBox, 1, 0);
        panel.Controls.Add(lblLang,      2, 0);
        panel.Controls.Add(_dictLangBox, 3, 0);

        panel.Controls.Add(lblUrl,      0, 1);
        panel.SetColumnSpan(_dictUrlBox, 2);
        panel.Controls.Add(_dictUrlBox, 1, 1);
        panel.SetColumnSpan(_dictDlBtn, 2);
        panel.Controls.Add(_dictDlBtn, 3, 1);

        panel.SetColumnSpan(_dictStatus, 4);
        panel.Controls.Add(_dictStatus, 0, 2);

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
        _dictStatus.Text      = count > 0 ? string.Format(T("Dictionaries.WordsLoaded"), count.ToString("N0")) : T("Dictionaries.NotDownloaded");
        _dictStatus.ForeColor = count > 0 ? Color.SeaGreen : SystemColors.GrayText;
    }

    void AddDictionary()
    {
        var dict = new UserDictionary { Name = T("Dictionaries.NewDictionaryName"), Enabled = true };
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
            MessageBox.Show(T("Dictionaries.EnterLangCode"), T("Common.AppName"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(T("Dictionaries.EnterValidUrl"), T("Common.AppName"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _dictDlBtn.Enabled = false;
        var origText = _dictDlBtn.Text;
        try
        {
            var progress = new Progress<string>(msg => _dictDlBtn.Text = msg);
            int count = await WordListDownloader.DownloadAsync(lang, url, progress);
            _langDicts.Reload(_dicts);
            RefreshDictStatus(dict);
            MessageBox.Show(string.Format(T("Dictionaries.DownloadSuccess"), count.ToString("N0"), lang.ToUpperInvariant()),
                T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(string.Format(T("Dictionaries.NetworkError"), ex.Message), T("Common.AppName"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(T("Dictionaries.GenericError"), ex.Message), T("Common.AppName"),
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
    // Load / commit
    // ═══════════════════════════════════════════════════════════════════════

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

        _autocorrectBox.Checked       = _settings.AutocorrectEnabled;
        _notifyWrongLayoutBox.Checked = _settings.NotifyWrongLayout;
        _fixCapsLockBox.Checked       = _settings.FixCapsLock;
        _autoPunctuationBox.Checked   = _settings.AutoPunctuation;
        _clipDelayBox.Value           = Math.Clamp(_settings.ClipboardDelayMs, 10, 1000);
        _undoThresholdBox.Value       = Math.Clamp(_settings.UndoLearnThreshold, 2, 10);
        _launchAtStartupBox.Checked   = StartupManager.IsEnabled();

        int langIdx = _languages.FindIndex(l => l.Code == _settings.UiLanguage);
        _languageBox.SelectedIndex = langIdx >= 0 ? langIdx : 0;

        RefreshLearnedWordsLabel();
    }

    void RefreshLearnedWordsLabel()
    {
        int learned = _undoCounts.Count(kv => kv.Value >= _settings.UndoLearnThreshold);
        _learnedWordsLabel.Text = string.Format(T("General.Advanced.LearnedWordsCount"), learned);
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

        _settings.ConvertHotKey     = _capturedKey;
        _settings.AutocorrectEnabled = _autocorrectBox.Checked;
        _settings.NotifyWrongLayout = _notifyWrongLayoutBox.Checked;
        _settings.FixCapsLock       = _fixCapsLockBox.Checked;
        _settings.AutoPunctuation   = _autoPunctuationBox.Checked;
        _settings.ClipboardDelayMs  = (int)_clipDelayBox.Value;
        _settings.UndoLearnThreshold = (int)_undoThresholdBox.Value;

        if (_languageBox.SelectedIndex >= 0)
            _settings.UiLanguage = _languages[_languageBox.SelectedIndex].Code;

        StartupManager.SetEnabled(_launchAtStartupBox.Checked);
    }

    void ApplyAll()
    {
        ApplyLanguageChanges();
        SaveDisplayValues();
    }
}
