# ClipNotes — Архитектура и документация проекта

## Содержание

1. [Обзор проекта](#обзор-проекта)
2. [Структура директорий](#структура-директорий)
3. [Архитектура приложения](#архитектура-приложения)
4. [Модели данных](#модели-данных)
5. [Сервисы](#сервисы)
6. [ViewModel и UI](#viewmodel-и-ui)
7. [Конфигурация и настройки](#конфигурация-и-настройки)
8. [Сборка и зависимости](#сборка-и-зависимости)
9. [Технические решения](#технические-решения)
10. [Известные проблемы и ограничения](#известные-проблемы-и-ограничения)

---

## Обзор проекта

**ClipNotes** — десктопное Windows-приложение (C# .NET 8 WPF) для автоматизированного захвата таймкодированных заметок во время записи в OBS Studio. По окончании записи генерирует Excel-таблицу, аудио-сниппеты и текстовые транскрипции для каждого маркера.

**Технологический стек:**
- C# .NET 8, WPF, MVVM (CommunityToolkit.Mvvm 8.4)
- OBS WebSocket v5 (ручная реализация без внешней библиотеки)
- FFmpeg (извлечение аудио, нарезка клипов)
- whisper.cpp / whisper-cli (транскрипция)
- ClosedXML (генерация .xlsx)
- WinAPI RegisterHotKey (глобальные горячие клавиши)

---

## Структура директорий

```
C:\Projects\ClipNotes\
├── source\                        ← исходный код (git repo)
│   ├── ClipNotes.sln
│   ├── build.ps1                  ← мастер-скрипт сборки
│   ├── README.md
│   ├── ARCHITECTURE.md            ← этот файл
│   ├── tools\                     ← PowerShell скрипты загрузки зависимостей
│   │   ├── download-ffmpeg.ps1
│   │   ├── download-whisper.ps1   ← параметр -Backend cpu|cuda
│   │   └── download-model.ps1
│   ├── ClipNotes.Setup\           ← WPF-установщик (7 страниц, UAC, ru/en)
│   ├── ClipNotes.Uninstaller\     ← Деинсталлятор (самоудаление)
│   ├── rebuild-installers.ps1     ← Быстрая пересборка установочников
│   ├── resource\                  ← исходные PNG для иконок
│   └── ClipNotes\                 ← C# проект
│       ├── ClipNotes.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models\
│       ├── Services\
│       ├── ViewModels\
│       ├── Views\
│       ├── Converters\
│       └── Helpers\
│
├── app\                           ← корень сборки (build.ps1 output root)
│   └── app\                       ← dotnet publish output (ClipNotes.exe + Uninstaller.exe)
│       ├── ClipNotes.exe
│       ├── ClipNotes.Uninstaller.exe
│       ├── lang\                  ← локализация (ru/lang.json, en/lang.json, ...)
│       ├── tools\                 ← ffmpeg.exe, ffprobe.exe, whisper-cli.exe + DLL
│       ├── models\                ← ggml-large-v3-turbo.bin
│       └── licenses\
│
└── sessions\                      ← данные записанных сессий (OutputDirectory)
    └── YYYY-MM-DD_HH-mm-ss\
        ├── video\
        ├── audio\
        ├── txt\
        ├── table\
        └── meta\
```

---

## Архитектура приложения

### Паттерн MVVM

```
Views (XAML)  ←→  MainViewModel  ←→  Services
                       ↑
                    Models
```

- **Models** — чистые данные (Marker, MarkerType, SessionData, AppSettings, HotkeyAction, HotkeyBinding)
- **Services** — бизнес-логика без UI (OBS, FFmpeg, Whisper, Excel, Hotkeys, Session, Pipeline, Settings, Log)
- **ViewModel** — `MainViewModel` связывает всё; один ViewModel для всего приложения
- **Views** — `MainWindow.xaml` + `App.xaml` со стилями

### Один экран, 4 вкладки

```
TabControl
├── Запись      — таймер, кнопки Bug/Task/Note, список маркеров
├── Экспорт     — загрузка видео, ручное добавление маркеров, прогресс генерации
├── История     — список прошлых сессий с быстрым доступом
└── Настройки   — OBS, директория, аудио, транскрипция, горячие клавиши, трей, тема
```

---

## Модели данных

### `MarkerType.cs`
```
enum MarkerType { Bug, Task, Note, Summary }
```

### `Marker.cs` (ObservableObject)
| Поле | Тип | Описание |
|---|---|---|
| Index | int | Порядковый номер |
| Type | MarkerType | Тип маркера |
| Timestamp | TimeSpan | Позиция в записи (из OBS) |
| Timecode | string | Строка "HH:MM:SS.ms" |
| Text | string | Метка/комментарий |
| GenerateAudio | bool | Флаг генерации аудио-клипа |
| GenerateText | bool | Флаг транскрипции |
| AudioFilePath | string? | Путь к аудио-клипу |
| TextFilePath | string? | Путь к файлу транскрипции |
| HoldDuration | TimeSpan? | Длительность удержания (Hold Mode, null = обычный маркер) |

Computed: `TimestampFormatted`, `HoldDurationText` — форматированные строки для UI.

### `SessionData.cs`
Метаданные сессии: имя, папка, путь к видео, путь к master.wav, продолжительность, время начала/конца, список маркеров, снимок настроек, исходный путь видео OBS.

Сериализуется в `meta/session.json`.

### `AppSettings.cs`
Настройки приложения (хранятся в `%APPDATA%\ClipNotes\settings.json`):

| Группа | Поля |
|---|---|
| OBS | ObsHost, ObsPort, ObsPassword |
| OBS автозапуск | AutoStartObs (false), ObsExePath |
| Директория | OutputRootDirectory |
| Аудио | PreSeconds (5), PostSeconds (5), AudioCodec ("wav"), AudioBitrate (192) |
| Транскрипция | WhisperModel ("large-v3-turbo"), TranscriptionLanguage ("auto"), Glossary, GlossaryFilePath |
| Hold Mode | HoldModeEnabled (false), HoldPreSeconds (2), HoldPostSeconds (2) |
| Горячие клавиши | List<HotkeyBindingData> |
| Именование | AskSessionName (true), AppendDateSuffix (false), DateSuffixFormat |
| Кастомные пути | UseCustomVideoPath/AudioPath/TxtPath/TablePath + кастомные пути |
| Трей / автозапуск | StartWithWindows (false), MinimizeToTray (false) |
| Тема | Theme ("Светлая") |
| Язык | Language ("ru") |
| История | List<SessionHistoryEntry>, MaxHistoryCount (20), DeleteFilesOnClear (false), ClearMarkersOnVideoLoad (true) |

Горячие клавиши по умолчанию: Ctrl+F1 (Bug), Ctrl+F2 (Task), Ctrl+F3 (Note), Ctrl+F4 (Summary), Ctrl+F9 (Start), Ctrl+F10 (Stop), Ctrl+F11 (Generate), Ctrl+F12 (OpenFolder).

### `HotkeyBinding.cs` (ObservableObject)
| Поле | Описание |
|---|---|
| Action | HotkeyAction (enum) |
| Key | System.Windows.Input.Key |
| Modifiers | ModifierKeys (флаги: Ctrl/Shift/Alt/Win) |
| DisplayName | Локализованное название действия |
| HotkeyText | Строка "Ctrl+F1" для отображения |

---

## Сервисы

### `ObsWebSocketService.cs`
Ручная реализация клиента OBS WebSocket Protocol v5.

**Протокол handshake:**
1. Получить `Hello` (op=0) с salt + challenge
2. Вычислить auth: `Base64(SHA256(Base64(SHA256(password + salt)) + challenge))`
3. Отправить `Identify` (op=1) с authString
4. Получить `Identified` (op=2)

**Основные методы:**
- `ConnectAsync(host, port, password)` — подключение с аутентификацией
- `StartRecordAsync()` / `StopRecordAsync()` — управление записью
- `GetRecordStatusAsync()` → `(TimeSpan duration, string timecode, bool isRecording)`
- `SetRecordDirectoryAsync(path)` — установить директорию записи OBS

**События:** `RecordStateChanged`, `RecordStopped`

**Детали реализации:**
- `ClientWebSocket` из `System.Net.WebSockets`
- Отдельный поток для приёма сообщений (`ReceiveLoop`)
- `ConcurrentDictionary<string, TaskCompletionSource<JsonElement>>` для correlation request/response по requestId
- Тайм-аут запроса: 10 секунд

### `FFmpegService.cs`
Обёртка над `ffmpeg.exe` и `ffprobe.exe`.

| Метод | Описание |
|---|---|
| `GetDurationAsync(videoPath)` | ffprobe → продолжительность видео как TimeSpan |
| `ExtractMasterAudioAsync(videoPath, outputPath)` | Извлечь WAV 16kHz mono PCM (для whisper) |
| `ExtractAudioClipAsync(masterWav, start, end, outputPath, codec, bitrate)` | Нарезать клип из master.wav |

FFmpeg команда для master: `-ar 16000 -ac 1 -c:a pcm_s16le`

### `WhisperService.cs`
Запуск `whisper-cli.exe` для транскрипции аудио-клипов.

```
whisper-cli.exe -m <model> -f <audioClipPath> -t <threads>
                [-l <lang>] [--prompt <glossary>]
                --output-txt -of <outputBase>
```

Принимает **готовый нарезанный WAV-клип** (без `--offset-t`/`--duration`).
Читает результат из `<outputBase>.txt`.

### `ExcelService.cs`
Генерация XLSX через ClosedXML.

**Столбцы:** `#`, `Timecode`, `Тип`, `Текст заметки`, `Аудио`, `Транскрипция`

Гиперссылки на аудио и транскрипцию — **относительные пути** (relative to XLSX file).

### `HotkeyService.cs`
WinAPI глобальные горячие клавиши.

- P/Invoke: `RegisterHotKey`, `UnregisterHotKey` из `user32.dll`
- `WndProc` hook через `HwndSource` (WPF interop)
- `WM_HOTKEY` = 0x0312
- Флаг `MOD_NOREPEAT` (0x4000) — не дублировать при удержании
- Требует инициализации ПОСЛЕ показа окна (нужен HWND)

**Hold Mode:** `SetWindowsHookEx(WH_KEYBOARD_LL)` + низкоуровневый перехват KeyDown/KeyUp. Длительность маркера = время удержания клавиши. Поддерживаются все 4 типа маркеров: Bug, Task, Note, Summary.

### `SessionService.cs`
Управление структурой папок сессии.

```
<output>/<YYYY-MM-DD_HH-mm-ss>/
├── video/    ← видеофайл OBS
├── audio/    ← master.wav + клипы
├── txt/      ← транскрипции
├── table/    ← ClipNotes.xlsx
└── meta/     ← session.json
```

Метод `MoveVideoToSession` — ждёт освобождения файла OBS до 60 секунд (проверка каждую секунду через `FileStream` с `FileShare.None`).

### `PipelineService.cs`
Оркестратор постобработки. Вызывается после остановки записи.

**Порядок шагов:**
1. Переместить видео в папку сессии
2. Получить длительность видео (ffprobe)
3. Извлечь master.wav (16kHz mono PCM)
4. Для каждого маркера (если `GenerateAudio` или `GenerateText`):
   - Вычислить окно: `[timestamp - pre, timestamp + post]` (с зажимом к границам)
   - Нарезать аудио-клип из master.wav
   - Если `GenerateText`: транскрибировать через whisper-cli
5. Сгенерировать XLSX
6. Сохранить `session.json`
7. Добавить в историю настроек

**События:** `StatusChanged(string message)`, `ProgressChanged(int current, int total)`

### `SettingsService.cs`
- Путь: `%APPDATA%\ClipNotes\settings.json`
- `LoadSettings()` — десериализация JSON, при ошибке возвращает `new AppSettings()`
- `SaveSettings(AppSettings)` — сериализация с отступами

### `LogService.cs`
Статический класс логирования. Путь к лог-файлу определяется через `Process.GetCurrentProcess().MainModule?.FileName` (не `Assembly.Location` — при single-file publish он пустой).

- Файлы: `%APPDATA%\ClipNotes\logs\YYYY-MM-DD.log` (не рядом с exe — во избежание PermissionDenied в Program Files)
- Методы: `Info`, `Warn`, `Error(message, ex?)`

### `LocalizationService.cs`
Статический класс локализации. Загружает строки из `lang/{code}/lang.json` в `Application.Current.Resources`, откуда WPF подхватывает их через `{DynamicResource loc_XXX}`.

- `Load(lang)` — загрузить язык; fallback на `en`, затем ничего не делать
- `T(key)` — получить строку по ключу (используется в коде, не в XAML)
- `GetAvailableLanguages()` — список папок в `lang/`, сортированный
- Пользователи могут добавить `lang/{code}/lang.json` для своего языка

---

## ViewModel и UI

### `MainViewModel.cs`

Единственный ViewModel. Зависимости (сервисы) создаются внутри конструктора.

**Состояния UI:**
| Свойство | Описание |
|---|---|
| IsRecording | Идёт запись |
| IsConnected | OBS подключён |
| IsProcessing | Идёт постобработка |
| IsHolding | Активен Hold Mode (клавиша/кнопка зажата) |
| HoldingType | Тип маркера для индикатора удержания |
| HoldingTimerText | Секунды удержания (обновляется каждые 100мс) |
| CurrentTimecode | "HH:MM:SS" для отображения |
| MarkersCountLabel | Локализованный заголовок "Маркеры (N)" |
| Markers | ObservableCollection<Marker> |
| Sessions | ObservableCollection<SessionHistoryEntry> |

**Основные команды:**
| Команда | Описание |
|---|---|
| TestObsConnection | Подключиться/проверить OBS |
| StartRecording | Начать запись через OBS |
| StopRecording | Остановить запись, запустить pipeline |
| AddMarker(type) | Добавить маркер с текущим таймкодом |
| Generate | Запустить постобработку для текущей сессии |
| CancelGeneration | Отменить pipeline |
| OpenSessionFolder | Открыть папку текущей сессии в Explorer |
| OpenHistoryFolder | Открыть папку сессии из истории |

**Таймер записи:** `DispatcherTimer` опрашивает `GetRecordStatusAsync()` каждые 500мс.

### `MainWindow.xaml` — дизайн

Apple-like минималистичный стиль:
- Фон: `#FAFAFA` (светло-серый)
- Карточки: белые с скруглёнными углами (`CornerRadius="12"`) и тенью
- Акцент: `#007AFF` (синий, как iOS)
- Шрифт: Segoe UI
- Кнопки: скруглённые (`CornerRadius="8"`), без рамок

### `App.xaml` — глобальные стили

Определены ResourceDictionary стили:
- `BgBrush`, `CardBrush`, `AccentBrush`, `DangerBrush`
- `BaseTextStyle`, `HeadingStyle`, `SubHeadingStyle`, `LabelStyle`
- `CardStyle` — контейнер с тенью
- `PrimaryButtonStyle`, `SecondaryButtonStyle`, `DangerButtonStyle`
- `MarkerButtonStyle` — цветные кнопки Bug/Task/Note
- `ModernTabItemStyle`, `ModernComboBoxStyle`

---

## Конфигурация и настройки

### Файл настроек
`%APPDATA%\ClipNotes\settings.json` — автосохраняется при каждом изменении.

### PathHelper.cs
Определяет пути к внешним инструментам. `AppDir` вычисляется через `Process.GetCurrentProcess().MainModule?.FileName` (не `Assembly.Location` — при single-file publish он пустой).

```csharp
AppDir/tools/ffmpeg.exe
AppDir/tools/ffprobe.exe
AppDir/tools/whisper-cli.exe
AppDir/models/ggml-{model}.bin
AppDir/lang/{code}/lang.json
```

---

## Сборка и зависимости

### `build.ps1` — параметры

```powershell
.\build.ps1 [-SkipDependencies] [-SkipModel] [-Model <name>] [-Configuration <Release|Debug>]
            [-BuildSetup] [-BuildOfflineSetup]
```

| Флаг | Описание |
|---|---|
| `-SkipDependencies` | Не качать FFmpeg и whisper-cli (уже есть) |
| `-SkipModel` | Не качать основную модель (уже есть) |
| `-BuildSetup` | Собрать онлайн-установщик → `Setup/ClipNotes-Setup.exe` + `ClipNotes-bundle.zip` |
| `-BuildOfflineSetup` | Собрать offline-установщик → `Setup/ClipNotes-Setup-Offline.exe` + `ClipNotes-offline-bundle.zip` (~6.5 ГБ, все модели) |

### `rebuild-installers.ps1` — пересборка установщика

```powershell
.\rebuild-installers.ps1
```

### ClipNotes.Setup (WPF-установщик)

7 страниц: Welcome → Options → Backend → Model → Summary → Progress → Finish

- Скачивает FFmpeg, whisper-cli, модель во время установки (требует интернет)
- UAC: если путь требует прав → WPF-диалог → перезапуск с `runas --direct-install`
- Темная/светлая тема: авто через реестр `AppsUseLightTheme`
- Регистрация в «Программах и компонентах» (Add/Remove Programs)
- Создаёт ярлыки рабочего стола и меню «Пуск»

### ClipNotes.Uninstaller

- WPF-приложение с диалогом подтверждения (кнопка «Удалить»)
- Читает `InstallLocation` из реестра (`HKLM/HKCU\...\Uninstall\ClipNotes`)
- Удаляет файлы приложения, ярлыки рабочего стола и меню «Пуск», ключ Run (автозапуск)
- Самоудаление: `cmd /c ping 127.0.0.1 -n 8 & rmdir /s /q "<installDir>"` — удаляет корень после выхода процесса
- Авто-закрытие через 2 секунды после успешного удаления
- Запускается из установщика или «Программ и компонентов»

**Шаги:**
1. `download-ffmpeg.ps1` — скачать FFmpeg essentials с gyan.dev, извлечь ffmpeg.exe + ffprobe.exe
2. `download-whisper.ps1` — собрать whisper-cli из исходников (CMake + MSVC) или скачать prebuilt; параметр `-Backend cpu|cuda`
3. `download-model.ps1` — скачать ggml-модель с huggingface.co/ggerganov/whisper.cpp
4. `dotnet publish` — self-contained win-x64
5. Скопировать tools/ и models/ в compile/

### Зависимости в app/

```
tools/
├── ffmpeg.exe         ← из gyan.dev FFmpeg essentials build
├── ffprobe.exe        ← из gyan.dev FFmpeg essentials build
├── whisper-cli.exe    ← собран из whisper.cpp v1.8.3 (CPU/BLAS или CUDA)
├── whisper.dll        ← runtime DLL whisper.cpp
├── ggml.dll           ← runtime DLL ggml
├── ggml-cpu.dll       ← runtime DLL ggml CPU backend
├── ggml-base.dll      ← runtime DLL ggml base
└── SDL2.dll           ← SDL2 (нужен whisper-cli)

models/
└── ggml-large-v3-turbo.bin   ← ~1.6 ГБ, модель Whisper
```

### NuGet пакеты (ClipNotes.csproj)

```xml
<PackageReference Include="ClosedXML" Version="0.104.2" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
<PackageReference Include="System.Text.Json" Version="8.0.5" />
```

---

## Технические решения

### Почему ручной клиент OBS WebSocket

Готовые библиотеки (OBSWebsocketDotNet) зачастую отстают от протокола v5 или имеют лишние зависимости. Реализация на `System.Net.WebSockets.ClientWebSocket` даёт полный контроль над протоколом и нет внешних NuGet-зависимостей.

### Почему master.wav для нарезки

Нарезать клипы напрямую из видео — медленнее из-за декодирования. Промежуточный WAV-файл (16kHz mono PCM) позволяет быстро нарезать клипы и одновременно является готовым форматом для whisper-cli.

### Почему `MoveVideoToSession` ждёт 60 секунд

OBS может не сразу освободить файл после остановки записи (финализация контейнера). Ожидание с попытками открыть файл эксклюзивно надёжнее, чем фиксированная задержка.

### Относительные гиперссылки в Excel

Абсолютные пути ломаются при переносе папки. Относительные пути (../audio/clip.wav, ../txt/clip.txt) работают корректно если структура папок сохранена.

### Hotkey через HwndSource

WPF не предоставляет нативного API для глобальных горячих клавиш. `HwndSource` позволяет подключить WinAPI `WndProc` к WPF-окну без создания отдельного Win32 окна. Инициализация возможна только после `Window.Loaded` (нужен реальный HWND).

---

## Известные проблемы и ограничения

### Платформа
- Только Windows 10/11 x64 (WPF + WinAPI RegisterHotKey)
- Требуется OBS Studio 28+ (WebSocket v5 встроен с v28)

### Транскрипция
- whisper-cli — синхронный процесс; длинные клипы блокируют поток pipeline
- Модель large-v3-turbo: ~1.6 ГБ, требует ~4 ГБ RAM при работе

### OBS WebSocket
- Не поддерживается переподключение в реальном времени (нужен перезапуск приложения)
- GetRecordStatus опрашивается каждые 500мс — при высокой нагрузке возможны задержки таймкода

### Первый запуск
- Без предварительного запуска `build.ps1` приложение запустится, но выдаст ошибки при попытке генерации (нет ffmpeg/whisper-cli)

### Горячие клавиши
- Конфликт с другими приложениями (например, F1-F3 в браузерах) — решается сменой комбинаций в настройках
- После смены горячих клавиш в UI нужно переинициализировать (кнопка «Применить» или перезапуск)

---

## Статус проекта (на дату создания документа)

- [x] Полная реализация всех моделей, сервисов, ViewModel
- [x] WPF UI с 4 вкладками в Apple-стиле
- [x] OBS WebSocket v5 клиент (handshake, auth, requests)
- [x] FFmpeg интеграция (master audio, клипы)
- [x] whisper-cli интеграция (транскрипция с глоссарием)
- [x] ClosedXML Excel-генерация с relative hyperlinks
- [x] WinAPI глобальные горячие клавиши
- [x] Постобработка pipeline (PipelineService)
- [x] История сессий
- [x] build.ps1 (FFmpeg + whisper-cli + model + publish)
- [x] Self-contained win-x64 сборка
- [x] Лицензионные файлы
- [x] Иконка приложения (icon.ico + tray.ico)
- [x] Тёмная/светлая тема (DWM title bar)
- [x] Автозапуск OBS, трей, автозапуск Windows
- [x] Installer (ClipNotes.Setup WPF + ClipNotes.Uninstaller)
- [x] JSON-локализация (ru/en; пользовательские языки через lang/{code}/lang.json)
- [x] Hold Mode (длительность клипа = время удержания горячей клавиши)
- [x] Hold Mode визуальный индикатор (пульсирующий badge с таймером)
- [x] Hold Mode через UI-кнопки (удержание ЛКМ)
- [x] Маркер Summary (4-й тип, фиолетовый)
- [x] Именование сессий (диалог ввода + авто-суффикс с датой: пресеты DM/MY/DMY/Custom)
- [x] Кастомные пути для video/audio/txt/table (move/copy режимы)
- [x] Авто-очистка маркеров при загрузке видео
- [x] Импорт маркеров из JSON-файла
- [x] Иерархия установки: `installDir/app/ClipNotes.exe` (все файлы приложения в `app/`)
- [x] SHA256SUMS.txt для релизных артефактов
- [ ] Unit-тесты (не реализованы)
- [ ] Код-подпись exe (не настроена)
