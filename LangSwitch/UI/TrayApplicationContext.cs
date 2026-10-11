using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using LangSwitch.Autocorrect;
using LangSwitch.Core;
using LangSwitch.Settings;
using static LangSwitch.Core.Localization;

namespace LangSwitch.UI;

sealed class TrayApplicationContext : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly FlagPopupForm _popup;
    readonly LayoutMonitor _monitor;
    readonly KeyboardHook _hook;
    TrayScrollHook? _scrollHook;
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
    ContextMenuStrip _mainMenu = null!;
    ContextMenuStrip? _layoutPickerMenu;

    // NotifyIcon has no public API to show a custom menu on a left click without side
    // effects (a hand-rolled owner window either gets its own taskbar button or ends up
    // positioned off the owner's — potentially offscreen — origin instead of the cursor).
    // Its own private ShowContextMenu() already does this correctly internally (no taskbar
    // artifact, correct activation) for whatever ContextMenuStrip is currently assigned —
    // this reflects into that method so the same correct codepath can be reused for both
    // buttons, swapping which menu is assigned right before invoking it. The method name
    // has been stable since .NET Framework 1.1.
    static readonly MethodInfo? ShowContextMenuMethod =
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    string _lastWord     = string.Empty;
    char   _lastBoundary = '\0';
    PendingUndo? _pendingUndo;

    // The most recent foreground window that belongs to some other app — i.e. whatever the
    // user was last actually working in. Needed because, by the time a tray icon click is
    // processed, the foreground window is often already Explorer's own tray/flyout UI
    // (especially when the icon lives in the "hidden icons" overflow popup, which steals
    // focus the moment it opens — well before the user's click on an icon inside it even
    // registers), so a live GetForegroundWindow() call at click time is too late to see the
    // real target. Kept up to date by polling (_foregroundPoll below) rather than only on
    // keystrokes, so it's also correct for a window the user hasn't typed into yet (e.g. the
    // Win+R Run dialog) — and additionally refreshed on every keystroke for good measure.
    IntPtr _lastExternalWindow;
    readonly System.Windows.Forms.Timer _foregroundPoll;

    readonly record struct PendingUndo(string Original, string Replacement, char Boundary);

    public TrayApplicationContext()
    {
        _settings      = SettingsStore.Load();
        ResolveUiLanguage();
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

        _foregroundPoll = new System.Windows.Forms.Timer { Interval = 250 };
        _foregroundPoll.Tick += (_, _) =>
        {
            var hwnd = LayoutManager.CurrentForegroundWindow;
            if (hwnd != IntPtr.Zero && !LayoutManager.BelongsToCurrentProcess(hwnd))
                _lastExternalWindow = hwnd;
        };
        _foregroundPoll.Start();

        _mainMenu = BuildMenu();
        _tray = new NotifyIcon
        {
            Text = T("Common.AppName"),
            Visible = true,
        };
        _tray.MouseDown += OnTrayClick;

        UpdateScrollHook();

        _monitor.Start();
        RefreshTrayIcon(_monitor.Current);
    }

    /// <summary>Creates or tears down the scroll-to-switch-layout hook to match the current
    /// setting — called at startup and whenever it's toggled in Settings.</summary>
    void UpdateScrollHook()
    {
        if (_scrollHook is not null)
        {
            _scrollHook.Scrolled -= OnTrayScrolled;
            _scrollHook.Dispose();
            _scrollHook = null;
        }
        if (_settings.ScrollToSwitchLayout)
        {
            _scrollHook = new TrayScrollHook(_tray);
            _scrollHook.Scrolled += OnTrayScrolled;
        }
    }

    void OnTrayScrolled(object? sender, int delta)
    {
        var layouts = WindowsLayoutDetector.GetInstalledLayouts();
        if (layouts.Count < 2) return;

        var current = _monitor.Current;
        int index = 0;
        for (int i = 0; i < layouts.Count; i++)
        {
            if (current is not null && layouts[i].Handle == current.Handle) { index = i; break; }
        }

        // Scroll up (positive delta) → previous layout; scroll down → next.
        int next = delta > 0 ? (index - 1 + layouts.Count) % layouts.Count : (index + 1) % layouts.Count;

        var targetHwnd = _lastExternalWindow != IntPtr.Zero ? _lastExternalWindow : LayoutManager.CurrentForegroundWindow;
        LayoutManager.ActivateLayout(layouts[next], targetHwnd);
    }

    void OnKeyDown(object? sender, HookKeyEventArgs e)
    {
        // Keep the "last external window" invariant honest — without this check, typing
        // into our own Settings dialog (the keyboard hook is global and fires regardless of
        // which process has focus) would overwrite it with our own window, so a scroll/click
        // on the tray right after could switch the keyboard layout of the Settings dialog
        // instead of whatever app the user was really working in.
        var fg = LayoutManager.CurrentForegroundWindow;
        if (fg != IntPtr.Zero && !LayoutManager.BelongsToCurrentProcess(fg))
            _lastExternalWindow = fg;

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

    void ResolveUiLanguage()
    {
        if (string.IsNullOrEmpty(_settings.UiLanguage))
        {
            var systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var available = GetAvailableLanguages();
            _settings.UiLanguage = available.Any(l => string.Equals(l.Code, systemLang, StringComparison.OrdinalIgnoreCase))
                ? systemLang
                : "en";
            SettingsStore.Save(_settings);
        }
        Load(_settings.UiLanguage);
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
                Balloon(string.Format(T("Balloon.ErrorPrefix"), ex.Message));
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
        if (string.IsNullOrEmpty(clipped))
        {
            await ClipSetAsync(prevClip);
            return false;
        }

        // Full Trim() (not just \r\n) — some editors' "copy current line, no selection"
        // behavior (e.g. Notepad++) includes a trailing space/newline that a plain
        // Trim('\r','\n') wouldn't strip, breaking the line-copy-vs-real-selection check
        // below. This trimmed form is for *comparison only* — the actual conversion/paste
        // below uses the untrimmed `clipped`, so a genuine selection that legitimately
        // includes a trailing newline (e.g. a full line with its line terminator) doesn't
        // get that newline silently dropped on paste-back.
        var trimmedForComparison = clipped.Trim();

        // Если скопированный текст совпадает с текущим или последним завершённым
        // словом — это line-copy без реального выделения, а не выделение пользователя.
        if (trimmedForComparison == trackerBuf || trimmedForComparison == lastWord)
        {
            await ClipSetAsync(prevClip);
            return false;
        }

        var converted = _candidates.ConvertToOppositeLayout(clipped, _langDicts);
        if (converted == clipped)
        {
            await ClipSetAsync(prevClip);
            Balloon(T("Balloon.CantDetermineLayout"));
            return true;
        }

        if (!await ClipSetAndVerifyAsync(converted))
        {
            await ClipSetAsync(prevClip);
            Balloon(T("Balloon.ClipboardBusy"));
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
            if (!TryConvertWord(buf, out var converted)) return;
            _tracker.Set(converted);
            await ReplaceViaClipboardAsync(buf.Length, converted);
        }
        else if (!string.IsNullOrEmpty(lastWord))
        {
            if (!TryConvertWord(lastWord, out var converted)) return;
            int deleteCount = lastWord.Length + (lastBound != '\0' ? 1 : 0);
            string insert   = converted + (lastBound != '\0' ? lastBound.ToString() : "");
            await ReplaceViaClipboardAsync(deleteCount, insert);
        }
        else
        {
            Balloon(T("Balloon.NoWordToConvert"));
        }
    }

    /// <summary>Converts a word to the opposite layout, surfacing the "can't determine
    /// layout" balloon and returning false if the conversion came back unchanged — shared by
    /// both TryConvertWordAsync branches (in-progress word vs. last completed word), which
    /// otherwise repeat this exact check.</summary>
    bool TryConvertWord(string word, out string converted)
    {
        converted = _candidates.ConvertToOppositeLayout(word, _langDicts);
        if (converted != word) return true;
        Balloon(T("Balloon.CantDetermineLayout"));
        return false;
    }

    async Task ReplaceViaClipboardAsync(int deleteCount, string insert)
    {
        string? prevClip = await ClipGetAsync();

        // Prime the clipboard with the replacement *before* deleting anything, so a clipboard
        // write failure never leaves the word erased with nothing to paste back in its place.
        if (!await ClipSetAndVerifyAsync(insert))
        {
            Balloon(T("Balloon.ClipboardBusy"));
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
                    ? string.Format(T("Balloon.PressToFix"), HotkeyFormat.Combo(_settings.ConvertHotKey))
                    : "";
                Balloon(string.Format(T("Balloon.WrongLayout"), corrected, hotKey));
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
        _tray.ShowBalloonTip(2000, T("Common.AppName"), msg, ToolTipIcon.Info);

    ContextMenuStrip BuildMenu()
    {
        _autocorrectItem = new ToolStripMenuItem(T("Menu.Autocorrect"))
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
        var hotKeyLabel = _settings.ConvertHotKey == Keys.None ? "" : $" ({HotkeyFormat.Combo(_settings.ConvertHotKey)})";
        menu.Items.Add($"{T("Menu.ConvertWord")}{hotKeyLabel}", null, (_, _) => ScheduleConvert());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("Menu.Settings"), null, (_, _) => OpenSettings());
        menu.Items.Add(T("Menu.About"), null, (_, _) => { using var about = new AboutForm(); about.ShowDialog(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("Menu.Exit"), null, (_, _) => Quit());
        menu.Opening += (_, _) => MenuTheme.Apply(menu);
        return menu;
    }

    void OnTrayClick(object? sender, MouseEventArgs e)
    {
        // Deliberately not using NotifyIcon.ContextMenuStrip: Windows' modern tray host
        // (especially the hidden-icons flyout) sometimes delivers the click as a context-menu
        // request regardless of which mouse button was actually pressed, so an assigned
        // ContextMenuStrip can pop up on a left click too — showing "Exit" right under the
        // cursor instead of the layout picker. Instead, the correct menu for the actual
        // button pressed is assigned right before showing it (see ShowContextMenuMethod).
        if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

        // Prefer the polled last-known external window over a live GetForegroundWindow()
        // call — by click time that often already returns Explorer's own tray/flyout UI
        // instead (see _lastExternalWindow). Falls back to a live read for the rare case
        // the app just launched and the poll hasn't ticked yet.
        var targetHwnd = _lastExternalWindow != IntPtr.Zero ? _lastExternalWindow : LayoutManager.CurrentForegroundWindow;

        if (e.Button == MouseButtons.Left)
        {
            _layoutPickerMenu?.Dispose();
            _layoutPickerMenu = BuildLayoutPickerMenu(targetHwnd);
            _tray.ContextMenuStrip = _layoutPickerMenu;
        }
        else
        {
            _tray.ContextMenuStrip = _mainMenu;
        }

        if (ShowContextMenuMethod is not null)
        {
            try { ShowContextMenuMethod.Invoke(_tray, null); return; }
            catch (Exception ex) { AppLog.Error(ex); }
        }
        _tray.ContextMenuStrip?.Show(Cursor.Position); // fallback if reflection ever fails
    }

    ContextMenuStrip BuildLayoutPickerMenu(IntPtr targetHwnd)
    {
        var menu = new ContextMenuStrip();
        var layouts = WindowsLayoutDetector.GetInstalledLayouts();
        if (layouts.Count == 0) return menu;

        var current = _monitor.Current;
        menu.Closed += (_, _) =>
        {
            foreach (ToolStripItem item in menu.Items) item.Image?.Dispose();
        };

        foreach (var layout in layouts)
        {
            using var flag = TrayIconFactory.LoadFlagBitmap(layout.FlagCode);
            var item = new ToolStripMenuItem(layout.DisplayName)
            {
                Image   = TrayIconFactory.Scale(flag, 16, 16),
                Checked = current is not null && layout.Handle == current.Handle,
            };
            item.Click += (_, _) => LayoutManager.ActivateLayout(layout, targetHwnd);
            menu.Items.Add(item);
        }

        MenuTheme.Apply(menu);
        return menu;
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
        _tray.Text = $"{T("Common.AppName")} — {layout.DisplayName}"
            + (_settings.AutocorrectEnabled ? T("Tray.AutocorrectOnSuffix") : "");
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    void OpenSettings()
    {
        var langBefore = _settings.UiLanguage;
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

            if (_settings.UiLanguage != langBefore)
            {
                Load(_settings.UiLanguage);
                var oldMenu = _mainMenu;
                _mainMenu = BuildMenu();
                oldMenu.Dispose();
            }

            RefreshTrayIcon(_monitor.Current);
            _autocorrectItem.Checked = _settings.AutocorrectEnabled;
            UpdateScrollHook();

            DownloadMissingDictionaries();
        }
    }

    /// <summary>
    /// Fires off a background download for any enabled dictionary that has no word list
    /// loaded yet — chiefly the ones SettingsForm.SyncDictionariesToInstalledLayouts just
    /// added for a newly installed keyboard layout, so "install a layout" alone is enough
    /// to get working autocorrect for it, no manual Dictionaries-tab trip required.
    /// </summary>
    void DownloadMissingDictionaries()
    {
        // Must check the *source*, not WordCount == 0: en/ru/uk always have a non-empty
        // built-in fallback word list (~25-31 words), so WordCount alone never reads 0 for
        // them and a real download would never fire for exactly the three flagship
        // languages this is most important for.
        var missing = _dicts.Where(d =>
            d.Enabled && !string.IsNullOrEmpty(d.Lang) && !string.IsNullOrEmpty(d.Url) &&
            _langDicts.SourceOf(d.Lang) is DictionarySource.Builtin or DictionarySource.None).ToList();
        if (missing.Count == 0) return;

        SynchronizationContext.Current!.Post(async _ =>
        {
            foreach (var dict in missing)
            {
                try { await WordListDownloader.DownloadAsync(dict.Lang, dict.Url); }
                catch (Exception ex) { AppLog.Error(ex); }
            }
            _langDicts.Reload(_dicts);
            _engine.ReloadWordLists(_langDicts);
        }, null);
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
        _tray.Visible = false; // hide immediately; full cleanup happens in Dispose(bool)
        Application.Exit();    // Application.Run disposes this ApplicationContext once the loop ends
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _scrollHook?.Dispose();
            _tray.Dispose();
            _mainMenu.Dispose();
            _layoutPickerMenu?.Dispose();
            _currentIcon?.Dispose();
            _monitor.Dispose();
            _popup.Dispose();
            _foregroundPoll.Stop();
            _foregroundPoll.Dispose();
        }
        base.Dispose(disposing);
    }
}
