using LangSwitch.Core;
using LangSwitch.Settings;
using System.Diagnostics.CodeAnalysis;
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

    int LearnedWordCount => _undoCounts.Count(kv => kv.Value >= _settings.UndoLearnThreshold);
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
    CheckBox _scrollToSwitchBox = null!;
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
    CheckedListEditor<Abbreviation> _abbrevEditor = null!;
    TextBox        _abbrevTriggerBox   = null!;
    TextBox        _abbrevExpansionBox = null!;

    // ── Dictionaries section ─────────────────────────────────────────────────
    CheckedListBox _dictListBox  = null!;
    CheckedListEditor<UserDictionary> _dictEditor = null!;
    TextBox        _dictNameBox  = null!;
    TextBox        _dictLangBox  = null!;
    TextBox        _dictUrlBox   = null!;
    Label          _dictStatus   = null!;
    Button         _dictDlBtn    = null!;
    Button         _dictImportBtn = null!;

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
        // Wide enough for the Exclusions row (textbox + Pick + Browse + Add/Remove column)
        // at full localized button widths without crowding — narrower dialogs are what kept
        // forcing buttons to compete for space across several of these sections.
        Size            = new Size(680, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f);
        AppIcon.ApplyTo(this);

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
        // Right padding matches BuildContent's Panel (16px) so the OK/Cancel row's right edge
        // lines up with the content above it instead of sitting a few pixels further left.
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 9, 16, 0) };
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
    /// <summary>
    /// Measures how wide a button needs to be to show <paramref name="text"/> in full,
    /// using the actual font it will render with — used instead of a guessed fixed pixel
    /// Width, since localized button labels (Russian/Ukrainian especially) are often
    /// significantly longer than their English source and a guessed width reliably clips
    /// them.
    /// </summary>
    int MeasureButtonWidth(string text, int extraPadding = 24) =>
        TextRenderer.MeasureText(text, Font).Width + extraPadding;

    /// <summary>
    /// The single place every plain action button (Add/Remove/Pick/Clear/Reset…) in this
    /// dialog gets created — guarantees every button is the same height and has the same
    /// zero top/bottom margin, so buttons placed next to each other in a row are always
    /// level with each other. Hand-setting Margin per button (what this replaces) is exactly
    /// what let buttons in the same row drift out of vertical alignment — one button with a
    /// default Margin.Top of 3 and its neighbor with an explicit 0 sit 3px apart.
    /// Horizontal spacing between buttons in a row is the caller's job (a right margin on
    /// all but the last button, or a FlowLayoutPanel), since that varies by layout.
    /// </summary>
    Button MakeButton(string text) => new()
    {
        Text = text,
        Width = MeasureButtonWidth(text),
        Height = 28,
        Margin = new Padding(0),
    };

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
        _scrollToSwitchBox  = new CheckBox { Text = T("General.Behavior.ScrollToSwitchLayout"), AutoSize = true };
        behavior.Controls.Add(VStack(_autocorrectBox, _notifyWrongLayoutBox, _fixCapsLockBox, _autoPunctuationBox, _launchAtStartupBox, _scrollToSwitchBox));

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
        var btnClear = MakeButton(T("Common.Clear"));
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
        var combo = HotkeyFormat.Combo(k);
        return combo.Length > 0 ? combo : T("General.Hotkey.None");
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
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hint = new Label
        {
            Text = T("Exclusions.Hint"),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
        };

        _exclusionsList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

        // A single 2-column grid (textbox/Pick on the left, Add/Remove stacked on the right)
        // rather than two independently-sized rows — a TableLayoutPanel's AutoSize column
        // width is the max of every cell in that column, so Add and Remove are guaranteed
        // the same width and the same right edge by construction, not by matching numbers
        // by hand across two separate containers (which is what kept drifting out of sync).
        var addGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 6, 0, 0),
        };
        addGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        addGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        addGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _exclusionBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = T("Exclusions.Placeholder"), Margin = new Padding(0, 3, 6, 4) };
        // MinimumSize (not just Dock=Fill) is what the AutoSize column actually measures —
        // a Dock=Fill control alone reports no real preferred size, so the column would
        // collapse. The column's final width is the max MinimumSize across both rows, and
        // Dock=Fill then stretches each button to that resolved width — guaranteeing Add and
        // Remove end up pixel-identical in width without hand-matching numbers.
        var btnAddExcl = new Button { Text = T("Common.Add"), Dock = DockStyle.Fill, MinimumSize = new Size(MeasureButtonWidth(T("Common.Add")), 28), Margin = new Padding(0, 0, 0, 4) };

        var btnPickExcl = MakeButton(T("Exclusions.PickRunningApp"));
        var btnBrowseExcl = MakeButton(T("Exclusions.BrowseExe"));
        btnBrowseExcl.Margin = new Padding(6, 0, 0, 0);
        var pickRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = true, Margin = new Padding(0, 0, 6, 2),
        };
        pickRow.Controls.AddRange([btnPickExcl, btnBrowseExcl]);

        var btnDelExcl  = new Button { Text = T("Common.Remove"), Dock = DockStyle.Fill, MinimumSize = new Size(MeasureButtonWidth(T("Common.Remove")), 28), Margin = new Padding(0, 0, 0, 2) };

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
        btnBrowseExcl.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title  = T("Exclusions.BrowseDialogTitle"),
                Filter = T("Exclusions.BrowseFilter"),
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
                AddExclusionText(Path.GetFileName(ofd.FileName));
        };
        btnDelExcl.Click += (_, _) =>
        {
            int idx = _exclusionsList.SelectedIndex;
            if (idx < 0) return;
            _settings.ExcludedApps.RemoveAt(idx);
            _exclusionsList.Items.RemoveAt(idx);
        };

        addGrid.Controls.Add(_exclusionBox, 0, 0);
        addGrid.Controls.Add(btnAddExcl,    1, 0);
        addGrid.Controls.Add(pickRow,       0, 1);
        addGrid.Controls.Add(btnDelExcl,    1, 1);

        table.Controls.Add(hint,            0, 0);
        table.Controls.Add(_exclusionsList, 0, 1);
        table.Controls.Add(addGrid,         0, 2);
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
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var label  = new Label  { Text = T("Abbreviations.Hint"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 0) };
        var btnAdd = MakeButton(T("Common.Add"));
        btnAdd.Margin = new Padding(4, 0, 4, 0);
        var btnDel = MakeButton(T("Common.Remove"));
        toolbar.Controls.Add(label,  0, 0);
        toolbar.Controls.Add(btnAdd, 1, 0);
        toolbar.Controls.Add(btnDel, 2, 0);

        _abbrevListBox = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = false, IntegralHeight = false };
        _abbrevEditor  = new CheckedListEditor<Abbreviation>(_abbrevListBox, a => a.Enabled, (a, v) => a.Enabled = v);
        _abbrevListBox.SelectedIndexChanged += (_, _) => RefreshAbbrevDetail();
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
            _abbrevEditor.Add(a);
        };
        btnDel.Click += (_, _) =>
        {
            var removed = _abbrevEditor.RemoveSelected();
            if (removed is null) return;
            _abbreviations.Remove(removed);
            if (_abbrevListBox.Items.Count == 0) RefreshAbbrevDetail();
        };
        _abbrevTriggerBox.TextChanged += (_, _) => _abbrevEditor.WhenSelected(a =>
        {
            a.Trigger = _abbrevTriggerBox.Text.Trim();
            _abbrevEditor.RefreshRows();
        });
        _abbrevExpansionBox.TextChanged += (_, _) => _abbrevEditor.WhenSelected(a =>
            a.Expansion = _abbrevExpansionBox.Text);

        outer.Controls.Add(toolbar,   0, 0);
        outer.Controls.Add(listPanel, 0, 1);
        outer.Controls.Add(detail,    0, 2);
        page.Controls.Add(outer);
        return page;
    }

    void LoadAbbrevList()
    {
        _abbrevEditor.LoadList(_abbreviations);
        RefreshAbbrevDetail();
    }

    void RefreshAbbrevDetail()
    {
        var a = _abbrevEditor.SelectedItem;
        bool hasSel = a is not null;
        _abbrevTriggerBox.Enabled   = hasSel;
        _abbrevExpansionBox.Enabled = hasSel;

        _abbrevEditor.WithSuppressed(() =>
        {
            _abbrevTriggerBox.Text   = a?.Trigger   ?? "";
            _abbrevExpansionBox.Text = a?.Expansion ?? "";
        });
    }

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
        var btnResetStats   = MakeButton(T("Statistics.ResetStats"));
        var btnResetLearned = MakeButton(T("Statistics.ResetLearned"));
        btnResetLearned.Margin = new Padding(8, 0, 0, 0);
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

        _statLearned.Text = LearnedWordCount.ToString("N0");
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

        SyncDictionariesToInstalledLayouts();
    }

    /// <summary>
    /// Keeps the dictionary list a pure reflection of whatever layouts are actually
    /// installed — add a layout, its language's dictionary appears (and gets downloaded,
    /// see TrayApplicationContext.DownloadMissingDictionaries); remove a layout, its
    /// dictionary disappears. No hardcoded language list anywhere in this path.
    /// </summary>
    void SyncDictionariesToInstalledLayouts()
    {
        var installedLangs = WindowsLayoutDetector.GetInstalledLayouts()
            .Select(l => l.Culture.TwoLetterISOLanguageName.ToLowerInvariant())
            .ToHashSet();

        foreach (var lang in installedLangs)
        {
            if (_dicts.Any(d => string.Equals(d.Lang, lang, StringComparison.OrdinalIgnoreCase))) continue;
            _dicts.Add(new UserDictionary
            {
                Name    = CultureDisplayName(lang),
                Lang    = lang,
                Enabled = true,
                Url     = WordListDownloader.UrlFor(lang),
            });
        }

        _dicts.RemoveAll(d => !string.IsNullOrEmpty(d.Lang) && !installedLangs.Contains(d.Lang.ToLowerInvariant()));
    }

    static string CultureDisplayName(string lang)
    {
        try { return new System.Globalization.CultureInfo(lang).EnglishName; }
        catch { return lang.ToUpperInvariant(); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dictionaries section
    // ═══════════════════════════════════════════════════════════════════════

    Panel BuildDictionariesSection()
    {
        var page  = new Panel { Dock = DockStyle.Fill };
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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
        var btnAdd = MakeButton(T("Common.Add"));
        btnAdd.Margin = new Padding(4, 0, 4, 0);
        var btnDel = MakeButton(T("Common.Remove"));

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
        _dictEditor = new CheckedListEditor<UserDictionary>(_dictListBox, d => d.Enabled, (d, v) => d.Enabled = v);
        _dictListBox.SelectedIndexChanged += (_, _) => RefreshDictDetail();

        panel.Controls.Add(_dictListBox);
        return panel;
    }

    Panel BuildDictDetail()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4,
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
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblName = new Label { Text = T("Dictionaries.Name"), AutoSize = true, Anchor = AnchorStyles.Left };
        _dictNameBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 4) };
        var lblLang  = new Label  { Text = T("Dictionaries.Lang"), AutoSize = true, Anchor = AnchorStyles.Left };
        _dictLangBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 4) };

        var lblUrl   = new Label { Text = T("Dictionaries.Url"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
        _dictUrlBox  = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 2, 4, 4) };
        _dictDlBtn   = new Button  { Text = T("Dictionaries.Download"), Dock = DockStyle.Fill, Height = 28, Margin = new Padding(0, 2, 0, 4) };
        _dictDlBtn.Click += async (_, _) => await DownloadSelectedDict();

        _dictImportBtn = new Button { Text = T("Dictionaries.ImportFromFile"), AutoSize = true, Height = 28, Margin = new Padding(0, 2, 0, 4), Anchor = AnchorStyles.Left };
        _dictImportBtn.Click += (_, _) => ImportSelectedDict();

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

        panel.SetColumnSpan(_dictImportBtn, 4);
        panel.Controls.Add(_dictImportBtn, 0, 2);

        panel.SetColumnSpan(_dictStatus, 4);
        panel.Controls.Add(_dictStatus, 0, 3);

        _dictNameBox.TextChanged += (_, _) => _dictEditor.WhenSelected(d =>
        {
            d.Name = _dictNameBox.Text;
            _dictEditor.RefreshRows();
        });
        _dictLangBox.TextChanged += (_, _) => _dictEditor.WhenSelected(d =>
        {
            d.Lang = _dictLangBox.Text.Trim().ToLowerInvariant();
            _dictEditor.RefreshRows();
            RefreshDictStatus(d);
        });
        _dictUrlBox.TextChanged += (_, _) => _dictEditor.WhenSelected(d => d.Url = _dictUrlBox.Text.Trim());

        return panel;
    }

    void LoadDictList()
    {
        _dictEditor.LoadList(_dicts);
        RefreshDictDetail();
    }

    void RefreshDictDetail()
    {
        var dict    = _dictEditor.SelectedItem;
        bool hasSel = dict is not null;

        _dictNameBox.Enabled = hasSel;
        _dictLangBox.Enabled = hasSel;
        _dictUrlBox.Enabled  = hasSel;
        _dictDlBtn.Enabled   = hasSel;
        _dictImportBtn.Enabled = hasSel;

        _dictEditor.WithSuppressed(() =>
        {
            _dictNameBox.Text = dict?.Name ?? "";
            _dictLangBox.Text = dict?.Lang ?? "";
            _dictUrlBox.Text  = dict?.Url  ?? "";
        });

        if (dict is not null) RefreshDictStatus(dict);
        else _dictStatus.Text = "";
    }

    void RefreshDictStatus(UserDictionary dict)
    {
        var count  = string.IsNullOrEmpty(dict.Lang) ? 0 : _langDicts.WordCount(dict.Lang);
        var source = string.IsNullOrEmpty(dict.Lang) ? DictionarySource.None : _langDicts.SourceOf(dict.Lang);

        _dictStatus.Text = source switch
        {
            DictionarySource.Hunspell      => string.Format(T("Dictionaries.WordsLoaded"), count.ToString("N0"), T("Dictionaries.SourceHunspell")),
            DictionarySource.FrequencyList => string.Format(T("Dictionaries.WordsLoaded"), count.ToString("N0"), T("Dictionaries.SourceFrequencyList")),
            DictionarySource.Builtin       => string.Format(T("Dictionaries.WordsLoaded"), count.ToString("N0"), T("Dictionaries.SourceBuiltin")),
            _                              => T("Dictionaries.NotDownloaded"),
        };
        _dictStatus.ForeColor = count > 0 ? Color.SeaGreen : SystemColors.GrayText;
    }

    void AddDictionary()
    {
        var dict = new UserDictionary { Name = T("Dictionaries.NewDictionaryName"), Enabled = true };
        _dicts.Add(dict);
        _dictEditor.Add(dict);
    }

    void RemoveDictionary()
    {
        var removed = _dictEditor.RemoveSelected();
        if (removed is null) return;
        _dicts.Remove(removed);
        if (_dictListBox.Items.Count == 0) RefreshDictDetail();
    }

    /// <summary>Validates there's a selected dictionary with a non-empty language code,
    /// showing the standard warning and returning false if not — shared by
    /// DownloadSelectedDict and ImportSelectedDict, which otherwise repeat this exact guard.</summary>
    bool TryGetSelectedLang([NotNullWhen(true)] out UserDictionary? dict, out string lang)
    {
        dict = SelectedDict();
        lang = _dictLangBox.Text.Trim().ToLowerInvariant();
        if (dict is null) return false;
        if (string.IsNullOrEmpty(lang))
        {
            MessageBox.Show(T("Dictionaries.EnterLangCode"), T("Common.AppName"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    void ShowGenericError(Exception ex) =>
        MessageBox.Show(string.Format(T("Dictionaries.GenericError"), ex.Message), T("Common.AppName"),
            MessageBoxButtons.OK, MessageBoxIcon.Error);

    async Task DownloadSelectedDict()
    {
        if (!TryGetSelectedLang(out var dict, out var lang)) return;

        var url = _dictUrlBox.Text.Trim();
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
            var progress = new Progress<string>(msg => { if (!IsDisposed) _dictDlBtn.Text = msg; });
            int count = await WordListDownloader.DownloadAsync(lang, url, progress);
            if (IsDisposed) return; // dialog was closed while the download was in flight
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
            ShowGenericError(ex);
        }
        finally
        {
            if (!IsDisposed)
            {
                _dictDlBtn.Text    = origText;
                _dictDlBtn.Enabled = true;
            }
        }
    }

    /// <summary>
    /// Lets the user pick a dictionary file from disk — a Hunspell .dic (with a matching .aff
    /// expected next to it) or a flat .txt word list — and installs it under WordLists/{lang}
    /// as if it had been downloaded, with no internet connection required.
    /// </summary>
    void ImportSelectedDict()
    {
        if (!TryGetSelectedLang(out var dict, out var lang)) return;

        using var ofd = new OpenFileDialog
        {
            Title  = T("Dictionaries.ImportDialogTitle"),
            Filter = T("Dictionaries.ImportFilter"),
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            Directory.CreateDirectory(LanguageDictionaries.WordListDir);
            var ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();

            if (ext == ".dic")
            {
                var affSource = Path.ChangeExtension(ofd.FileName, ".aff");
                if (!File.Exists(affSource))
                {
                    MessageBox.Show(string.Format(T("Dictionaries.ImportMissingAff"), Path.GetFileName(ofd.FileName)),
                        T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                File.Copy(ofd.FileName, Path.Combine(LanguageDictionaries.WordListDir, lang + ".dic"), overwrite: true);
                File.Copy(affSource,    Path.Combine(LanguageDictionaries.WordListDir, lang + ".aff"), overwrite: true);
            }
            else
            {
                File.Copy(ofd.FileName, Path.Combine(LanguageDictionaries.WordListDir, lang + ".txt"), overwrite: true);
            }

            _langDicts.Reload(_dicts);
            RefreshDictStatus(dict);

            var count = _langDicts.WordCount(lang);
            MessageBox.Show(string.Format(T("Dictionaries.ImportSuccess"), count.ToString("N0"), lang.ToUpperInvariant(), Path.GetFileName(ofd.FileName)),
                T("Common.AppName"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowGenericError(ex);
        }
    }

    UserDictionary? SelectedDict() => _dictEditor.SelectedItem;

    // ═══════════════════════════════════════════════════════════════════════
    // Load / commit
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Single source of truth for the PopupSize radio group, used by both
    /// LoadDisplayValues and SaveDisplayValues instead of a switch and a ternary chain that
    /// have to be kept in sync by hand.</summary>
    (RadioButton Button, FlagDisplaySize Value)[] SizeOptions =>
    [
        (_sizeSmall, FlagDisplaySize.Small),
        (_sizeMedium, FlagDisplaySize.Medium),
        (_sizeLarge, FlagDisplaySize.Large),
    ];

    /// <summary>Same idea as <see cref="SizeOptions"/>, for the PopupPosition radio group.</summary>
    (RadioButton Button, ScreenCorner Value)[] CornerOptions =>
    [
        (_cornerTL, ScreenCorner.TopLeft),
        (_cornerTR, ScreenCorner.TopRight),
        (_cornerBL, ScreenCorner.BottomLeft),
        (_cornerBR, ScreenCorner.BottomRight),
    ];

    void LoadDisplayValues()
    {
        SizeOptions.FirstOrDefault(o => o.Value == _settings.PopupSize, (Button: _sizeMedium, Value: FlagDisplaySize.Medium))
            .Button.Checked = true;
        CornerOptions.FirstOrDefault(o => o.Value == _settings.PopupPosition, (Button: _cornerBR, Value: ScreenCorner.BottomRight))
            .Button.Checked = true;

        _autocorrectBox.Checked       = _settings.AutocorrectEnabled;
        _notifyWrongLayoutBox.Checked = _settings.NotifyWrongLayout;
        _fixCapsLockBox.Checked       = _settings.FixCapsLock;
        _autoPunctuationBox.Checked   = _settings.AutoPunctuation;
        _scrollToSwitchBox.Checked    = _settings.ScrollToSwitchLayout;
        _clipDelayBox.Value           = Math.Clamp(_settings.ClipboardDelayMs, 10, 1000);
        _undoThresholdBox.Value       = Math.Clamp(_settings.UndoLearnThreshold, 2, 10);
        _launchAtStartupBox.Checked   = StartupManager.IsEnabled();

        int langIdx = _languages.FindIndex(l => l.Code == _settings.UiLanguage);
        _languageBox.SelectedIndex = langIdx >= 0 ? langIdx : 0;

        RefreshLearnedWordsLabel();
    }

    void RefreshLearnedWordsLabel() =>
        _learnedWordsLabel.Text = string.Format(T("General.Advanced.LearnedWordsCount"), LearnedWordCount);

    void SaveDisplayValues()
    {
        _settings.PopupSize = SizeOptions.First(o => o.Button.Checked).Value;
        _settings.PopupPosition = CornerOptions.First(o => o.Button.Checked).Value;

        _settings.ConvertHotKey     = _capturedKey;
        _settings.AutocorrectEnabled = _autocorrectBox.Checked;
        _settings.NotifyWrongLayout = _notifyWrongLayoutBox.Checked;
        _settings.FixCapsLock       = _fixCapsLockBox.Checked;
        _settings.AutoPunctuation   = _autoPunctuationBox.Checked;
        _settings.ScrollToSwitchLayout = _scrollToSwitchBox.Checked;
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
