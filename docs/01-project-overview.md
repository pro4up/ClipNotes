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
└── sessions\        ← выходные данные сессий (OutputDirectory в настройках)
```

## Структура source\

```
source\
├── build.ps1                    ← мастер-скрипт сборки (-SkipDependencies, -SkipModel, -Model, -Backend)
├── ClipNotes.sln
├── README.md                    ← пользовательская документация
├── ARCHITECTURE.md              ← подробная архитектура
├── tools\
│   ├── download-ffmpeg.ps1      ← скачать ffmpeg в app\tools\
│   ├── download-whisper.ps1     ← собрать/скачать whisper-cli в app\tools\ (-Backend cpu|cuda)
│   └── download-model.ps1       ← скачать GGML-модель в app\models\
├── installer\
│   └── ClipNotes.iss            ← Inno Setup скрипт для создания установщика
├── resource\                    ← исходные PNG для иконок
└── ClipNotes\                   ← C# проект
    ├── ClipNotes.csproj
    ├── App.xaml / App.xaml.cs   ← глобальные стили, ApplyTheme(), DWM, трей
    ├── GlobalUsings.cs
    ├── AssemblyInfo.cs
    ├── Models\
    │   ├── AppSettings.cs       ← настройки приложения (JSON), SessionHistoryEntry, HotkeyBindingData
    │   ├── MarkerType.cs        ← enum: Bug, Task, Note
    │   ├── Marker.cs            ← маркер (таймкод + текст + флаги генерации)
    │   ├── SessionData.cs       ← данные текущей сессии
    │   ├── HotkeyAction.cs      ← enum действий горячих клавиш
    │   └── HotkeyBinding.cs     ← модель привязки клавиши в UI
    ├── Services\
    │   ├── ObsWebSocketService.cs    ← OBS WebSocket v5 (SHA256 auth)
    │   ├── FFmpegService.cs          ← FFmpeg: длительность, master.wav, нарезка клипов
    │   ├── WhisperService.cs         ← whisper-cli: транскрипция аудиоклипа
    │   ├── ExcelService.cs           ← ClosedXML: генерация .xlsx
    │   ├── HotkeyService.cs          ← WinAPI RegisterHotKey
    │   ├── SessionService.cs         ← управление папками сессии, перемещение видео
    │   ├── PipelineService.cs        ← оркестратор постобработки (7 шагов)
    │   ├── SettingsService.cs        ← load/save settings.json
    │   └── LogService.cs             ← логирование в AppDir/Logs/YYYY-MM-DD.log
    ├── ViewModels\
    │   └── MainViewModel.cs     ← единственный ViewModel (CommunityToolkit.Mvvm)
    ├── Views\
    │   ├── MainWindow.xaml      ← весь UI: 4 вкладки (Запись|Экспорт|История|Настройки)
    │   └── MainWindow.xaml.cs   ← code-behind (DWM, трей, HwndSource)
    ├── Helpers\
    │   ├── PathHelper.cs        ← все runtime-пути приложения
    │   └── RelayCommand.cs      ← ICommand реализация
    └── Converters\              ← IValueConverter для XAML
        ├── BoolToVisibilityConverter.cs
        └── MarkerTypeToBrushConverter.cs
```

## Ключевые файлы для понимания логики

| Файл | Роль |
|------|------|
| `PathHelper.cs` | Все пути к ffmpeg, whisper, models (через Assembly.Location) |
| `LogService.cs` | Логирование (через Process.MainModule?.FileName, а не Assembly.Location) |
| `MainViewModel.cs` | Вся бизнес-логика UI, команды, состояние |
| `ObsWebSocketService.cs` | Подключение к OBS, авторизация SHA256 |
| `FFmpegService.cs` | FFmpeg операции (длительность, master.wav, клипы) |
| `WhisperService.cs` | whisper-cli транскрипция (принимает готовый аудиоклип) |
| `PipelineService.cs` | Оркестратор: 7 шагов постобработки |
| `AppSettings.cs` | Конфигурация (OBS, модель, OutputDirectory, тема, трей, история) |

## OBS WebSocket (тестовый стенд)

- IP: `172.28.64.1`, Port: `4455`, без пароля
- OBS Studio 32.0.4, WebSocket v5.6.3

## Тестовое видео

- `E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv`
- 126.9 сек, HEVC 1920x1080 60fps + AAC

## Настройки приложения

- Файл: `%AppData%\ClipNotes\settings.json`
- OutputDirectory должен указывать на `E:\Claude Workstation\Projects\ClipNotes\sessions\`
