using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClipNotes.Helpers;
using ClipNotes.Models;
using ClipNotes.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogSvc = ClipNotes.Services.LogService;
using Loc = ClipNotes.Helpers.LocalizationService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ObsWebSocketService _obs;
    private readonly FFmpegService _ffmpeg;
    private readonly WhisperService _whisper;
    private readonly ExcelService _excel;
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private readonly DispatcherTimer _recordingTimer;
    private CancellationTokenSource? _pipelineCts;

    // Current session
    private SessionData? _currentSession;

    // --- Navigation ---
    [ObservableProperty] private int _currentTab; // 0=Запись, 1=Экспорт, 2=История, 3=Настройки

    // --- Setup fields ---
    [ObservableProperty] private string _obsHost = "localhost";
    [ObservableProperty] private int _obsPort = 4455;
    [ObservableProperty] private string _obsPassword = "";
    [ObservableProperty] private bool _obsConnected;
    [ObservableProperty] private string _obsStatus = "Не подключено";
    [ObservableProperty] private string _outputRootDirectory = "";
    [ObservableProperty] private double _preSeconds = 5.0;
    [ObservableProperty] private double _postSeconds = 5.0;
    [ObservableProperty] private string _audioCodec = "wav";
    [ObservableProperty] private int _audioBitrate = 192;
    [ObservableProperty] private string _whisperModel = "large-v3-turbo";
    [ObservableProperty] private string _transcriptionLanguage = "auto";
    [ObservableProperty] private string _glossary = "";
    [ObservableProperty] private string? _glossaryFilePath;
    public ObservableCollection<HotkeyBinding> HotkeyBindings { get; } = new();

    // --- Custom output paths ---
    [ObservableProperty] private bool _useCustomVideoPath;
    [ObservableProperty] private string _customVideoPath = "";
    [ObservableProperty] private bool _useCustomAudioPath;
    [ObservableProperty] private string _customAudioPath = "";
    [ObservableProperty] private bool _useCustomTxtPath;
    [ObservableProperty] private string _customTxtPath = "";
    [ObservableProperty] private bool _useCustomTablePath;
    [ObservableProperty] private string _customTablePath = "";

    // --- Session naming ---
    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private bool _askSessionName = true;
    [ObservableProperty] private bool _appendDateSuffix = false;
    [ObservableProperty] private string _dateSuffixFormat = "{yyyy}.{MM}.{dd}";

    // --- Recording fields ---
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _recordingTime = "00:00:00";
    [ObservableProperty] private string _recordingStatus = "Готов к записи";
    public ObservableCollection<Marker> Markers { get; } = new();

    // --- Review fields ---
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _processingStatus = "";
    [ObservableProperty] private int _processingProgress;
    [ObservableProperty] private int _processingTotal = 1;
    [ObservableProperty] private bool _reviewReady;

    // --- Manual video loading (without OBS) ---
    [ObservableProperty] private string? _loadedVideoPath;
    [ObservableProperty] private string _manualMarkerTimecode = "00:00:30";
    [ObservableProperty] private MarkerType _manualMarkerType = MarkerType.Note;

    // --- OBS auto-start ---
    [ObservableProperty] private bool _autoStartObs;
    [ObservableProperty] private string _obsExePath = "";

    // --- Tray / startup ---
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimizeToTray;

    // --- Hold mode ---
    [ObservableProperty] private bool _holdModeEnabled;
    [ObservableProperty] private double _holdPreSeconds = 2.0;
    [ObservableProperty] private double _holdPostSeconds = 2.0;

    // Hold state (runtime)
    private TimeSpan? _holdStart;
    private MarkerType _holdMarkerType;

    // --- Tool validation ---
    [ObservableProperty] private string _toolsStatus = "";

    // --- History ---
    public ObservableCollection<SessionHistoryEntry> SessionHistory { get; } = new();
    [ObservableProperty] private int _maxHistoryCount = 20;
    [ObservableProperty] private bool _deleteFilesOnClear;
    [ObservableProperty] private bool _clearMarkersOnVideoLoad = true;

    // --- Theme ---
    [ObservableProperty] private string _selectedTheme = "Светлая";
    public string[] ThemeOptions { get; } = { "Светлая", "Тёмная" };

    // --- Language ---
    [ObservableProperty] private string _selectedLanguage = "ru";
    public List<string> AvailableLanguages { get; private set; } = [];

    partial void OnSelectedLanguageChanged(string value)
    {
        Loc.Load(value);
        // Refresh bindings that depend on localization
        foreach (var hk in HotkeyBindings)
            hk.NotifyLocalizationChanged();
        SaveSettings();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        App.ApplyTheme(value == "Тёмная");
        SaveSettings();
    }

    // --- Audio codec options ---
    public string[] AudioCodecOptions { get; } = { "wav", "mp3", "aac" };
    public ModelOption[] ModelOptions { get; } =
    {
        new("large-v3-turbo", "large-v3-turbo — ~6 GB VRAM"),
        new("large-v3",       "large-v3 — ~10 GB VRAM"),
        new("medium",         "medium — ~5 GB VRAM"),
        new("small",          "small — ~2 GB VRAM"),
        new("base",           "base — ~1 GB VRAM"),
    };
    public string[] LanguageOptions { get; } = { "auto", "ru", "en", "de", "fr", "es", "zh", "ja", "ko" };
    public MarkerType[] MarkerTypeOptions { get; } = { MarkerType.Bug, MarkerType.Task, MarkerType.Note, MarkerType.Summary };

    public MainViewModel()
    {
        _obs = new ObsWebSocketService();
        _ffmpeg = new FFmpegService();
        _whisper = new WhisperService();
        _excel = new ExcelService();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
        _hotkeyService = new HotkeyService();

        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _recordingTimer.Tick += OnRecordingTimerTick;

        _obs.RecordStateChanged += OnRecordStateChanged;
        _obs.RecordStopped += OnRecordStopped;
        _obs.Error += msg =>
        {
            LogSvc.Error($"OBS: {msg}");
            Application.Current?.Dispatcher.Invoke(() => ObsStatus = $"Ошибка: {msg}");
        };

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.HotkeyReleased += OnHotkeyReleased;

        AvailableLanguages = Loc.GetAvailableLanguages();
        LoadSettings();
        SetupToolPaths();
        ValidateTools();

        // Автозапуск OBS.exe если включено
        if (AutoStartObs && File.Exists(ObsExePath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ObsExePath) { UseShellExecute = true });

        // Авто-подключение OBS если настройки заполнены
        if (!string.IsNullOrWhiteSpace(ObsHost) && ObsPort > 0)
            Task.Run(() => _ = TestObsConnectionAsync());
    }

    private void SetupToolPaths()
    {
        _ffmpeg.SetPaths(PathHelper.FFmpegPath, PathHelper.FFprobePath);
        _whisper.SetPaths(PathHelper.WhisperCliPath, PathHelper.GetModelPath(WhisperModel));
    }

    private void ValidateTools()
    {
        var issues = new List<string>();
        if (!File.Exists(PathHelper.FFmpegPath)) issues.Add("ffmpeg.exe не найден");
        if (!File.Exists(PathHelper.FFprobePath)) issues.Add("ffprobe.exe не найден");
        if (!File.Exists(PathHelper.WhisperCliPath)) issues.Add("whisper-cli.exe не найден");

        var modelPath = PathHelper.GetModelPath(WhisperModel);
        if (!File.Exists(modelPath))
            issues.Add($"Модель '{WhisperModel}' не найдена — выберите другую модель в настройках");

        ToolsStatus = issues.Count == 0
            ? $"Инструменты: OK | Модель: {WhisperModel}"
            : "⚠ " + string.Join("; ", issues);
    }

    partial void OnWhisperModelChanged(string value)
    {
        SetupToolPaths();
        ValidateTools();
    }

    partial void OnAutoStartObsChanged(bool value) => SaveSettings();

    partial void OnMinimizeToTrayChanged(bool value) => SaveSettings();

    partial void OnClearMarkersOnVideoLoadChanged(bool value) => SaveSettings();

    partial void OnHoldModeEnabledChanged(bool value)
    {
        if (value) _hotkeyService.EnableHoldMode();
        else _hotkeyService.DisableHoldMode();
        SaveSettings();
    }

    partial void OnHoldPreSecondsChanged(double value) => SaveSettings();
    partial void OnHoldPostSecondsChanged(double value) => SaveSettings();

    partial void OnUseCustomVideoPathChanged(bool value) => SaveSettings();
    partial void OnCustomVideoPathChanged(string value) => SaveSettings();
    partial void OnUseCustomAudioPathChanged(bool value) => SaveSettings();
    partial void OnCustomAudioPathChanged(string value) => SaveSettings();
    partial void OnUseCustomTxtPathChanged(bool value) => SaveSettings();
    partial void OnCustomTxtPathChanged(string value) => SaveSettings();
    partial void OnUseCustomTablePathChanged(bool value) => SaveSettings();
    partial void OnCustomTablePathChanged(string value) => SaveSettings();

    partial void OnAskSessionNameChanged(bool value) => SaveSettings();
    partial void OnAppendDateSuffixChanged(bool value) => SaveSettings();
    partial void OnDateSuffixFormatChanged(string value) => SaveSettings();

    partial void OnStartWithWindowsChanged(bool value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (value)
                key?.SetValue("ClipNotes", $"\"{exePath}\" --tray");
            else
                key?.DeleteValue("ClipNotes", false);
        }
        catch (Exception ex) { LogSvc.Warn($"Registry error: {ex.Message}"); }
        SaveSettings();
    }

    private void LoadSettings()
    {
        var s = _settingsService.Load();
        ObsHost = s.ObsHost;
        ObsPort = s.ObsPort;
        ObsPassword = s.ObsPassword ?? "";
        OutputRootDirectory = s.OutputRootDirectory;
        PreSeconds = s.PreSeconds;
        PostSeconds = s.PostSeconds;
        AudioCodec = s.AudioCodec;
        AudioBitrate = s.AudioBitrate;
        WhisperModel = s.WhisperModel;
        TranscriptionLanguage = s.TranscriptionLanguage;
        Glossary = s.Glossary;
        GlossaryFilePath = s.GlossaryFilePath;

        HotkeyBindings.Clear();
        foreach (var hk in s.Hotkeys)
        {
            HotkeyBindings.Add(new HotkeyBinding
            {
                Action = hk.Action,
                Key = (Key)hk.Key,
                Modifiers = (ModifierKeys)hk.Modifiers
            });
        }

        // Ensure all actions have bindings
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (!HotkeyBindings.Any(h => h.Action == action))
                HotkeyBindings.Add(new HotkeyBinding { Action = action });
        }

        SessionHistory.Clear();
        MaxHistoryCount = s.MaxHistoryCount;
        foreach (var h in s.SessionHistory.OrderByDescending(x => x.CreatedAt).Take(MaxHistoryCount))
            SessionHistory.Add(h);

        AutoStartObs = s.AutoStartObs;
        ObsExePath = s.ObsExePath;
        StartWithWindows = s.StartWithWindows;
        MinimizeToTray = s.MinimizeToTray;
        DeleteFilesOnClear = s.DeleteFilesOnClear;
        ClearMarkersOnVideoLoad = s.ClearMarkersOnVideoLoad;
        UseCustomVideoPath = s.UseCustomVideoPath;
        CustomVideoPath = s.CustomVideoPath;
        UseCustomAudioPath = s.UseCustomAudioPath;
        CustomAudioPath = s.CustomAudioPath;
        UseCustomTxtPath = s.UseCustomTxtPath;
        CustomTxtPath = s.CustomTxtPath;
        UseCustomTablePath = s.UseCustomTablePath;
        CustomTablePath = s.CustomTablePath;
        AskSessionName = s.AskSessionName;
        AppendDateSuffix = s.AppendDateSuffix;
        DateSuffixFormat = s.DateSuffixFormat;
        HoldPreSeconds = s.HoldPreSeconds;
        HoldPostSeconds = s.HoldPostSeconds;
        HoldModeEnabled = s.HoldModeEnabled; // last — triggers OnHoldModeEnabledChanged

        // Apply saved theme
        SelectedTheme = s.Theme; // calls OnSelectedThemeChanged → ApplyTheme

        // Apply saved language (must be after AvailableLanguages is set)
        var lang = s.Language;
        if (!AvailableLanguages.Contains(lang)) lang = AvailableLanguages.FirstOrDefault() ?? "ru";
        Loc.Load(lang);
        SelectedLanguage = lang;
    }

    public void SaveSettings()
    {
        var s = new AppSettings
        {
            ObsHost = ObsHost,
            ObsPort = ObsPort,
            ObsPassword = string.IsNullOrEmpty(ObsPassword) ? null : ObsPassword,
            OutputRootDirectory = OutputRootDirectory,
            PreSeconds = PreSeconds,
            PostSeconds = PostSeconds,
            AudioCodec = AudioCodec,
            AudioBitrate = AudioBitrate,
            WhisperModel = WhisperModel,
            TranscriptionLanguage = TranscriptionLanguage,
            Glossary = Glossary,
            GlossaryFilePath = GlossaryFilePath,
            AutoStartObs = AutoStartObs,
            ObsExePath = ObsExePath,
            StartWithWindows = StartWithWindows,
            MinimizeToTray = MinimizeToTray,
            Theme = SelectedTheme,
            Hotkeys = HotkeyBindings.Select(h => new HotkeyBindingData
            {
                Action = h.Action,
                Key = (int)h.Key,
                Modifiers = (int)h.Modifiers
            }).ToList(),
            MaxHistoryCount = MaxHistoryCount,
            DeleteFilesOnClear = DeleteFilesOnClear,
            ClearMarkersOnVideoLoad = ClearMarkersOnVideoLoad,
            UseCustomVideoPath = UseCustomVideoPath,
            CustomVideoPath = CustomVideoPath,
            UseCustomAudioPath = UseCustomAudioPath,
            CustomAudioPath = CustomAudioPath,
            UseCustomTxtPath = UseCustomTxtPath,
            CustomTxtPath = CustomTxtPath,
            UseCustomTablePath = UseCustomTablePath,
            CustomTablePath = CustomTablePath,
            AskSessionName = AskSessionName,
            AppendDateSuffix = AppendDateSuffix,
            DateSuffixFormat = DateSuffixFormat,
            HoldModeEnabled = HoldModeEnabled,
            HoldPreSeconds = HoldPreSeconds,
            HoldPostSeconds = HoldPostSeconds,
            SessionHistory = SessionHistory.ToList(),
            Language = SelectedLanguage
        };
        _settingsService.Save(s);
    }

    private AppSettings CreateSettingsSnapshot() => new()
    {
        ObsHost = ObsHost,
        ObsPort = ObsPort,
        OutputRootDirectory = OutputRootDirectory,
        PreSeconds = PreSeconds,
        PostSeconds = PostSeconds,
        AudioCodec = AudioCodec,
        AudioBitrate = AudioBitrate,
        WhisperModel = WhisperModel,
        TranscriptionLanguage = TranscriptionLanguage,
        Glossary = Glossary,
        GlossaryFilePath = GlossaryFilePath,
        HoldModeEnabled = HoldModeEnabled,
        HoldPreSeconds = HoldPreSeconds,
        HoldPostSeconds = HoldPostSeconds,
        UseCustomVideoPath = UseCustomVideoPath,
        CustomVideoPath = CustomVideoPath,
        UseCustomAudioPath = UseCustomAudioPath,
        CustomAudioPath = CustomAudioPath,
        UseCustomTxtPath = UseCustomTxtPath,
        CustomTxtPath = CustomTxtPath,
        UseCustomTablePath = UseCustomTablePath,
        CustomTablePath = CustomTablePath
    };

    public void InitializeHotkeys(Window window)
    {
        _hotkeyService.Initialize(window);
        _hotkeyService.RegisterHotkeys(HotkeyBindings);
    }

    public void RefreshHotkeys()
    {
        _hotkeyService.RegisterHotkeys(HotkeyBindings);
    }

    // --- Commands ---

    [RelayCommand]
    private async Task TestObsConnectionAsync()
    {
        ObsStatus = "Подключение...";
        var pw = string.IsNullOrEmpty(ObsPassword) ? null : ObsPassword;
        var ok = await _obs.ConnectAsync(ObsHost, ObsPort, pw);
        ObsConnected = ok;
        ObsStatus = ok ? "Подключено к OBS" : "Не удалось подключиться";
        if (ok) SaveSettings();
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выберите корневую папку для сессий"
        };
        if (dialog.ShowDialog() == true)
        {
            OutputRootDirectory = dialog.FolderName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void BrowseGlossaryFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите файл глоссария",
            Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            GlossaryFilePath = dialog.FileName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        if (DeleteFilesOnClear)
        {
            foreach (var entry in SessionHistory)
            {
                try
                {
                    if (Directory.Exists(entry.FolderPath))
                        Directory.Delete(entry.FolderPath, true);
                }
                catch (Exception ex) { LogSvc.Warn($"ClearHistory delete failed: {ex.Message}"); }
            }
        }
        SessionHistory.Clear();
        SaveSettings();
    }

    [RelayCommand]
    private void BrowseObsExe()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите OBS Studio exe",
            Filter = "OBS Studio (obs64.exe;obs32.exe)|obs64.exe;obs32.exe|Исполняемые файлы (*.exe)|*.exe"
        };
        if (dialog.ShowDialog() == true)
        {
            ObsExePath = dialog.FileName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        if (!ObsConnected)
        {
            RecordingStatus = "Сначала подключитесь к OBS";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputRootDirectory))
        {
            RecordingStatus = "Укажите выходную директорию";
            return;
        }

        try
        {
            // Resolve session name
            var nameToUse = SessionName.Trim();
            if (string.IsNullOrEmpty(nameToUse) && AskSessionName)
            {
                var input = InputDialog.Show("Имя записи", "Введите имя для этой записи (или оставьте пустым):", "");
                if (input == null) return; // user cancelled
                nameToUse = input.Trim();
            }

            var now = DateTime.Now;
            var videoName = BuildVideoName(nameToUse, now);

            // Create session folder
            var sessionDir = _sessionService.CreateSessionFolder(OutputRootDirectory, string.IsNullOrEmpty(nameToUse) ? null : nameToUse);
            _currentSession = new SessionData
            {
                SessionName = Path.GetFileName(sessionDir),
                SessionFolder = sessionDir,
                StartedAt = now,
                SettingsSnapshot = CreateSettingsSnapshot()
            };
            _currentSession.SettingsSnapshot.SessionVideoName = videoName;
            SetEffectiveDirs(_currentSession);

            // Set OBS record directory to effective video dir
            Directory.CreateDirectory(_currentSession.EffectiveVideoDir);
            await _obs.SetRecordDirectoryAsync(_currentSession.EffectiveVideoDir);

            // Start recording
            await _obs.StartRecordAsync();

            IsRecording = true;
            Markers.Clear();
            RecordingStatus = "Запись...";
            RecordingTime = "00:00:00";
            CurrentTab = 0; // Запись
            _recordingTimer.Start();
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (!IsRecording) return;

        try
        {
            RecordingStatus = "Остановка записи...";
            var outputPath = await _obs.StopRecordAsync();
            _recordingTimer.Stop();
            IsRecording = false;

            if (_currentSession != null)
            {
                _currentSession.StoppedAt = DateTime.Now;
                _currentSession.ObsOutputPath = outputPath;
                _currentSession.Markers = Markers.ToList();

                // Move video file
                if (!string.IsNullOrEmpty(outputPath))
                {
                    RecordingStatus = "Перемещение видеофайла...";
                    var videoPath = await _sessionService.MoveVideoToSession(
                        outputPath, _currentSession.SessionFolder,
                        _currentSession.SettingsSnapshot.SessionVideoName,
                        _currentSession.EffectiveVideoDir);
                    _currentSession.VideoFilePath = videoPath;
                }

                // Get duration
                if (_currentSession.VideoFilePath != null)
                {
                    var dur = await _ffmpeg.GetDurationAsync(_currentSession.VideoFilePath);
                    _currentSession.Duration = dur;
                }

                _sessionService.SaveSessionMeta(_currentSession);

                // Add to history
                var entry = new SessionHistoryEntry
                {
                    SessionName = _currentSession.SessionName,
                    FolderPath = _currentSession.SessionFolder,
                    CreatedAt = _currentSession.StartedAt,
                    MarkerCount = _currentSession.Markers.Count
                };
                SessionHistory.Insert(0, entry);
                SaveSettings();

                RecordingStatus = "Запись завершена";
                ReviewReady = true;
                CurrentTab = 1; // Экспорт
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка остановки: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadExistingVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputRootDirectory))
        {
            ProcessingStatus = "Сначала укажите выходную директорию в настройках";
            CurrentTab = 3; // Настройки
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите видеофайл для генерации",
            Filter = "Видеофайлы (*.mkv;*.mp4;*.avi;*.mov;*.flv;*.wmv)|*.mkv;*.mp4;*.avi;*.mov;*.flv;*.wmv|Все файлы (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sessionDir = _sessionService.CreateSessionFolder(OutputRootDirectory);
            _currentSession = new SessionData
            {
                SessionName = Path.GetFileName(sessionDir),
                SessionFolder = sessionDir,
                StartedAt = DateTime.Now,
                SettingsSnapshot = CreateSettingsSnapshot(),
                VideoFilePath = dialog.FileName
            };
            SetEffectiveDirs(_currentSession);

            SetupToolPaths();
            ValidateTools();
            var dur = await _ffmpeg.GetDurationAsync(dialog.FileName);
            _currentSession.Duration = dur;

            LoadedVideoPath = dialog.FileName;
            if (ClearMarkersOnVideoLoad)
                Markers.Clear();
            ReviewReady = true;
            ProcessingStatus = $"Видео загружено: {Path.GetFileName(dialog.FileName)} ({dur:hh\\:mm\\:ss})";
            CurrentTab = 1; // Экспорт
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"Ошибка загрузки: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddManualMarker()
    {
        if (_currentSession == null)
        {
            ProcessingStatus = "Сначала загрузите видео (кнопка «Обзор» выше)";
            return;
        }

        if (!TimeSpan.TryParseExact(ManualMarkerTimecode, @"hh\:mm\:ss", null, out var ts) &&
            !TimeSpan.TryParseExact(ManualMarkerTimecode, @"h\:mm\:ss", null, out ts) &&
            !TimeSpan.TryParseExact(ManualMarkerTimecode, @"mm\:ss", null, out ts) &&
            !TimeSpan.TryParse(ManualMarkerTimecode, out ts))
        {
            ProcessingStatus = "Неверный формат тайм-кода. Используйте ЧЧ:ММ:СС";
            return;
        }

        var marker = new Marker
        {
            Index = Markers.Count + 1,
            Type = ManualMarkerType,
            Timestamp = ts,
            Timecode = ts.ToString(@"hh\:mm\:ss")
        };

        Markers.Add(marker);
        ManualMarkerTimecode = (ts + TimeSpan.FromSeconds(30)).ToString(@"hh\:mm\:ss");
        SaveMarkersToSession();
    }

    [RelayCommand]
    private void RemoveMarker(Marker? marker)
    {
        if (marker == null) return;
        Markers.Remove(marker);
        // Re-index
        for (int i = 0; i < Markers.Count; i++)
            Markers[i].Index = i + 1;
        SaveMarkersToSession();
    }

    [RelayCommand]
    private void ImportMarkers()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Импорт маркеров",
            Filter = "Файлы маркеров (*.json)|*.json|Все файлы (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var file = _sessionService.LoadMarkersFile(dialog.FileName);
        if (file == null || file.Markers.Count == 0)
        {
            ProcessingStatus = "Файл маркеров пуст или повреждён";
            return;
        }

        foreach (var entry in file.Markers)
        {
            if (!Enum.TryParse<MarkerType>(entry.Type, true, out var markerType)) continue;
            if (!TimeSpan.TryParse(entry.Timestamp, out var ts)) continue;

            TimeSpan? holdDuration = null;
            if (!string.IsNullOrEmpty(entry.HoldDuration) && TimeSpan.TryParse(entry.HoldDuration, out var hd))
                holdDuration = hd;

            Markers.Add(new Marker
            {
                Index = Markers.Count + 1,
                Type = markerType,
                Timestamp = ts,
                Timecode = ts.ToString(@"hh\:mm\:ss"),
                Text = entry.Text,
                GenerateAudio = entry.GenerateAudio,
                GenerateText = entry.GenerateText,
                HoldDuration = holdDuration
            });
        }
        ProcessingStatus = $"Импортировано маркеров: {file.Markers.Count}";
        SaveMarkersToSession();
    }

    private void SetEffectiveDirs(SessionData session)
    {
        var s = session.SettingsSnapshot;
        var sf = session.SessionFolder;
        session.EffectiveVideoDir = s.UseCustomVideoPath && !string.IsNullOrWhiteSpace(s.CustomVideoPath)
            ? s.CustomVideoPath : Path.Combine(sf, "video");
        session.EffectiveAudioDir = s.UseCustomAudioPath && !string.IsNullOrWhiteSpace(s.CustomAudioPath)
            ? s.CustomAudioPath : Path.Combine(sf, "audio");
        session.EffectiveTxtDir = s.UseCustomTxtPath && !string.IsNullOrWhiteSpace(s.CustomTxtPath)
            ? s.CustomTxtPath : Path.Combine(sf, "txt");
        session.EffectiveTableDir = s.UseCustomTablePath && !string.IsNullOrWhiteSpace(s.CustomTablePath)
            ? s.CustomTablePath : Path.Combine(sf, "table");
    }

    private string BuildVideoName(string baseName, DateTime dt)
    {
        if (!AppendDateSuffix || string.IsNullOrWhiteSpace(DateSuffixFormat))
            return baseName;
        var suffix = DateSuffixFormat
            .Replace("{yyyy}", dt.Year.ToString("D4"))
            .Replace("{yy}", dt.ToString("yy"))
            .Replace("{MM}", dt.Month.ToString("D2"))
            .Replace("{dd}", dt.Day.ToString("D2"))
            .Replace("{HH}", dt.Hour.ToString("D2"))
            .Replace("{mm}", dt.Minute.ToString("D2"))
            .Replace("{ss}", dt.Second.ToString("D2"));
        return string.IsNullOrWhiteSpace(baseName) ? suffix : $"{baseName} {suffix}";
    }

    private void SaveMarkersToSession()
    {
        if (_currentSession == null) return;
        _sessionService.SaveMarkersFile(_currentSession.SessionFolder, Markers);
    }

    [RelayCommand]
    private async Task AddMarkerAsync(MarkerType type)
    {
        if (!IsRecording || _currentSession == null) return;

        try
        {
            var status = await _obs.GetRecordStatusAsync();
            if (status == null) return;

            var marker = new Marker
            {
                Index = Markers.Count + 1,
                Type = type,
                Timestamp = status.Value.duration,
                Timecode = status.Value.timecode.Length > 8
                    ? status.Value.timecode[..8]
                    : status.Value.timecode
            };

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Markers.Add(marker);
                SaveMarkersToSession();
            });
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка маркера: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (_currentSession == null) return;

        IsProcessing = true;
        ProcessingStatus = "Начало обработки...";
        ProcessingProgress = 0;
        _pipelineCts = new CancellationTokenSource();

        try
        {
            SetupToolPaths();
            _currentSession.Markers = Markers.ToList();

            var pipeline = new PipelineService(_ffmpeg, _whisper, _excel);
            pipeline.StatusChanged += s =>
                Application.Current?.Dispatcher.Invoke(() => ProcessingStatus = s);
            pipeline.ProgressChanged += (current, total) =>
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProcessingProgress = current;
                    ProcessingTotal = total;
                });

            await pipeline.RunAsync(_currentSession, _pipelineCts.Token);

            ProcessingStatus = "Генерация завершена!";
        }
        catch (OperationCanceledException)
        {
            ProcessingStatus = "Отменено";
        }
        catch (Exception ex)
        {
            LogSvc.Error("Generation pipeline failed", ex);
            // Show brief message; full details in Logs/
            var msg = ex.Message.Length > 300 ? ex.Message[..300] + "…" : ex.Message;
            ProcessingStatus = $"Ошибка: {msg}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void CancelGeneration()
    {
        _pipelineCts?.Cancel();
    }

    [RelayCommand]
    private void OpenSessionFolder()
    {
        var folder = _currentSession?.SessionFolder;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            Process.Start("explorer.exe", folder);
    }

    [RelayCommand]
    private void OpenHistoryFolder(string? folderPath)
    {
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            Process.Start("explorer.exe", folderPath);
    }

    private async void OnRecordingTimerTick(object? sender, EventArgs e)
    {
        if (!IsRecording) return;
        try
        {
            var status = await _obs.GetRecordStatusAsync();
            if (status != null)
                RecordingTime = status.Value.timecode.Length > 8
                    ? status.Value.timecode[..8]
                    : status.Value.timecode;
        }
        catch { }
    }

    private void OnRecordStateChanged(string state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            RecordingStatus = state switch
            {
                "OBS_WEBSOCKET_OUTPUT_STARTING" => "Запись начинается...",
                "OBS_WEBSOCKET_OUTPUT_STARTED" => "Запись...",
                "OBS_WEBSOCKET_OUTPUT_STOPPING" => "Запись останавливается...",
                "OBS_WEBSOCKET_OUTPUT_STOPPED" => "Запись остановлена",
                _ => state
            };
        });
    }

    private void OnRecordStopped(string outputPath)
    {
        // Handled via StopRecordingAsync
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            var isMarkerAction = action is HotkeyAction.MarkerBug or HotkeyAction.MarkerTask or HotkeyAction.MarkerNote or HotkeyAction.MarkerSummary;

            if (isMarkerAction && HoldModeEnabled && IsRecording && _currentSession != null)
            {
                // Hold mode: record start timestamp
                var status = await _obs.GetRecordStatusAsync();
                if (status == null) return;
                _holdStart = status.Value.duration;
                _holdMarkerType = action switch
                {
                    HotkeyAction.MarkerBug => MarkerType.Bug,
                    HotkeyAction.MarkerTask => MarkerType.Task,
                    HotkeyAction.MarkerSummary => MarkerType.Summary,
                    _ => MarkerType.Note
                };
                RecordingStatus = $"Удержание... {_holdMarkerType} @ {_holdStart:hh\\:mm\\:ss}";
                return;
            }

            switch (action)
            {
                case HotkeyAction.MarkerBug:
                    await AddMarkerAsync(MarkerType.Bug);
                    break;
                case HotkeyAction.MarkerTask:
                    await AddMarkerAsync(MarkerType.Task);
                    break;
                case HotkeyAction.MarkerNote:
                    await AddMarkerAsync(MarkerType.Note);
                    break;
                case HotkeyAction.MarkerSummary:
                    await AddMarkerAsync(MarkerType.Summary);
                    break;
                case HotkeyAction.StartRecording:
                    await StartRecordingAsync();
                    break;
                case HotkeyAction.StopRecording:
                    await StopRecordingAsync();
                    break;
                case HotkeyAction.Generate:
                    await GenerateAsync();
                    break;
                case HotkeyAction.OpenOutputFolder:
                    OpenSessionFolder();
                    break;
            }
        });
    }

    private void OnHotkeyReleased(HotkeyAction action)
    {
        if (!HoldModeEnabled || !IsRecording || _currentSession == null || _holdStart == null) return;

        Application.Current.Dispatcher.Invoke(async () =>
        {
            try
            {
                var status = await _obs.GetRecordStatusAsync();
                if (status == null) return;

                var holdEnd = status.Value.duration;
                var holdDuration = holdEnd - _holdStart.Value;
                if (holdDuration < TimeSpan.Zero) holdDuration = TimeSpan.Zero;

                var marker = new Marker
                {
                    Index = Markers.Count + 1,
                    Type = _holdMarkerType,
                    Timestamp = _holdStart.Value,
                    Timecode = _holdStart.Value.ToString(@"hh\:mm\:ss"),
                    HoldDuration = holdDuration
                };

                Markers.Add(marker);
                SaveMarkersToSession();
                RecordingStatus = $"Маркер [{_holdMarkerType}] {marker.TimestampFormatted} ({holdDuration:mm\\:ss})";
            }
            finally
            {
                _holdStart = null;
            }
        });
    }

    [RelayCommand]
    private void BrowseCustomPath(string which)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Выберите папку" };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        switch (which)
        {
            case "video": CustomVideoPath = dialog.SelectedPath; break;
            case "audio": CustomAudioPath = dialog.SelectedPath; break;
            case "txt":   CustomTxtPath   = dialog.SelectedPath; break;
            case "table": CustomTablePath = dialog.SelectedPath; break;
        }
    }

    public void Cleanup()
    {
        _recordingTimer.Stop();
        _hotkeyService.Dispose();
        _obs.Dispose();
        SaveSettings();
    }
}

public record ModelOption(string Value, string Display)
{
    public override string ToString() => Display;
    public string ModelName => Display.Contains(" — ") ? Display[..Display.IndexOf(" — ")] : Display;
    public string VramHint  => Display.Contains(" — ") ? Display[(Display.IndexOf(" — ") + 3)..] : "";
}
