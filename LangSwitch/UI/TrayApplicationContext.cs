using System.Runtime.InteropServices;
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
    readonly ILayoutCandidateProvider _candidates;
    readonly LanguageDictionaries _langDicts;
    List<UserDictionary> _dicts;
    List<Abbreviation> _abbreviations;
    readonly Dictionary<string, int> _undoCounts;
    readonly Stats _stats;
    readonly AutoPunctuator _punctuator = new();
    AppSettings _settings;
    Icon? _currentIcon;
    ToolStripMenuItem _autocorrectItem = null!;
    string _lastWord     = string.Empty;
    char   _lastBoundary = '\0';
    PendingUndo? _pendingUndo;

    readonly record struct PendingUndo(string Original, string Replacement, char Boundary);

    public TrayApplicationContext()
    {
        _settings      = SettingsStore.Load();
        var seedLangs  = WindowsLayoutDetector.GetInstalledLayouts()
            .Select(l => l.Culture.TwoLetterISOLanguageName).Distinct();
        _dicts         = DictionaryStore.Load(seedLangs);
        _abbreviations = AbbreviationStore.Load();
        _undoCounts    = UndoLearningStore.Load();
        _stats         = StatsStore.Load();
        _langDicts     = new LanguageDictionaries(_dicts);
        _candidates    = new LayoutCandidateGenerator(new WinLayoutCharMapSource());
        _engine        = new AutocorrectEngine(_langDicts, new WinTextInjector(), _candidates, _abbreviations,
            _settings.FixCapsLock, NeverCorrectSet());

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
        _tray.MouseClick += OnTrayClick;

        _monitor.Start();
        RefreshTrayIcon(_monitor.Current);
    }

    void OnKeyDown(object? sender, HookKeyEventArgs e)
    {
        // Never buffer, autocorrect, or manually convert text in a password field.
        if (PasswordFieldDetector.IsFocusedFieldPassword())
        {
            _pendingUndo = null;
            _tracker.Clear();
            return;
        }

        if (_settings.ConvertHotKey != Keys.None)
        {
            var baseKey = _settings.ConvertHotKey & Keys.KeyCode;
            var reqMods = _settings.ConvertHotKey & Keys.Modifiers;
            if (e.Key == baseKey && Control.ModifierKeys == reqMods)
            {
                e.Handled = true;
                _pendingUndo = null;
                ScheduleConvert();
                return;
            }
        }

        if (_settings.AutoPunctuation && !IsExcludedApp() && _punctuator.TryHandle(e))
            return;

        if (e.Key == Keys.Back && _pendingUndo is { } undo)
        {
            e.Handled = true;
            _pendingUndo = null;
            int deleteCount = undo.Replacement.Length + (undo.Boundary != '\0' ? 1 : 0);
            string restore   = undo.Original + (undo.Boundary != '\0' ? undo.Boundary.ToString() : "");
            TextInjector.Replace(deleteCount, restore);
            _tracker.Clear();
            RecordUndo(undo.Original);
            return;
        }
        _pendingUndo = null; // any other key ends the undo window

        _tracker.Feed(e);
    }

    HashSet<string> NeverCorrectSet() =>
        new(_undoCounts.Where(kv => kv.Value >= _settings.UndoLearnThreshold).Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

    void RecordUndo(string original)
    {
        var key = original.ToLowerInvariant();
        _undoCounts[key] = _undoCounts.GetValueOrDefault(key) + 1;
        UndoLearningStore.Save(_undoCounts);
        if (_undoCounts[key] >= _settings.UndoLearnThreshold)
            _engine.ReloadNeverCorrect(NeverCorrectSet());
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
                if (await TryConvertSelectionAsync(buf, lastWord)) return;
                await TryConvertWordAsync(buf, lastWord, lastBound);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex);
                Balloon("Error: " + ex.Message);
            }
        }, null);
    }

    // ── Clipboard helpers (retry on CLIPBRD_E_CANT_OPEN) ─────────────────
    [DllImport("user32.dll")]
    static extern uint GetClipboardSequenceNumber();

    static async Task<string?> ClipGetAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch (ExternalException) { await Task.Delay(15); }
        }
        return null;
    }

    static async Task ClipSetAsync(string? text)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (text != null) Clipboard.SetText(text);
                else Clipboard.Clear();
                return;
            }
            catch (ExternalException) { await Task.Delay(15); }
        }
    }

    /// <summary>
    /// Sets the clipboard and confirms the write actually stuck before returning — a plain
    /// ClipSetAsync can silently no-op under clipboard contention (e.g. Windows' clipboard
    /// history service), leaving a stale value in place for the Ctrl+V that follows.
    /// </summary>
    static async Task<bool> ClipSetAndVerifyAsync(string text)
    {
        for (int i = 0; i < 8; i++)
        {
            await ClipSetAsync(text);
            if (await ClipGetAsync() == text) return true;
            await Task.Delay(15);
        }
        return false;
    }

    // ── Selected text path ────────────────────────────────────────────────
    // trackerBuf/lastWord — текущее или последнее завершённое слово трекера;
    // если скопированный текст совпадает с любым из них, значит приложение
    // сделало «copy current line» без выделения (поведение нового
    // Notepad/Notepad++), и надо переключиться на word-путь, а не вставлять
    // результат поверх несуществующего выделения.
    async Task<bool> TryConvertSelectionAsync(string trackerBuf, string lastWord)
    {
        string? prevClip = await ClipGetAsync();
        uint seqBefore = GetClipboardSequenceNumber();

        TextInjector.SendChord(0x11, 0x43); // Ctrl+C
        await Task.Delay(80);

        // The clipboard sequence number only changes when something actually overwrites
        // the clipboard. If it's unchanged, Ctrl+C genuinely did nothing — there is no
        // selection — regardless of whatever happens to already be on the clipboard
        // (avoids misreading stale clipboard content as a fresh copy).
        if (GetClipboardSequenceNumber() == seqBefore)
            return false;

        var clipped = await ClipGetAsync();
        // Full Trim() (not just \r\n) — some editors' "copy current line, no selection"
        // behavior (e.g. Notepad++) includes a trailing space/newline that a plain
        // Trim('\r','\n') wouldn't strip, breaking the line-copy-vs-real-selection check below.
        var text = clipped?.Trim();

        // Если скопированный текст совпадает с текущим или последним завершённым
        // словом — это line-copy без реального выделения, а не выделение пользователя.
        if (string.IsNullOrEmpty(text) || text == trackerBuf || text == lastWord)
        {
            await ClipSetAsync(prevClip);
            return false;
        }

        var converted = _candidates.ConvertToOppositeLayout(text);
        if (converted == text)
        {
            await ClipSetAsync(prevClip);
            Balloon("Can't determine target layout");
            return true;
        }

        if (!await ClipSetAndVerifyAsync(converted))
        {
            await ClipSetAsync(prevClip);
            Balloon("Clipboard busy — try again");
            return true;
        }
        await Task.Delay(_settings.ClipboardDelayMs);
        TextInjector.SendChord(0x11, 0x56); // Ctrl+V

        await Task.Delay(50);
        await ClipSetAsync(prevClip);

        _tracker.Clear();
        _stats.ManualConversions++;
        return true;
    }

    // ── Current-word / last-word path ─────────────────────────────────────
    async Task TryConvertWordAsync(string buf, string lastWord, char lastBound)
    {
        if (!string.IsNullOrEmpty(buf))
        {
            var converted = _candidates.ConvertToOppositeLayout(buf);
            if (converted == buf) { Balloon("Can't determine target layout"); return; }

            _tracker.Set(converted);
            await ReplaceViaClipboardAsync(buf.Length, converted);
        }
        else if (!string.IsNullOrEmpty(lastWord))
        {
            var converted = _candidates.ConvertToOppositeLayout(lastWord);
            if (converted == lastWord) { Balloon("Can't determine target layout"); return; }

            int deleteCount = lastWord.Length + (lastBound != '\0' ? 1 : 0);
            string insert   = converted + (lastBound != '\0' ? lastBound.ToString() : "");
            await ReplaceViaClipboardAsync(deleteCount, insert);
        }
        else
        {
            Balloon("No word to convert — type a word first");
        }
    }

    async Task ReplaceViaClipboardAsync(int deleteCount, string insert)
    {
        string? prevClip = await ClipGetAsync();

        // Prime the clipboard with the replacement *before* deleting anything, so a clipboard
        // write failure never leaves the word erased with nothing to paste back in its place.
        if (!await ClipSetAndVerifyAsync(insert))
        {
            Balloon("Clipboard busy — try again");
            return;
        }

        TextInjector.SendBackspaces(deleteCount);
        await Task.Delay(30);

        await Task.Delay(_settings.ClipboardDelayMs);
        TextInjector.SendChord(0x11, 0x56); // Ctrl+V
        await Task.Delay(50);

        await ClipSetAsync(prevClip);
        _stats.ManualConversions++;
    }

    void OnWordReady(object? sender, WordReadyEventArgs e)
    {
        _lastWord     = e.Word;
        _lastBoundary = e.Boundary;

        if (IsExcludedApp()) return;

        if (_settings.AutocorrectEnabled && _engine.TryReplace(e, out var replacement))
        {
            _pendingUndo = new PendingUndo(e.Word, replacement!, e.Boundary);
            _lastWord = string.Empty;
            _stats.WordsCorrected++;
            _stats.CharactersFixed += e.Word.Length;
            return;
        }

        if (_settings.NotifyWrongLayout)
        {
            var corrected = LayoutAutoCorrector.TryCorrect(e.Word, _candidates.GetAllCandidates(e.Word), _langDicts);
            if (corrected is not null)
            {
                var hotKey = _settings.ConvertHotKey != Keys.None
                    ? $" (press {HotKeyLabel(_settings.ConvertHotKey)} to fix)"
                    : "";
                Balloon($"Wrong layout? → {corrected}{hotKey}");
            }
        }
    }

    bool IsExcludedApp()
    {
        if (_settings.ExcludedApps.Count == 0) return false;
        var name = WindowsLayoutDetector.GetForegroundProcessName();
        if (name is null) return false;
        return _settings.ExcludedApps.Any(x =>
            string.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase));
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
            RefreshTrayIcon(_monitor.Current);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_autocorrectItem);
        var hotKeyLabel = _settings.ConvertHotKey == Keys.None ? "" : $" ({HotKeyLabel(_settings.ConvertHotKey)})";
        menu.Items.Add($"Convert word{hotKeyLabel}", null, (_, _) => ScheduleConvert());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());
        menu.Opening += (_, _) => MenuTheme.Apply(menu);
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

    void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ShowLayoutPicker();
    }

    void ShowLayoutPicker()
    {
        var layouts = WindowsLayoutDetector.GetInstalledLayouts();
        if (layouts.Count == 0) return;

        var current = _monitor.Current;
        var menu = new ContextMenuStrip();
        menu.Closed += (_, _) =>
        {
            foreach (ToolStripItem item in menu.Items) item.Image?.Dispose();
            menu.Dispose();
        };

        foreach (var layout in layouts)
        {
            using var flag = TrayIconFactory.LoadFlagBitmap(layout.FlagCode);
            var item = new ToolStripMenuItem(layout.DisplayName)
            {
                Image   = TrayIconFactory.Scale(flag, 16, 16),
                Checked = current is not null && layout.Handle == current.Handle,
            };
            item.Click += (_, _) => LayoutManager.ActivateLayout(layout);
            menu.Items.Add(item);
        }

        MenuTheme.Apply(menu);
        menu.Show(Cursor.Position);
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

        var icon = TrayIconFactory.Create(layout, _settings.AutocorrectEnabled);
        _tray.Icon = icon;
        _tray.Text = $"LangSwitch — {layout.DisplayName}"
            + (_settings.AutocorrectEnabled ? " (autocorrect on)" : "");
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _dicts, _langDicts, _abbreviations, _stats, _undoCounts, ResetUndoLearning);
        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            DictionaryStore.Save(_dicts);
            AbbreviationStore.Save(_abbreviations);
            StatsStore.Save(_stats);
            _langDicts.Reload(_dicts);
            _engine.ReloadWordLists(_langDicts);
            _engine.ReloadAbbreviations(_abbreviations);
            _engine.SetFixCapsLock(_settings.FixCapsLock);
            _engine.ReloadNeverCorrect(NeverCorrectSet());
            _popup.ApplySettings(_settings);
            RefreshTrayIcon(_monitor.Current);
            _autocorrectItem.Checked = _settings.AutocorrectEnabled;
        }
    }

    void ResetUndoLearning()
    {
        _undoCounts.Clear();
        UndoLearningStore.Save(_undoCounts);
        _engine.ReloadNeverCorrect([]);
    }

    void Quit()
    {
        StatsStore.Save(_stats);
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
