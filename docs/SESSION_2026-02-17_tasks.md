# Задачи для следующей сессии — 2026-02-17

Статус: ОЖИДАЮТ РЕАЛИЗАЦИИ
Приоритет: выполнять в порядке нумерации (зависимости ↓).

---

## 1. Тёмная тема — исправить визуальные дефекты

**Файлы:** `App.xaml`, `App.xaml.cs`, `Views/MainWindow.xaml`

**Проблемы (все в тёмной теме):**
- Белые фоны (border, layouts, separators) — не перешли на тёмные кисти
- Чекбоксы: чёрный текст, белый фон → нужен светлый текст, тёмный фон, светлая галочка
- Активная вкладка Tab: ярко-белая — сделать светло-серой (~#E0E0E0)
- Все вкладки: белая обводка-линия у TabControl → убрать/сделать тёмной
- Кнопки ✕ (Delete), типы маркеров (Bug/Task/Note), кнопка Generate: слишком яркие цвета → приглушить яркость/насыщенность на 20-30%
- ComboBox / SelectBox: белый фон, чёрный текст → тёмный фон, светлый текст
- Scrollbar: белый → тёмный (`ScrollViewer` + стиль `ScrollBar` в `App.xaml`)

**Подход:**
1. В `App.xaml` добавить явные стили для `CheckBox`, `ScrollBar`, `TabControl` с `DynamicResource`
2. В `App.xaml.cs → ApplyTheme(dark)` добавить кисти для фона чекбоксов, scrollbar
3. Цвета кнопок Bug/Task/Note — приглушить в XAML (opacity или HSL)
4. Проверить после каждого изменения через Build + запуск с `"Theme":"Тёмная"` в settings.json

---

## 2. Порядок вкладок

**Файл:** `Views/MainWindow.xaml`

Текущий порядок: Настройки | Запись | Экспорт | История
Новый порядок: **Запись | Экспорт | История | Настройки**

Просто переставить `<TabItem>` блоки в XAML. `CurrentTab` — индекс, обновить все места где он устанавливается в `MainViewModel.cs` (StartRecording → 0, StopRecording → 1, LoadExistingVideo → 1).

---

## 3. Авто-проверка OBS при запуске

**Файлы:** `ViewModels/MainViewModel.cs`

Если `ObsHost` и `ObsPort` непустые — вызывать `TestObsConnectionAsync()` в конструкторе (после `LoadSettings()`).
Обернуть в `Task.Run(() => _ = TestObsConnectionAsync())` чтобы не блокировать UI.

---

## 4. Проверить логирование

**Файл:** `Services/LogService.cs`

Подозрение: LogService пишет в `AppDir/Logs/`, но `Assembly.GetExecutingAssembly().Location` может вернуть пустой путь при single-file publish.

**Диагностика:**
```csharp
// В LogService.Write добавить fallback:
var appDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? ".";
var LogDir = Path.Combine(appDir, "Logs");
```
Заменить `Assembly.GetExecutingAssembly().Location` на `Process.GetCurrentProcess().MainModule?.FileName`.
После изменения: пересобрать, запустить, намеренно вызвать ошибку, проверить файл `app/Logs/YYYY-MM-DD.log`.

---

## 5. Оптимизация обработки маркеров

**Цель:** 60-мин видео за ~6-8 минут транскрипции.

**Текущий bottleneck:** Whisper обрабатывает каждый маркер ОТДЕЛЬНО из полного master.wav (параметр `--offset-t`). На короткие клипы (10с) тратится много времени на инициализацию модели.

**Решение — передавать уже нарезанный WAV-клип напрямую в whisper:**
- `WhisperService.TranscribeSegmentAsync` сейчас принимает `masterAudioPath` + offset/duration
- Изменить: передавать `audioClipPath` (уже нарезанный файл), убрать `--offset-t` и `--duration`
- В `PipelineService.RunAsync`: сначала нарезать аудиоклип (`ExtractAudioClipAsync`), затем транскрибировать его напрямую

**Изменения:**
- `WhisperService.TranscribeSegmentAsync(string audioClipPath, ...)` — убрать offset/duration параметры
- Внутри: убрать `--offset-t` и `--duration` из args
- `PipelineService`: сначала создать audioClip (обязательно, даже если `marker.GenerateAudio == false`), затем транскрибировать его, после — удалить временный файл если `GenerateAudio == false`

**Дополнительно:** параллельная обработка маркеров через `Parallel.ForEachAsync` (осторожно: whisper-cli — одиночный процесс, GPU не делится, лучше последовательно).

**Проверка:** замерить время на тестовом видео до и после.

---

## 6. Whisper модели — добавить VRAM

**Файл:** `ViewModels/MainViewModel.cs` и/или `Views/MainWindow.xaml`

Заменить `ModelOptions` с просто названий на отображаемые строки с пометкой VRAM:

| Значение (ключ) | Отображение |
|---|---|
| base | base — ~1 GB VRAM |
| small | small — ~2 GB VRAM |
| medium | medium — ~5 GB VRAM |
| large-v3 | large-v3 — ~10 GB VRAM |
| large-v3-turbo | large-v3-turbo — ~6 GB VRAM |

Подход: отдельные классы `ModelOption { Value, Display }`, ComboBox показывает `Display`, сохраняется `Value`.
Или проще: отдельный `string[]` для display и mapping dictionary.

---

## 7. Тёмная тема — Scrollbar

Входит в п.1, вынесен отдельно для акцента.

В `App.xaml` добавить стиль `<Style TargetType="ScrollBar">` и `<Style TargetType="ScrollViewer">` с `DynamicResource` для цвета трека и ползунка.
В `ApplyTheme(dark)`: добавить кисти `ScrollBarThumbBrush`, `ScrollBarTrackBrush`.

---

## 8. Автозапуск OBS

**Файлы:** `Models/AppSettings.cs`, `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml`

**Добавить в AppSettings:**
```csharp
public bool AutoStartObs { get; set; } = false;
public string ObsExePath { get; set; } = "";
```

**В MainViewModel:**
- `SelectedAutoStartObs` (bool ObservableProperty)
- `ObsExePath` (string ObservableProperty)
- В конструкторе после `LoadSettings()`: если `AutoStartObs && File.Exists(ObsExePath)` → `Process.Start(ObsExePath)`

**В XAML (Settings tab):** добавить CheckBox "Автозапуск OBS" и TextBox/Browse для пути к OBS.exe.

---

## 9. Автозапуск Windows + трей

**Файлы:** `Models/AppSettings.cs`, `ViewModels/MainViewModel.cs`, `App.xaml.cs`, `Views/MainWindow.xaml`

**Добавить в AppSettings:**
```csharp
public bool StartWithWindows { get; set; } = false;
public bool MinimizeToTray { get; set; } = false;
```

**Автозапуск Windows** — через реестр:
```csharp
// Add/remove registry key
using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
if (enable) key.SetValue("ClipNotes", $"\"{exePath}\" --tray");
else key.DeleteValue("ClipNotes", false);
```

**Трей:** добавить `System.Windows.Forms.NotifyIcon` в `App.xaml.cs`:
- При сворачивании (`MainWindow.StateChanged`) → `Hide()` + показать иконку трея
- По двойному клику на иконке → `Show()` + `WindowState = Normal`
- Контекстное меню трея: "Открыть", "Выход"

При запуске с аргументом `--tray` → запустить скрытым.

---

## 10. Улучшение дизайна (Apple-style)

**Файлы:** `App.xaml`, `Views/MainWindow.xaml`

**ComboBox:** стилизовать в Apple-style с скруглёнными углами, тонкой границей, плавный dropdown.
**Вкладки Tab:** убрать рамку TabControl, сделать как pill-navigation (горизонтальные кнопки с закруглёнными краями).
**Общий стиль:** убрать все жёсткие рамки, использовать тени (через `Effect`), увеличить padding, использовать SF Pro / Segoe UI Variable.
**Анимации:** добавить `<Style.Triggers><EventTrigger RoutedEvent="...">` с `DoubleAnimation` для opacity при наведении.

**Подход:** Сначала переработать стиль ComboBox в `App.xaml` (изолированное изменение), проверить, затем вкладки.

---

## Порядок выполнения (рекомендуемый)

```
П.4 (Логирование — быстро, диагностика) →
П.2 (Порядок вкладок — одно место) →
П.3 (Авто-OBS подключение — несколько строк) →
П.8 (Автозапуск OBS exe) →
П.6 (VRAM в моделях) →
П.5 (Оптимизация Whisper) →
П.1 + П.7 (Тёмная тема — большой блок) →
П.9 (Трей + автозапуск Windows) →
П.10 (Дизайн — самый объёмный)
```

После каждого пункта: `dotnet build` (без ошибок) + краткий тест в UI.
