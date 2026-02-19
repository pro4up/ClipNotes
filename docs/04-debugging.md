# 04 — Отладка и поиск багов

## Методология поиска бага

1. **Воспроизвести** — чёткие шаги для воспроизведения
2. **Локализовать** — какой слой (UI, ViewModel, Service, FFmpeg/Whisper, OBS)
3. **Прочитать код** — только нужный файл, не весь проект
4. **Исправить минимально** — не рефакторить попутно
5. **Проверить** — повторить шаги воспроизведения
6. **Коммитить** — только по запросу пользователя

## Карта слоёв → файлы

| Слой | Файл | Типичные баги |
|------|------|---------------|
| OBS подключение | `ObsWebSocketService.cs` | SHA256 auth, WebSocket handshake |
| Горячие клавиши | `HotkeyService.cs` | Конфликт хоткеев, утечка регистрации |
| FFmpeg операции | `FFmpegService.cs` | Exit code, deadlock stdout/stderr, locale (запятая вместо точки) |
| Whisper транскрипция | `WhisperService.cs` | Deadlock stdout/stderr, путь к клипу, путь к модели |
| Постобработка | `PipelineService.cs` | Порядок шагов, ошибки в середине pipeline |
| Экспорт Excel | `ExcelService.cs` | Кодировка, формат таймкода, относительные пути |
| Управление сессией | `SessionService.cs` | Ожидание освобождения видеофайла OBS (до 60 сек) |
| Пути к файлам | `PathHelper.cs` | Неверный AppDir (Assembly.Location) |
| UI биндинги | `MainViewModel.cs` | ObservableProperty, Commands, состояние вкладок |
| Настройки | `SettingsService.cs` | JSON десериализация, путь к settings.json |
| Логи | `LogService.cs` | Путь к Logs/ (Process.MainModule?.FileName) |

## Инструменты отладки

### Логи приложения

Файл: `app\Logs\YYYY-MM-DD.log` — пишется автоматически через `LogService`.

Для дополнительной отладки в коде:
```csharp
System.Diagnostics.Debug.WriteLine($"[ClipNotes] {message}");
```

### Ручная проверка FFmpeg

```powershell
# Получить длительность видео
& 'E:\Claude Workstation\Projects\ClipNotes\app\tools\ffprobe.exe' `
    -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 `
    'E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv'

# Извлечь аудио из тестового видео (5 сек с 10-й секунды)
& 'E:\Claude Workstation\Projects\ClipNotes\app\tools\ffmpeg.exe' `
    -i 'E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv' `
    -ss 10 -t 5 -vn -acodec pcm_s16le -ar 16000 -ac 1 'C:\temp\test.wav'
```

### Ручная проверка whisper-cli

```powershell
# Передаётся готовый аудиоклип (без --offset-t / --duration)
& 'E:\Claude Workstation\Projects\ClipNotes\app\tools\whisper-cli.exe' `
    -m 'E:\Claude Workstation\Projects\ClipNotes\app\models\ggml-base.bin' `
    --output-txt -of 'C:\temp\test_out' 'C:\temp\test.wav'
# Должен создать C:\temp\test_out.txt
```

## Паттерн: баг в постобработке

При баге в FFmpeg/Whisper pipeline:
1. Проверить exit code процесса
2. Читать stderr — там обычно ошибка
3. Воспроизвести команду вручную в PowerShell
4. Убедиться что пути не содержат спецсимволов (пробелы → кавычки)
5. Проверить `Task.WhenAll` для конкурентного чтения stdout/stderr (deadlock!)
6. Проверить что числа в FFmpeg аргументах используют `.` (не `,`) как разделитель — использовать `CultureInfo.InvariantCulture`

## Паттерн: баг в UI / биндинге

1. Найти свойство во ViewModel
2. Проверить `[ObservableProperty]` / `OnPropertyChanged`
3. Проверить биндинг в XAML (`Mode=`, `UpdateSourceTrigger=`)
4. Проверить конвертер если используется
