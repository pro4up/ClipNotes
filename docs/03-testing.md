# 03 — Тестирование функционала

## Предусловия

- Приложение собрано (`app\ClipNotes.exe` существует)
- OBS Studio запущен с WebSocket на `172.28.64.1:4455`
- Настройки приложения заданы (см. 02-build-and-scripts.md)

## Чек-лист тестирования

### 1. Подключение к OBS

- [ ] Запустить ClipNotes.exe
- [ ] В Settings: Host = `172.28.64.1:4455`, Password пустой
- [ ] Нажать Connect (или кнопка подключения)
- [ ] Статус должен показать "Connected"
- [ ] В логах: нет ошибок авторизации SHA256

**Что проверить в коде при баге:** `ObsWebSocketService.cs` → метод аутентификации, формирование challenge-response.

### 2. Глобальные горячие клавиши

- [ ] Настроить хоткей добавления маркера (например, F9)
- [ ] Свернуть ClipNotes
- [ ] Нажать хоткей — маркер должен добавиться в список
- [ ] Убедиться что хоткей работает из любого окна

**Что проверить:** `HotkeyService.cs` → `RegisterHotKey` / `UnregisterHotKey`, `HwndSource`.

### 3. Добавление маркеров вручную

- [ ] OBS запущен, запись активна (или симуляция)
- [ ] Ввести текст в поле маркера
- [ ] Нажать кнопку добавления / хоткей
- [ ] Маркер появился в списке с таймкодом

### 4. OBS запись — отслеживание старт/стоп

- [ ] Начать запись в OBS
- [ ] ClipNotes должен зафиксировать время начала
- [ ] Остановить запись в OBS
- [ ] ClipNotes должен автоматически запустить постобработку

### 5. Постобработка (FFmpeg + Whisper)

Для этого теста можно использовать тестовое видео:
`E:\Claude Workstation\Projects\video\2026-02-17_01-12-07\video\recording.mkv`

- [ ] FFmpeg извлекает аудио-сниппет для каждого маркера
- [ ] Аудио появляется в `sessions\{session}\audio\`
- [ ] whisper-cli запускается для каждого файла
- [ ] Транскрипции появляются в `sessions\{session}\txt\`
- [ ] Excel-файл создаётся в `sessions\{session}\table\`

**whisper-cli аргументы (для ручной проверки):**
```powershell
.\app\tools\whisper-cli.exe `
    -m .\app\models\ggml-base.bin `
    --offset-t 5000 `
    --duration 10000 `
    --output-txt `
    -of "C:\temp\test_clip" `
    "path\to\audio.wav"
```

**FFmpeg аудио-извлечение (для ручной проверки):**
```powershell
.\app\tools\ffmpeg.exe -i "recording.mkv" `
    -ss 00:00:05 -t 10 `
    -vn -acodec pcm_s16le `
    "output.wav"
```

### 6. Excel-экспорт

- [ ] Открыть сгенерированный .xlsx в Excel
- [ ] Колонки: таймкод, текст маркера, транскрипция, путь к аудио
- [ ] Данные корректны и совпадают с маркерами

### 7. Тестирование с моделью base (быстро)

Модель `ggml-base.bin` (148 МБ) — для быстрого тестирования постобработки:
```powershell
.\build.ps1 -SkipDependencies -Model "base"
```
Затем в Settings выбрать модель `base`.

## Автоматизированная проверка путей

```powershell
# Запустить и проверить все компоненты
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
- whisper-cli требует WAV-файл как вход (FFmpeg конвертирует)
- Большие модели (large-v3-turbo, 1549 МБ) медленнее, но точнее
