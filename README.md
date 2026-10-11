# LangSwitch

A lightweight Windows tray utility that fixes text typed in the wrong keyboard
layout (EN ⇄ RU ⇄ UK) — a PuntoSwitcher-style layout autocorrector for .NET 8.

## Features

- **Automatic layout autocorrect** — detects a word typed in the wrong
  keyboard layout (e.g. `ghbdtn` instead of `привет`) and silently fixes it
  as soon as you finish typing it.
- **Undo with Backspace** — if a correction was wrong, pressing Backspace
  right after it reverts to what you actually typed.
- **Manual conversion hotkey** — force-convert the selected text or the
  current/last word to the opposite layout on demand.
- **Caps Lock fix** — corrects the classic "Caps Lock was on by accident"
  pattern (`hELLO` → `Hello`).
- **Abbreviation expansion** — define short triggers that expand into longer
  text as you type (e.g. `btw` → `by the way`).
- **Per-application exclusions** — autocorrect stays out of the way in
  terminals, IDEs, and any other app you list (pre-configured for common
  ones out of the box).
- **Tray flag indicator** — shows a popup with the active layout's flag when
  it changes; left-click the tray icon for a layout picker, right-click for
  settings.
- **Keyboard layout management** — install/remove Windows keyboard layouts
  directly from the Settings window.
- **Configurable dictionaries** — per-language word lists (download
  frequency word lists, or add your own) used to decide whether a word looks
  "right" in a given language.

## Download

Grab the latest self-contained `LangSwitch.exe` from the
[Releases](../../releases) page — no .NET runtime install required, just run
it.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Debug build
dotnet build LangSwitch.sln

# Run the unit tests
dotnet test LangSwitch.Tests/LangSwitch.Tests.csproj

# Publish a single-file, self-contained release build
dotnet publish LangSwitch/LangSwitch.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o build/out
```

(`build/build.bat` wraps the publish command above.)

## Project structure

```
LangSwitch.sln
  LangSwitch/         WinForms app — tray UI, settings, Win32 interop (hook,
                       layout switching, text injection)
  LangSwitch.Core/     Pure logic, no Win32/WinForms dependency — layout
                       conversion tables, dictionaries, autocorrect decision
                       logic, abbreviation/caps-lock correctors, JSON stores
  LangSwitch.Tests/    xUnit tests for LangSwitch.Core and the autocorrect
                       orchestration
```

Settings, dictionaries, abbreviations, and word lists are stored as JSON/text
files next to the executable at runtime (`settings.json`,
`dictionaries.json`, `abbreviations.json`, `WordLists/`), not in the
repository.

## Contributing a translation

The UI is localized via plain JSON files under `LangSwitch/Locales/` — no
code changes needed to add a language. See [LOCALIZATION.md](LOCALIZATION.md).

## License

[MIT](LICENSE)
