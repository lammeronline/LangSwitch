# Adding a LangSwitch translation

LangSwitch's interface text lives in plain JSON files, not compiled into the
app. Adding a language is just adding a file — no code changes, no rebuild.

## Where the files live

```
LangSwitch/Locales/en.json   ← source of truth (every key, in English)
LangSwitch/Locales/ru.json
LangSwitch/Locales/uk.json
```

These are copied next to `LangSwitch.exe` at build time, so at runtime the
app reads from a `Locales/` folder beside the executable. That's also where
you can drop a translation **without building anything**: copy a `.json`
file into that folder, restart LangSwitch (or just reopen Settings — the
language list is re-scanned every time), and it shows up in
**Settings → General → Interface → Language**.

If you're contributing the translation back to the project, put the file in
`LangSwitch/Locales/` in the repo instead — it's picked up by the build
automatically (the `.csproj` includes `Locales/*.json` as a wildcard), so no
other file needs to change.

## File format

A flat JSON object: `"key": "translated text"`. One required key,
`_displayName`, is what shows up in the language picker — so a German
translation would start:

```json
{
  "_displayName": "Deutsch",

  "Common.OK": "OK",
  "Common.Cancel": "Abbrechen",
  ...
}
```

## How to make one

1. Copy `LangSwitch/Locales/en.json` to `LangSwitch/Locales/{code}.json`,
   where `{code}` is the language's two-letter code (`de`, `fr`, `pl`, …).
   It doesn't have to match an ISO code exactly — it's just the filename
   LangSwitch shows in the picker via `_displayName` — but using the real
   ISO 639-1 code is the convention the built-in ones follow.
2. Change `_displayName` to the language's own name, as a native speaker
   would write it (e.g. `"Deutsch"`, not `"German"`).
3. Translate each value. Leave the keys (the left-hand side) alone — they're
   just internal IDs, never shown to anyone.
4. Keep `{0}`, `{1}`, etc. exactly where they make sense in your language's
   word order — they get substituted with real values (a count, a word, a
   hotkey) at runtime. Example:
   ```json
   "Dictionaries.DownloadSuccess": "Downloaded {0} words for {1}."
   ```
   `{0}` is a number, `{1}` is a language code — reorder them if your
   grammar needs `{1}` before `{0}`, just keep both present.
5. Keep `\n` where it appears — it's a line break inside a hint/tooltip, not
   literal text to translate.
6. **Partial translations are fine.** You don't have to translate every key
   to submit something useful — English is always loaded underneath the
   active language as a fallback, so any key you skip just shows its English
   text instead of a blank or a raw key name like `Common.OK`.

## A few keys that are deliberately left in English everywhere

The hotkey labels `Ctrl`, `Shift`, `Alt` (built from `Keys` in
`SettingsForm.cs`/`TrayApplicationContext.cs`, not from the locale files)
aren't part of the translation — they mirror what's printed on the physical
keyboard, which doesn't change with UI language.

## Testing your translation

1. Build normally (`dotnet build LangSwitch.sln`) — your new file under
   `LangSwitch/Locales/` is picked up automatically by the wildcard content
   rule, or just drop the finished `.json` into the `Locales/` folder next
   to an existing build's `.exe`.
2. Run LangSwitch, open **Settings → General**, pick your language from the
   **Language** dropdown, click **OK**.
3. Click through every section (General, Appearance, Exclusions,
   Abbreviations, Statistics, Languages, Dictionaries) and check nothing
   looks cut off — some labels sit in fairly tight spaces (e.g. the nav
   sidebar, the hotkey/advanced rows), so a much longer translation than the
   English original may get visually clipped even though the text itself is
   correct.
4. Right-click and left-click the tray icon to check the context menu and
   layout picker too.

## Submitting it

Open a pull request adding `LangSwitch/Locales/{code}.json`. Keep the key
order matching `en.json` if you can — it makes future diffs (when new keys
get added) much easier to review.
