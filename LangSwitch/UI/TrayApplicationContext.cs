using LangSwitch.Autocorrect;
using LangSwitch.Core;
using LangSwitch.Settings;

namespace LangSwitch.UI;

sealed class TrayApplicationContext : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly FlagPopupForm _popup;
    readonly LayoutMonitor _monitor;
    readonly KeyboardHook _hook;
    readonly WordTracker _tracker;
    readonly AutocorrectEngine _engine;
    readonly LanguageDictionaries _langDicts;
    List<UserDictionary> _dicts;
    AppSettings _settings;
    Icon? _currentIcon;
    ToolStripMenuItem _autocorrectItem = null!;
    string _lastWord     = string.Empty;
    char   _lastBoundary = '\0';

    public TrayApplicationContext()
    {
        _settings  = SettingsStore.Load();
        _dicts     = DictionaryStore.Load();
        _langDicts = new LanguageDictionaries();
        _engine    = new AutocorrectEngine(_langDicts);

        _popup   = new FlagPopupForm(_settings);
        _monitor = new LayoutMonitor();
        _monitor.LayoutChanged += OnLayoutChanged;

        _hook    = new KeyboardHook();
        _tracker = new WordTracker();
        _hook.KeyDown      += OnKeyDown;
        _tracker.WordReady += OnWordReady;

        _tray = new NotifyIcon
        {
            Text = "LangSwitch",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _monitor.Start();
        RefreshTrayIcon(_monitor.Current);
    }

    void OnKeyDown(object? sender, HookKeyEventArgs e)
    {
        if (_settings.ConvertHotKey != Keys.None)
        {
            var baseKey = _settings.ConvertHotKey & Keys.KeyCode;
            var reqMods = _settings.ConvertHotKey & Keys.Modifiers;
            if (e.Key == baseKey && Control.ModifierKeys == reqMods)
            {
                e.Handled = true;
                ScheduleConvert();
                return;
            }
        }
        _tracker.Feed(e);
    }

    void ScheduleConvert()
    {
        // Snapshot state now (inside hook callback); clear lastWord to prevent double-trigger
        var buf       = _tracker.Current;
        var lastWord  = _lastWord;
        var lastBound = _lastBoundary;
        _lastWord = string.Empty;

        SynchronizationContext.Current!.Post(async _ =>
        {
            try
            {
                if (await TryConvertSelectionAsync()) return;
                TryConvertWord(buf, lastWord, lastBound);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
                Balloon("Error: " + ex.Message);
            }
        }, null);
    }

    // ── Selected text path ────────────────────────────────────────────────
    async Task<bool> TryConvertSelectionAsync()
    {
        string? prevClip = Clipboard.ContainsText() ? Clipboard.GetText() : null;

        TextInjector.SendChord(0x11, 0x43); // Ctrl+C
        await Task.Delay(80);

        if (!Clipboard.ContainsText()) return false;
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text) || text == prevClip) return false;

        var converted = LayoutConverter.ConvertToOppositeLayout(text);
        if (converted == text)
        {
            // restore clipboard and bail
            if (prevClip != null) Clipboard.SetText(prevClip); else Clipboard.Clear();
            Balloon("Can't determine target layout");
            return true;
        }

        Clipboard.SetText(converted);
        await Task.Delay(30);
        TextInjector.SendChord(0x11, 0x56); // Ctrl+V

        _tracker.Clear(); // cursor moved; buffer is stale
        return true;
    }

    // ── Current-word / last-word path ─────────────────────────────────────
    void TryConvertWord(string buf, string lastWord, char lastBound)
    {
        if (!string.IsNullOrEmpty(buf))
        {
            var converted = LayoutConverter.ConvertToOppositeLayout(buf);
            if (converted == buf) { Balloon("Can't determine target layout"); return; }

            _tracker.Set(converted);
            TextInjector.Replace(buf.Length, converted);
        }
        else if (!string.IsNullOrEmpty(lastWord))
        {
            var converted = LayoutConverter.ConvertToOppositeLayout(lastWord);
            if (converted == lastWord) { Balloon("Can't determine target layout"); return; }

            int deleteCount = lastWord.Length + (lastBound != '\0' ? 1 : 0);
            string insert   = converted + (lastBound != '\0' ? lastBound.ToString() : "");
            TextInjector.Replace(deleteCount, insert);
        }
        else
        {
            Balloon("No word to convert — type a word first");
        }
    }

    void OnWordReady(object? sender, WordReadyEventArgs e)
    {
        _lastWord     = e.Word;
        _lastBoundary = e.Boundary;

        if (_settings.AutocorrectEnabled && _engine.TryReplace(e))
            _lastWord = string.Empty;
    }

    void Balloon(string msg) =>
        _tray.ShowBalloonTip(2000, "LangSwitch", msg, ToolTipIcon.Info);

    ContextMenuStrip BuildMenu()
    {
        _autocorrectItem = new ToolStripMenuItem("Autocorrect")
        {
            CheckOnClick = true,
            Checked      = _settings.AutocorrectEnabled,
        };
        _autocorrectItem.CheckedChanged += (_, _) =>
        {
            _settings.AutocorrectEnabled = _autocorrectItem.Checked;
            SettingsStore.Save(_settings);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_autocorrectItem);
        var hotKeyLabel = _settings.ConvertHotKey == Keys.None ? "" : $" ({HotKeyLabel(_settings.ConvertHotKey)})";
        menu.Items.Add($"Convert word{hotKeyLabel}", null, (_, _) => ScheduleConvert());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());
        return menu;
    }

    static string HotKeyLabel(Keys k)
    {
        if (k == Keys.None) return "";
        var parts = new List<string>();
        if ((k & Keys.Control) != 0) parts.Add("Ctrl");
        if ((k & Keys.Shift)   != 0) parts.Add("Shift");
        if ((k & Keys.Alt)     != 0) parts.Add("Alt");
        var baseKey = k & Keys.KeyCode;
        if (baseKey != Keys.None) parts.Add(baseKey.ToString());
        return string.Join("+", parts);
    }

    void OnLayoutChanged(object? sender, LayoutChangedEventArgs e)
    {
        RefreshTrayIcon(e.Current);
        _popup.ShowLayout(e.Current);
    }

    void RefreshTrayIcon(LayoutInfo? layout)
    {
        layout ??= WindowsLayoutDetector.GetForegroundLayout();
        if (layout is null) return;

        var icon = TrayIconFactory.Create(layout);
        _tray.Icon = icon;
        _tray.Text = $"LangSwitch — {layout.DisplayName}";
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _dicts, _langDicts);
        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            DictionaryStore.Save(_dicts);
            _langDicts.Reload();
            _engine.ReloadWordLists(_langDicts);
            _popup.ApplySettings(_settings);
            RefreshTrayIcon(_monitor.Current);
        }
    }

    void Quit()
    {
        _tray.Visible = false;
        _monitor.Dispose();
        _popup.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _tray.Dispose();
            _currentIcon?.Dispose();
            _monitor.Dispose();
            _popup.Dispose();
        }
        base.Dispose(disposing);
    }
}
