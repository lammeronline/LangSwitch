using LangSwitch.Core;
using LangSwitch.Settings;

namespace LangSwitch.UI;

sealed class SettingsForm : Form
{
    readonly AppSettings _settings;

    // Languages tab state
    List<AvailableLayout> _allLayouts = [];
    readonly Dictionary<string, bool> _checkedState = new(StringComparer.OrdinalIgnoreCase);
    CheckedListBox _layoutList = null!;
    TextBox _searchBox = null!;

    // Display tab
    RadioButton _sizeSmall = null!, _sizeMedium = null!, _sizeLarge = null!;
    RadioButton _cornerTL = null!, _cornerTR = null!, _cornerBL = null!, _cornerBR = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Build();
        LoadAvailableLayouts();
        LoadDisplayValues();
    }

    void Build()
    {
        Text = "LangSwitch — Settings";
        Size = new Size(460, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildLanguagesTab());
        tabs.TabPages.Add(BuildDisplayTab());
        Controls.Add(tabs);
        Controls.Add(BuildButtonBar());
    }

    Panel BuildButtonBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46 };

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 84, Height = 28 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 84, Height = 28 };

        btnOk.Click += (_, _) => ApplyAll();

        void Place()
        {
            btnCancel.Location = new Point(bar.Width - 92, 9);
            btnOk.Location = new Point(btnCancel.Left - 90, 9);
        }
        Place();
        bar.Resize += (_, _) => Place();
        bar.Controls.AddRange([btnOk, btnCancel]);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        return bar;
    }

    // ── Languages tab ────────────────────────────────────────────────────────

    TabPage BuildLanguagesTab()
    {
        var page = new TabPage("Languages");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(6),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // search label
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // search box
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // list
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // hint

        var searchLabel = new Label
        {
            Text = "Keyboard layouts (check to install, uncheck to remove from Windows):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _searchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search…" };
        _searchBox.TextChanged += (_, _) => FilterList(_searchBox.Text);

        _layoutList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };
        _layoutList.ItemCheck += OnItemCheck;

        var hint = new Label
        {
            Text = "Some changes may require signing out and back in to take full effect.",
            ForeColor = SystemColors.GrayText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        table.Controls.Add(searchLabel, 0, 0);
        table.Controls.Add(_searchBox, 0, 1);
        table.Controls.Add(_layoutList, 0, 2);
        table.Controls.Add(hint, 0, 3);
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
        // Determine what changed
        var toInstall   = _allLayouts.Where(l => !l.IsInstalled &&  _checkedState.GetValueOrDefault(l.Klid)).ToList();
        var toUninstall = _allLayouts.Where(l =>  l.IsInstalled && !_checkedState.GetValueOrDefault(l.Klid)).ToList();

        // Install new layouts
        foreach (var layout in toInstall)
        {
            try { LayoutManager.Install(layout.Klid); }
            catch (Exception ex) { AppLog.Error(ex); }
        }

        // Uninstall removed layouts — need the live LayoutInfo with current HKL handle
        var installed = WindowsLayoutDetector.GetInstalledLayouts();
        var failed = new List<string>();

        foreach (var layout in toUninstall)
        {
            // Match by KLID (low 16 bits of HKL = LANGID from KLID last 4 hex digits)
            int targetLangId = Convert.ToInt32(layout.Klid[4..], 16);
            var liveLayout = installed.FirstOrDefault(l =>
                ((int)(l.Handle.ToInt64() & 0xFFFF)) == targetLangId);

            if (liveLayout is null) continue;

            if (!LayoutManager.Uninstall(liveLayout))
                failed.Add(layout.Name);
        }

        if (failed.Count > 0)
        {
            MessageBox.Show(
                $"Could not remove the following layouts (they may be in use):\n\n" +
                string.Join("\n", failed) +
                "\n\nTry signing out and back in.",
                "LangSwitch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    // ── Display tab ──────────────────────────────────────────────────────────

    TabPage BuildDisplayTab()
    {
        var page = new TabPage("Display");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(6),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        table.Controls.Add(BuildSizeGroup(),     0, 0);
        table.Controls.Add(BuildPositionGroup(), 0, 1);
        table.Controls.Add(new Panel(),          0, 2);

        page.Controls.Add(table);
        return page;
    }

    GroupBox BuildSizeGroup()
    {
        var g = new GroupBox { Text = "Flag size", Dock = DockStyle.Fill };
        _sizeSmall  = Radio("Small  (64 px)",  14, 26);
        _sizeMedium = Radio("Medium (96 px)", 150, 26);
        _sizeLarge  = Radio("Large  (128 px)", 290, 26);
        g.Controls.AddRange([_sizeSmall, _sizeMedium, _sizeLarge]);
        return g;
    }

    GroupBox BuildPositionGroup()
    {
        var g = new GroupBox { Text = "Popup position", Dock = DockStyle.Fill };
        _cornerTL = Radio("↖  Top Left",     14,  26);
        _cornerTR = Radio("↗  Top Right",   230,  26);
        _cornerBL = Radio("↙  Bottom Left",  14,  56);
        _cornerBR = Radio("↘  Bottom Right",230,  56);
        g.Controls.AddRange([_cornerTL, _cornerTR, _cornerBL, _cornerBR]);
        return g;
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
    }

    // ── Commit ───────────────────────────────────────────────────────────────

    void ApplyAll()
    {
        ApplyLanguageChanges();
        SaveDisplayValues();
    }
}
