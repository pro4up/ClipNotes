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
├── build.ps1                    ← мастер-скрипт сборки
├── ClipNotes.sln
├── ARCHITECTURE.md              ← подробная архитектура
├── tools\
│   ├── download-ffmpeg.ps1      ← скачать ffmpeg в app\tools\
│   ├── download-whisper.ps1     ← собрать/скачать whisper-cli в app\tools\
│   └── download-model.ps1       ← скачать GGML-модель в app\models\
└── ClipNotes\                   ← C# проект
    ├── ClipNotes.csproj
    ├── App.xaml / App.xaml.cs
    ├── Models\
    │   ├── AppSettings.cs       ← настройки (JSON)
    │   ├── MarkerEntry.cs       ← маркер (таймкод + текст)
    │   └── SessionData.cs       ← данные текущей сессии
    ├── Services\
    │   ├── ObsWebSocketService.cs    ← OBS WebSocket v5
    │   ├── HotkeyService.cs          ← WinAPI RegisterHotKey
    │   ├── AudioProcessingService.cs ← FFmpeg + whisper-cli
    │   ├── ExcelExportService.cs     ← ClosedXML
    │   └── SettingsService.cs        ← load/save settings.json
    ├── ViewModels\
    │   └── MainViewModel.cs     ← основной ViewModel (CommunityToolkit.Mvvm)
    ├── Views\
    │   ├── MainWindow.xaml/cs
    │   └── SettingsWindow.xaml/cs
    ├── Helpers\
    │   └── PathHelper.cs        ← все runtime-пути приложения
    └── Converters\              ← IValueConverter для XAML
```

## Ключевые файлы для понимания логики

| Файл | Роль |
|------|------|
| `PathHelper.cs` | Все пути к ffmpeg, whisper, models |
| `MainViewModel.cs` | Вся бизнес-логика UI |
| `ObsWebSocketService.cs` | Подключение к OBS, авторизация SHA256 |
| `AudioProcessingService.cs` | FFmpeg + whisper-cli, конкурентное чтение stdout/stderr |
| `AppSettings.cs` | Конфигурация (порт OBS, модель, OutputDirectory) |

## OBS WebSocket (тестовый стенд)

- IP: `172.28.64.1`, Port: `4455`, без пароля
- OBS Studio 32.0.4, WebSocket v5.6.3

## Тестовое видео

- `E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv`
- 126.9 сек, HEVC 1920x1080 60fps + AAC

## Настройки приложения

- Файл: `%AppData%\ClipNotes\settings.json`
- OutputDirectory должен указывать на `E:\Claude Workstation\Projects\ClipNotes\sessions\`
