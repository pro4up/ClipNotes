# Contributing to ClipNotes

Thank you for your interest in contributing! This document covers the project structure, build process, and development guidelines.

---

## Requirements

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell 7+
- (Optional) Visual Studio 2022 or JetBrains Rider

---

## Project Structure

```
source/
├── ClipNotes/                  # Main WPF application
│   ├── Models/                 # Data models (AppSettings, Marker, Session, etc.)
│   ├── ViewModels/             # MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                  # XAML windows and dialogs
│   ├── Services/               # Business logic services
│   │   ├── OBSService.cs       # OBS WebSocket v5 client
│   │   ├── PipelineService.cs  # Audio extraction + transcription pipeline
│   │   ├── ExcelService.cs     # Excel report generation (ClosedXML)
│   │   ├── HotkeyService.cs    # Global hotkeys via WinAPI RegisterHotKey
│   │   └── ...
│   ├── Helpers/                # PathHelper, LocHelper, converters
│   └── Resources/              # Icons, themes, styles
├── ClipNotes.Setup/            # WPF installer (7 pages)
├── ClipNotes.Uninstaller/      # WPF uninstaller
├── tools/                      # PowerShell dependency download scripts
│   ├── download-ffmpeg.ps1
│   ├── download-whisper.ps1    # -Backend cpu|cuda
│   └── download-model.ps1
├── build.ps1                   # Master build script
├── rebuild-installers.ps1      # Rebuild installers only (skips app build)
├── README.md
├── ARCHITECTURE.md
└── docs/
    └── CONTRIBUTING.md         # This file
```

---

## Building

### Quick build (app only)

```powershell
cd source
.\build.ps1 -SkipDependencies -SkipModel
```

### Full build (first time — downloads all tools)

```powershell
cd source
.\build.ps1
```

Output: `../app/ClipNotes.exe`

### Build installers

```powershell
# Online setup + portable ZIP
.\rebuild-installers.ps1

# + Offline setup (downloads all whisper models, ~7 GB)
.\rebuild-installers.ps1 -Offline
```

Output: `../Setup/`

---

## Architecture Overview

See [ARCHITECTURE.md](../ARCHITECTURE.md) for full details. Key points:

- **MVVM** via `CommunityToolkit.Mvvm` — `[ObservableProperty]`, `[RelayCommand]`
- **OBS WebSocket v5** — manual JSON implementation, SHA-256 auth
- **Pipeline** — FFmpeg extracts master WAV → parallel clip cutting → sequential whisper transcription → ClosedXML Excel
- **Hotkeys** — `RegisterHotKey` (WinAPI) via `HwndSource`; Hold Mode uses `SetWindowsHookEx` keyboard hook
- **Localization** — JSON files in `lang/{code}/lang.json`, loaded into `Application.Current.Resources`
- **Single-file publish** — use `Process.GetCurrentProcess().MainModule?.FileName` for app path, not `Assembly.Location`

---

## Code Style

- Standard C# naming conventions
- `async`/`await` throughout; never block the UI thread
- Read stdout and stderr **concurrently** when running FFmpeg/whisper (use `Task.WhenAll`) — sequential reading causes deadlocks
- Always use `CultureInfo.InvariantCulture` when formatting numeric values for FFmpeg arguments
- No magic strings — use localization keys (`loc_*`) for all user-visible text

---

## Localization

Language files live in `lang/{code}/lang.json` next to `ClipNotes.exe`. To add a new language:

1. Copy `lang/en/lang.json` to `lang/{your-code}/lang.json`
2. Translate values (keep all keys unchanged)
3. Restart ClipNotes — new language appears in Settings → Language

---

## Testing Checklist

Before submitting a PR, manually verify:

- [ ] OBS connection (connect / disconnect / reconnect)
- [ ] Start / stop recording from app
- [ ] Add markers during recording (all 4 types: Bug, Task, Note, Summary)
- [ ] Hold Mode — hold key/button, verify clip duration matches hold time
- [ ] Manual mode — load video, add markers manually, generate
- [ ] Export pipeline — audio clips + transcription + Excel report
- [ ] Import markers from JSON
- [ ] Settings save/load (restart app, verify persistence)
- [ ] Hotkey configuration — change and apply
- [ ] Custom output paths (video/audio/txt/table) — move and copy modes
- [ ] Session history — open folder, clear history
- [ ] Theme switching (light/dark) and language switching
- [ ] Startup with Windows / minimize to tray

---

## Submitting Changes

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes, test thoroughly
4. Submit a Pull Request with a clear description of what changed and why
