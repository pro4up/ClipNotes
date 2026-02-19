# 03 — Тестирование функционала

## Предусловия

- Приложение собрано (`app\ClipNotes.exe` существует)
- OBS Studio запущен с WebSocket на `172.28.64.1:4455`
- Настройки приложения заданы (см. 02-build-and-scripts.md)

## Чек-лист тестирования

### 1. Подключение к OBS

- [ ] Запустить ClipNotes.exe
- [ ] В Настройках: Host = `172.28.64.1`, Port = `4455`, Password пустой
- [ ] Нажать «Проверить соединение»
- [ ] Статус должен показать "Connected"
- [ ] В логах (`Logs\YYYY-MM-DD.log`): нет ошибок авторизации SHA256

**Что проверить в коде при баге:** `ObsWebSocketService.cs` → метод аутентификации, формирование challenge-response.

### 2. Глобальные горячие клавиши

- [ ] Настроить хоткей добавления маркера (например, Ctrl+F1)
- [ ] Свернуть ClipNotes
- [ ] Нажать хоткей — маркер должен добавиться в список
- [ ] Убедиться что хоткей работает из любого окна

**Что проверить:** `HotkeyService.cs` → `RegisterHotKey` / `UnregisterHotKey`, `HwndSource`.

### 3. Добавление маркеров вручную (без OBS)

- [ ] Вкладка Экспорт → кнопка «Обзор», выбрать видеофайл
- [ ] Ввести тайм-код `ЧЧ:ММ:СС`, выбрать тип, нажать «+ Добавить»
- [ ] Маркер появился в списке, тайм-код автоматически сдвинулся на +30с
- [ ] Нажать «✕» для удаления маркера, убедиться что индексы переиндексированы

### 4. OBS запись — отслеживание старт/стоп

- [ ] Вкладка Запись → нажать «Начать запись»
- [ ] ClipNotes должен зафиксировать время начала, таймер пошёл
- [ ] Добавить маркеры через кнопки Bug/Task/Note или горячие клавиши
- [ ] Нажать «Остановить запись»
- [ ] ClipNotes должен перейти на вкладку Экспорт

### 5. Постобработка (FFmpeg + Whisper)

Для теста можно загрузить тестовое видео:
`E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv`

- [ ] FFmpeg извлекает master.wav (16kHz mono PCM) из видео
- [ ] FFmpeg нарезает аудиоклипы для каждого маркера
- [ ] Аудиоклипы появляются в `sessions\{session}\audio\`
- [ ] whisper-cli транскрибирует каждый клип (принимает готовый WAV-файл)
- [ ] Транскрипции появляются в `sessions\{session}\txt\`
- [ ] Excel-файл создаётся в `sessions\{session}\table\`

**FFmpeg аудио-извлечение (ручная проверка):**
```powershell
.\app\tools\ffmpeg.exe -i "recording.mkv" `
    -ss 00:00:05 -t 10 `
    -vn -acodec pcm_s16le -ar 16000 -ac 1 `
    "output.wav"
```

**whisper-cli (ручная проверка — передаётся готовый клип):**
```powershell
.\app\tools\whisper-cli.exe `
    -m .\app\models\ggml-base.bin `
    --output-txt `
    -of "C:\temp\test_clip" `
    "C:\temp\output.wav"
# Создаст C:\temp\test_clip.txt
```

### 6. Excel-экспорт

- [ ] Открыть сгенерированный `.xlsx` в Excel
- [ ] Столбцы: `#`, `Timecode`, `Тип`, `Текст заметки`, `Аудио`, `Транскрипция`
- [ ] Гиперссылки на аудио и транскрипцию работают (относительные пути)
- [ ] Данные корректны и совпадают с маркерами

### 7. Тестирование с моделью base (быстро)

Модель `ggml-base.bin` (~141 МБ) — для быстрого тестирования постобработки:
```powershell
.\build.ps1 -SkipDependencies -Model "base"
```
Затем в Настройках выбрать модель `base`.

## Автоматизированная проверка путей

```powershell
$app = 'E:\Claude Workstation\Projects\ClipNotes\app'
@(
    "$app\ClipNotes.exe",
    "$app\tools\ffmpeg.exe",
    "$app\tools\ffprobe.exe",
    "$app\tools\whisper-cli.exe"
) | ForEach-Object {
    if (Test-Path $_) { Write-Host "OK: $_" -ForegroundColor Green }
    else { Write-Host "MISSING: $_" -ForegroundColor Red }
}
$model = Get-ChildItem "$app\models\ggml-*.bin" -ErrorAction SilentlyContinue
if ($model) { Write-Host "OK: $($model.FullName)" -ForegroundColor Green }
else { Write-Host "MISSING: models\ggml-*.bin" -ForegroundColor Red }
```

## Известные ограничения

- Приложение только для Windows (WPF + WinAPI)
- whisper-cli требует WAV-файл 16kHz mono как вход (FFmpeg конвертирует)
- Большие модели (large-v3-turbo, ~1549 МБ) медленнее загружаются, но точнее
