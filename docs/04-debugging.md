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
| FFmpeg/Whisper | `AudioProcessingService.cs` | Deadlock, exit code, путь к файлу |
| Экспорт Excel | `ExcelExportService.cs` | Кодировка, формат таймкода |
| Пути к файлам | `PathHelper.cs` | Неверный AppDir |
| UI биндинги | `MainViewModel.cs` | NotifyPropertyChanged, Commands |
| Настройки | `SettingsService.cs` | JSON десериализация |

## Инструменты отладки

### Логи приложения

Смотреть в Output window Visual Studio или добавить:
```csharp
System.Diagnostics.Debug.WriteLine($"[ClipNotes] {message}");
```

### Ручная проверка FFmpeg

```powershell
# Извлечь аудио из тестового видео (5 сек с 10-й секунды)
& 'E:\Claude Workstation\Projects\ClipNotes\app\tools\ffmpeg.exe' `
    -i 'E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv' `
    -ss 10 -t 5 -vn -acodec pcm_s16le 'C:\temp\test.wav'
```

### Ручная проверка whisper-cli

```powershell
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
5. Проверить `Task.WhenAll` для stdout/stderr

## Паттерн: баг в UI / биндинге

1. Найти свойство во ViewModel
2. Проверить `[ObservableProperty]` / `OnPropertyChanged`
3. Проверить биндинг в XAML (`Mode=`, `UpdateSourceTrigger=`)
4. Проверить конвертер если используется
