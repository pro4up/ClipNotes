# 01 — Обзор проекта

## Назначение

**ClipNotes** — десктопное Windows-приложение (C# .NET 8 WPF). Захватывает таймкодированные заметки во время записи OBS Studio. По окончании записи:
- Нарезает аудио-сниппеты (FFmpeg)
- Транскрибирует (whisper-cli)
- Генерирует Excel-таблицу (ClosedXML)

## Структура на диске

```
E:\Claude Workstation\Projects\ClipNotes\
├── source\          ← git-репозиторий, исходный код
├── app\             ← собранное приложение (dotnet publish)
├── Setup\           ← готовые установочники
└── sessions\        ← выходные данные сессий (OutputDirectory в настройках)
```

## Структура source\

```
source\
├── build.ps1                    ← мастер-скрипт сборки
├── rebuild-installers.ps1       ← быстрая пересборка установочников
├── ClipNotes.sln
├── README.md                    ← пользовательская документация
├── ARCHITECTURE.md              ← подробная архитектура
├── docs\                        ← внутренняя документация (для AI-агентов)
├── tools\
│   ├── download-ffmpeg.ps1
│   ├── download-whisper.ps1     ← -Backend cpu|cuda
│   └── download-model.ps1
├── resource\                    ← исходные PNG для иконок
├── ClipNotes\                   ← основное приложение
│   ├── ClipNotes.csproj
│   ├── App.xaml / App.xaml.cs   ← глобальные стили, ApplyTheme(), DWM, трей
│   ├── Models\
│   │   ├── AppSettings.cs       ← все настройки + SessionHistoryEntry
│   │   ├── MarkerType.cs        ← enum: Bug, Task, Note, Summary
│   │   ├── Marker.cs            ← таймкод + HoldDuration + флаги генерации
│   │   ├── SessionData.cs       ← данные текущей сессии
│   │   ├── HotkeyAction.cs      ← enum действий
│   │   └── HotkeyBinding.cs     ← модель привязки клавиши в UI
│   ├── Services\
│   │   ├── ObsWebSocketService.cs    ← OBS WebSocket v5 (SHA256 auth)
│   │   ├── FFmpegService.cs          ← длительность, master.wav, нарезка
│   │   ├── WhisperService.cs         ← whisper-cli транскрипция
│   │   ├── ExcelService.cs           ← ClosedXML Excel
│   │   ├── HotkeyService.cs          ← WinAPI RegisterHotKey + SetWindowsHookEx (Hold Mode)
│   │   ├── SessionService.cs         ← папки сессии, маркеры JSON, MoveVideo
│   │   ├── PipelineService.cs        ← оркестратор постобработки
│   │   ├── SettingsService.cs        ← settings.json load/save
│   │   └── LogService.cs             ← AppDir/Logs/YYYY-MM-DD.log
│   ├── ViewModels\
│   │   └── MainViewModel.cs     ← единственный ViewModel (CommunityToolkit.Mvvm)
│   ├── Views\
│   │   ├── MainWindow.xaml      ← 4 вкладки: Запись|Экспорт|История|Настройки
│   │   └── MainWindow.xaml.cs   ← анимация Hold, скролл настроек, hotkey TextBox
│   ├── Helpers\
│   │   ├── PathHelper.cs        ← runtime-пути приложения
│   │   └── InputDialog.cs       ← простой WPF диалог ввода текста
│   └── Converters\
│       ├── BoolToVisibilityConverter.cs
│       └── MarkerTypeToBrushConverter.cs
├── ClipNotes.Setup\             ← WPF-установщик
│   ├── Pages\                   ← Welcome/Options/Backend/Model/Summary/Progress/Finish
│   ├── Services\InstallerService.cs
│   ├── Loc.cs                   ← локализация установщика
│   └── lang\en\ + lang\ru\
└── ClipNotes.Uninstaller\       ← деинсталлятор
    ├── Loc.cs
    └── lang\en\ + lang\ru\
```

## Ключевые файлы для понимания логики

| Файл | Роль |
|------|------|
| `PathHelper.cs` | Все пути к ffmpeg, whisper, models, lang |
| `LogService.cs` | Логирование (через Process.MainModule?.FileName, не Assembly.Location) |
| `MainViewModel.cs` | Вся бизнес-логика UI, команды, состояние |
| `ObsWebSocketService.cs` | Подключение к OBS, авторизация SHA256 |
| `FFmpegService.cs` | FFmpeg операции (длительность, master.wav, клипы) |
| `WhisperService.cs` | whisper-cli транскрипция (принимает готовый аудиоклип) |
| `PipelineService.cs` | Оркестратор: шаги постобработки |
| `AppSettings.cs` | Все настройки (OBS, модель, пути, тема, трей, история, Hold Mode) |

## OBS WebSocket (тестовый стенд)

- IP: `172.28.64.1`, Port: `4455`, без пароля
- OBS Studio 32.0.4, WebSocket v5.6.3

## Тестовое видео

- `E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv`
- 126.9 сек, HEVC 1920x1080 60fps + AAC

## Настройки приложения

- Файл: `%AppData%\ClipNotes\settings.json`
- OutputDirectory должен указывать на `E:\Claude Workstation\Projects\ClipNotes\sessions\`
