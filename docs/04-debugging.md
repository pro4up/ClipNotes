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

## Известные баги и их решения

### Deadlock при запуске FFmpeg или whisper-cli

**Симптом:** Постобработка зависает навсегда.

**Причина:** Последовательное чтение stdout, затем stderr (или наоборот) — процесс блокируется когда буфер заполнен.

**Решение:**
```csharp
// НЕПРАВИЛЬНО:
var stdout = await process.StandardOutput.ReadToEndAsync();
var stderr = await process.StandardError.ReadToEndAsync();

// ПРАВИЛЬНО:
var stdoutTask = process.StandardOutput.ReadToEndAsync();
var stderrTask = process.StandardError.ReadToEndAsync();
await Task.WhenAll(stdoutTask, stderrTask);
var stdout = stdoutTask.Result;
var stderr = stderrTask.Result;
```

Проверить: `AudioProcessingService.cs` — все места запуска процессов.

### Неполная загрузка модели

**Симптом:** whisper-cli крашится или выдаёт мусор.

**Проверка:**
```powershell
(Get-Item 'E:\Claude Workstation\Projects\ClipNotes\app\models\ggml-large-v3-turbo.bin').Length / 1MB
# Должно быть ~1549 МБ, если 756 — файл неполный
```

**Решение:** Удалить файл, запустить `.\tools\download-model.ps1 -ModelName "large-v3-turbo"`.

### whisper-cli не принимает аргументы

**Симптом:** Ошибка "unknown option" или транскрипция всего файла вместо сниппета.

**Правильные аргументы:**
```
--offset-t N      # N в миллисекундах (не секундах!)
--duration N      # N в миллисекундах
--output-txt      # флаг без значения
-of "base_path"   # путь без расширения, создаст base_path.txt
```

### OBS не подключается

**Диагностика:**
```powershell
# Проверить что OBS WebSocket слушает
Test-NetConnection -ComputerName 172.28.64.1 -Port 4455
```

**Частые причины:**
- OBS WebSocket плагин не включён (OBS → Tools → WebSocket Server Settings)
- Неверный IP (в WSL хост-машина обычно `172.x.x.1`, не `localhost`)
- Файрвол блокирует порт

### Горячая клавиша не срабатывает

**Проверить:**
1. `HotkeyService.cs` — вызов `RegisterHotKey` возвращает true?
2. Конфликт с другим приложением (Windows не даёт зарегистрировать уже занятый хоткей)
3. `HwndSource` создан до регистрации хоткея?

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
