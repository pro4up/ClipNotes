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
    [ObservableProperty] private int _currentTab; // 0=Setup, 1=Recording, 2=Review, 3=History

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

    // --- History ---
    public ObservableCollection<SessionHistoryEntry> SessionHistory { get; } = new();

    // --- Audio codec options ---
    public string[] AudioCodecOptions { get; } = { "wav", "mp3", "aac" };
    public string[] ModelOptions { get; } = { "large-v3-turbo", "large-v3" };
    public string[] LanguageOptions { get; } = { "auto", "ru", "en", "de", "fr", "es", "zh", "ja", "ko" };

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
        _obs.Error += msg => Application.Current.Dispatcher.Invoke(
            () => ObsStatus = $"Ошибка: {msg}");

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        LoadSettings();
        SetupToolPaths();
    }

    private void SetupToolPaths()
    {
        _ffmpeg.SetPaths(PathHelper.FFmpegPath, PathHelper.FFprobePath);
        _whisper.SetPaths(PathHelper.WhisperCliPath, PathHelper.GetModelPath(WhisperModel));
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
        foreach (var h in s.SessionHistory.OrderByDescending(x => x.CreatedAt).Take(20))
            SessionHistory.Add(h);
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
            Hotkeys = HotkeyBindings.Select(h => new HotkeyBindingData
            {
                Action = h.Action,
                Key = (int)h.Key,
                Modifiers = (int)h.Modifiers
            }).ToList(),
            SessionHistory = SessionHistory.ToList()
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
        GlossaryFilePath = GlossaryFilePath
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
            // Create session folder
            var sessionDir = _sessionService.CreateSessionFolder(OutputRootDirectory);
            _currentSession = new SessionData
            {
                SessionName = Path.GetFileName(sessionDir),
                SessionFolder = sessionDir,
                StartedAt = DateTime.Now,
                SettingsSnapshot = CreateSettingsSnapshot()
            };

            // Set OBS record directory to session/video
            var videoDir = Path.Combine(sessionDir, "video");
            await _obs.SetRecordDirectoryAsync(videoDir);

            // Start recording
            await _obs.StartRecordAsync();

            IsRecording = true;
            Markers.Clear();
            RecordingStatus = "Запись...";
            RecordingTime = "00:00:00";
            CurrentTab = 1;
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
                        outputPath, _currentSession.SessionFolder);
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
                CurrentTab = 2;
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Ошибка остановки: {ex.Message}";
        }
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
                Timecode = status.Value.timecode
            };

            Application.Current.Dispatcher.Invoke(() => Markers.Add(marker));
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
                Application.Current.Dispatcher.Invoke(() => ProcessingStatus = s);
            pipeline.ProgressChanged += (current, total) =>
                Application.Current.Dispatcher.Invoke(() =>
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
            ProcessingStatus = $"Ошибка: {ex.Message}";
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

    public void Cleanup()
    {
        _recordingTimer.Stop();
        _hotkeyService.Dispose();
        _obs.Dispose();
        SaveSettings();
    }
}
