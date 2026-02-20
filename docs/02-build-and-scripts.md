# 02 — Сборка и скрипты

## PowerShell скрипты — назначение и параметры

### `build.ps1` (мастер-скрипт)
Запускать из `source\` в PowerShell (Windows).

```powershell
# Полная сборка (зависимости + модель + компиляция)
.\build.ps1

# Без загрузки зависимостей (ffmpeg/whisper уже есть)
.\build.ps1 -SkipDependencies

# Без загрузки модели
.\build.ps1 -SkipModel

# С конкретной моделью
.\build.ps1 -Model "base"

# С CUDA-бэкендом для whisper
.\build.ps1 -Backend cuda

# Только компиляция (всё уже скачано)
.\build.ps1 -SkipDependencies -SkipModel
```

**Шаги build.ps1:**
1. Скачать FFmpeg → `app\tools\`
2. Собрать/скачать whisper-cli → `app\tools\`
3. Скачать GGML-модель → `app\models\`
4. `dotnet restore`
5. `dotnet publish` → `app\`
6. Создать лицензионные файлы → `app\licenses\`

### `tools\download-ffmpeg.ps1`
```powershell
# Со стандартным OutputDir (app\tools\)
.\tools\download-ffmpeg.ps1

# С кастомным путём
.\tools\download-ffmpeg.ps1 -OutputDir "C:\custom\path"
```
Источник: `gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip`
Кеш: `tools\.cache\ffmpeg-essentials.zip` (повторно не скачивает)
Извлекает только: `ffmpeg.exe`, `ffprobe.exe`

### `tools\download-whisper.ps1`
```powershell
.\tools\download-whisper.ps1              # CPU (OpenBLAS, работает везде)
.\tools\download-whisper.ps1 -Backend cuda  # NVIDIA CUDA 12
```
1. Клонирует `whisper.cpp v1.8.3` в `tools\.cache\whisper.cpp`
2. Собирает через CMake + VS2022 (или Ninja fallback)
3. Если сборка не удалась — скачивает prebuilt из GitHub Releases
4. Копирует `whisper-cli.exe` (+ DLL) в `app\tools\`

Бэкенды:
- `cpu` (по умолчанию) — OpenBLAS, работает на любом железе
- `cuda` — NVIDIA CUDA 12, требует CUDA Runtime
- Vulkan (AMD/Intel) — только через сборку из исходников с `-DGGML_VULKAN=ON`

**Требования:** Git, CMake, Visual Studio 2022 (или MSVC Build Tools)

### `tools\download-model.ps1`
```powershell
# Стандартная модель (large-v3-turbo)
.\tools\download-model.ps1

# Конкретная модель
.\tools\download-model.ps1 -ModelName "base"
.\tools\download-model.ps1 -ModelName "small"
.\tools\download-model.ps1 -ModelName "medium"
.\tools\download-model.ps1 -ModelName "large-v3"
.\tools\download-model.ps1 -ModelName "large-v3-turbo"
```
Источник: Hugging Face `ggerganov/whisper.cpp`
**Проверяет размер файла** против Content-Length — не пропустит неполную загрузку.

## Компиляция напрямую (без build.ps1)

```powershell
cd 'E:\Claude Workstation\Projects\ClipNotes\source'
dotnet publish ClipNotes/ClipNotes.csproj `
    -c Release -r win-x64 --self-contained true `
    -o 'E:\Claude Workstation\Projects\ClipNotes\app'
```

## Запуск приложения

```
E:\Claude Workstation\Projects\ClipNotes\app\ClipNotes.exe
```

**При первом запуске:**
1. Открыть вкладку Настройки
2. Указать OBS Host: `172.28.64.1:4455`
3. Указать OutputDirectory: `E:\Claude Workstation\Projects\ClipNotes\sessions\`
4. Выбрать модель (для теста: `base`)
5. Нажать Save

## Результат сборки (app\)

```
app\
├── ClipNotes.exe         ← запускаемый файл
├── tools\
│   ├── ffmpeg.exe
│   ├── ffprobe.exe
│   └── whisper-cli.exe   ← + DLL (whisper.dll, ggml.dll, ggml-cpu.dll и др.)
├── models\
│   └── ggml-{model}.bin
└── licenses\
    ├── LICENSES.txt
    ├── FFmpeg-LICENSE.txt
    ├── whisper.cpp-MIT.txt
    └── ClosedXML-MIT.txt
```

## Проверка после сборки

```powershell
# Проверить наличие всех компонентов
Test-Path 'E:\Claude Workstation\Projects\ClipNotes\app\ClipNotes.exe'
Test-Path 'E:\Claude Workstation\Projects\ClipNotes\app\tools\ffmpeg.exe'
Test-Path 'E:\Claude Workstation\Projects\ClipNotes\app\tools\whisper-cli.exe'
Get-ChildItem 'E:\Claude Workstation\Projects\ClipNotes\app\models\ggml-*.bin'
```

## Сборка установочников

### Быстрый способ (рекомендуется)

```powershell
.\rebuild-installers.ps1              # Online Setup + Portable ZIP
.\rebuild-installers.ps1 -Offline    # + Offline Setup (~450 MB, нужен интернет для CUDA whisper)
.\rebuild-installers.ps1 -PortableOnly
```

### Через build.ps1

```powershell
.\build.ps1 -SkipDependencies -SkipModel -BuildSetup          # Online Setup
.\build.ps1 -SkipDependencies -SkipModel -BuildPortable        # Portable ZIP
.\build.ps1 -SkipDependencies -SkipModel -BuildOfflineSetup    # Offline Setup (все модели + CUDA)
```

### Результат (Setup\)

```
Setup\
├── ClipNotes-Setup.exe           ← Online installer (~10 MB)
├── ClipNotes-Setup-Offline.exe   ← Offline installer EXE
├── ClipNotes-offline-bundle.zip  ← Бандл с инструментами и моделями (~450 MB)
├── ClipNotes-portable.zip        ← Portable без models/
└── SHA256SUMS.txt
```

## Проблемы при сборке

| Проблема | Решение |
|----------|---------|
| `whisper-cli.exe` не собрался | Установить VS2022 Build Tools + CMake |
| Модель неполная | Удалить файл, запустить download-model.ps1 снова |
| `dotnet publish` не найден | Установить .NET 8 SDK |
| Permission denied в WSL при mv | Использовать `cp -r` + `rm -rf` вместо `mv` |
