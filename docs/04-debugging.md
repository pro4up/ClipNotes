# 04 — Отладка и поиск багов

## Методология

1. **Воспроизвести** — чёткие шаги для воспроизведения
2. **Локализовать** — какой слой (UI / ViewModel / Service / FFmpeg / Whisper / OBS)
3. **Прочитать код** — только нужный файл, не весь проект
4. **Исправить минимально** — не рефакторить попутно
5. **Проверить** — повторить шаги воспроизведения (сборка + ручной тест)
6. **Коммитить** — только по запросу пользователя

---

## Карта слоёв → файлы

| Слой | Файл | Типичные баги |
|------|------|---------------|
| OBS подключение | `ObsWebSocketService.cs` | SHA256 auth, WebSocket handshake, таймаут запроса |
| Горячие клавиши | `HotkeyService.cs` | Конфликт регистрации, утечка `RegisterHotKey`, `HwndSource` не инициализирован |
| Hold Mode | `HotkeyService.cs` | `SetWindowsHookEx` WH_KEYBOARD_LL, KeyDown/KeyUp события |
| FFmpeg операции | `FFmpegService.cs` | Deadlock stdout/stderr, locale (запятая вместо точки), exit code |
| Whisper транскрипция | `WhisperService.cs` | Deadlock stdout/stderr, путь к клипу, путь к модели |
| Постобработка | `PipelineService.cs` | Порядок шагов, отмена, параллельная нарезка |
| Экспорт Excel | `ExcelService.cs` | Кодировка, формат таймкода, относительные пути |
| Управление сессией | `SessionService.cs` | Ожидание освобождения видеофайла OBS (до 60 сек), маркеры JSON |
| Пути к файлам | `PathHelper.cs` | Неверный AppDir — нужен `Process.MainModule?.FileName` |
| Кастомные пути | `MainViewModel.cs` → `CopyToCustomPaths` | Move/Copy режим, исключение master.wav |
| UI биндинги | `MainViewModel.cs` | ObservableProperty, Commands, состояние IsProcessing |
| Локализация | `LocalizationService.cs` + `lang/{code}/lang.json` | Отсутствующие ключи, fallback на en |
| Настройки | `SettingsService.cs` | JSON десериализация, путь к settings.json |
| Логи | `LogService.cs` | Путь через `Process.MainModule?.FileName` (не Assembly.Location!) |

---

## Известные решённые баги (не повторять)

| Баг | Решение |
|-----|---------|
| Deadlock FFmpeg/Whisper | stdout + stderr читать через `Task.WhenAll`, не последовательно |
| Запятая в FFmpeg аргументах (RU Windows) | `value.ToString("F3", CultureInfo.InvariantCulture)` |
| `Assembly.Location` пустой в single-file | Использовать `Process.GetCurrentProcess().MainModule?.FileName` |
| `Icon="/Resources/icon.ico"` не работает | `pack://application:,,,/Resources/icon.ico` + `<Resource>` в .csproj |
| Двойной вызов `GenerateAsync` | Guard `if (IsProcessing) return;` в начале метода |
| `TimeSpan.TryParse` принимает "99" как 99 дней | Использовать только `TryParseExact` с явными форматами |

---

## Паттерн: баг в постобработке

При баге в FFmpeg/Whisper pipeline:
1. Проверить exit code процесса (логи)
2. Читать stderr — там обычно ошибка
3. Воспроизвести команду вручную в PowerShell
4. Убедиться что пути не содержат спецсимволов (пробелы → кавычки)
5. Проверить `Task.WhenAll` для конкурентного чтения stdout/stderr (deadlock!)
6. Проверить что числа в FFmpeg аргументах используют `.` (InvariantCulture)

---

## Паттерн: баг в UI / биндинге

1. Найти свойство во ViewModel
2. Проверить `[ObservableProperty]` / `OnPropertyChanged`
3. Проверить биндинг в XAML (`Mode=`, `UpdateSourceTrigger=`)
4. Проверить конвертер если используется
5. Для `DynamicResource` — проверить что ключ есть в `lang/{code}/lang.json`

---

## Паттерн: баг в Hold Mode

1. Проверить `HotkeyService.cs` → `EnableHoldMode` / `DisableHoldMode` (SetWindowsHookEx)
2. Проверить `MainViewModel.cs` → `StartHoldAsync` / `EndHoldAsync`
3. Убедиться что `IsHolding` сбрасывается при отпускании
4. Проверить что `_holdTimer` остановлен и `HoldIndicator` скрыт

---

## Инструменты отладки

### Логи приложения

```
app\Logs\YYYY-MM-DD.log
```
Методы: `LogSvc.Info(msg)`, `LogSvc.Warn(msg)`, `LogSvc.Error(msg, ex)`.

Для временной отладки в коде:
```csharp
System.Diagnostics.Debug.WriteLine($"[ClipNotes] {message}");
```

### Ручная проверка FFmpeg

```powershell
$app = 'E:\Claude Workstation\Projects\ClipNotes\app'
$video = 'E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv'

# Длительность
& "$app\tools\ffprobe.exe" -v error -show_entries format=duration `
    -of default=noprint_wrappers=1:nokey=1 $video

# Нарезать клип (5 сек с 10-й секунды)
& "$app\tools\ffmpeg.exe" -i $video -ss 10 -t 5 `
    -vn -acodec pcm_s16le -ar 16000 -ac 1 'C:\temp\test.wav'
```

### Ручная проверка whisper-cli

```powershell
$app = 'E:\Claude Workstation\Projects\ClipNotes\app'
& "$app\tools\whisper-cli.exe" -m "$app\models\ggml-base.bin" `
    --output-txt -of 'C:\temp\test_out' 'C:\temp\test.wav'
# Создаст: C:\temp\test_out.txt
```
