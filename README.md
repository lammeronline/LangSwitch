# LangSwitch

A lightweight Windows tray utility that fixes text typed in the wrong keyboard
layout — a PuntoSwitcher-style layout autocorrector for .NET 8. Works with
any keyboard layout installed on your system, not just a fixed set of
languages.

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
  directly from the Settings window; a dictionary is added or removed to
  match automatically, no fixed language list anywhere.
- **Hunspell-backed dictionaries** — word knowledge comes from Hunspell
  stem+affix dictionaries where available (recognizes inflected forms, not
  just a flat word list), falling back to a frequency word list for
  languages Hunspell doesn't cover. English, Russian and Ukrainian ship
  bundled for offline use out of the box; every other language downloads
  automatically the moment its keyboard layout is installed. A dictionary
  can also be imported manually from a local file with no internet
  connection at all.

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
                       layout switching, text injection), bundled offline
                       dictionaries (WordLists/)
  LangSwitch.Core/     Pure logic, no Win32/WinForms dependency — layout
                       conversion, Hunspell/frequency-list dictionaries,
                       autocorrect decision logic, abbreviation/caps-lock
                       correctors, JSON stores
  LangSwitch.Tests/    xUnit tests for LangSwitch.Core and the autocorrect
                       orchestration
```

Settings, dictionaries, and abbreviations are stored as JSON files next to
the executable at runtime (`settings.json`, `dictionaries.json`,
`abbreviations.json`) and are not part of the repository. The en/ru/uk
dictionary files under `LangSwitch/WordLists/` are the exception — those
ship in the repo and get copied next to the executable at build/publish
time, so the app works offline out of the box; any other language's
dictionary is downloaded (or imported manually) at runtime into the same
`WordLists/` folder next to the executable.

## Contributing a translation

The UI is localized via plain JSON files under `LangSwitch/Locales/` — no
code changes needed to add a language. See [LOCALIZATION.md](LOCALIZATION.md).

## License

[MIT](LICENSE)
