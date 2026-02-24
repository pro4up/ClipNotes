using System.Windows.Input;
using ClipNotes.Helpers;
using ClipNotes.Models;
using ClipNotes.Services;
using LogSvc = ClipNotes.Services.LogService;
using Loc = ClipNotes.Helpers.LocalizationService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel
{
    partial void OnSelectedLanguageChanged(string value)
    {
        Loc.Load(value);
        foreach (var hk in HotkeyBindings)
            hk.NotifyLocalizationChanged();
        // Refresh status strings that are set in code
        if (!IsRecording && !IsProcessing)
        {
            if (!ObsConnected) ObsStatus = Loc.T("loc_StatusObsDisconnected");
            RecordingStatus = Loc.T("loc_StatusReady");
        }
        OnPropertyChanged(nameof(MarkersCountLabel));
        ValidateTools();
        SaveSettings();
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        App.ApplyTheme(value == 1);
        if (_settingsLoaded) _themeSetByUser = true;
        SaveSettings();
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
    partial void OnVideoPathCopyChanged(bool value) => SaveSettings();
    partial void OnAudioPathCopyChanged(bool value) => SaveSettings();
    partial void OnTxtPathCopyChanged(bool value) => SaveSettings();
    partial void OnTablePathCopyChanged(bool value) => SaveSettings();
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
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
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
                Modifiers = (System.Windows.Input.ModifierKeys)hk.Modifiers
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
        VideoPathCopy = s.VideoPathCopy;
        UseCustomAudioPath = s.UseCustomAudioPath;
        CustomAudioPath = s.CustomAudioPath;
        AudioPathCopy = s.AudioPathCopy;
        UseCustomTxtPath = s.UseCustomTxtPath;
        CustomTxtPath = s.CustomTxtPath;
        TxtPathCopy = s.TxtPathCopy;
        UseCustomTablePath = s.UseCustomTablePath;
        CustomTablePath = s.CustomTablePath;
        TablePathCopy = s.TablePathCopy;
        AskSessionName = s.AskSessionName;
        AppendDateSuffix = s.AppendDateSuffix;
        DateSuffixFormat = s.DateSuffixFormat;
        HoldPreSeconds = s.HoldPreSeconds;
        HoldPostSeconds = s.HoldPostSeconds;
        HoldModeEnabled = s.HoldModeEnabled; // last — triggers OnHoldModeEnabledChanged

        // Apply saved language (must be before status strings and theme)
        var lang = s.Language;
        if (!AvailableLanguages.Contains(lang)) lang = AvailableLanguages.FirstOrDefault() ?? "ru";
        Loc.Load(lang);
        SelectedLanguage = lang;

        // Apply theme: auto-detect from Windows on first launch, then use user's choice
        _themeSetByUser = s.ThemeSetByUser;
        SelectedThemeIndex = s.ThemeSetByUser
            ? (s.Theme is "Dark" or "Тёмная" ? 1 : 0)
            : DetectWindowsTheme();
        _settingsLoaded = true;

        // Init status strings after localization is loaded
        ObsStatus = Loc.T("loc_StatusObsDisconnected");
        RecordingStatus = Loc.T("loc_StatusReady");
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
            Theme = SelectedThemeIndex == 1 ? "Dark" : "Light",
            ThemeSetByUser = _themeSetByUser,
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
            VideoPathCopy = VideoPathCopy,
            UseCustomAudioPath = UseCustomAudioPath,
            CustomAudioPath = CustomAudioPath,
            AudioPathCopy = AudioPathCopy,
            UseCustomTxtPath = UseCustomTxtPath,
            CustomTxtPath = CustomTxtPath,
            TxtPathCopy = TxtPathCopy,
            UseCustomTablePath = UseCustomTablePath,
            CustomTablePath = CustomTablePath,
            TablePathCopy = TablePathCopy,
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
        VideoPathCopy = VideoPathCopy,
        UseCustomAudioPath = UseCustomAudioPath,
        CustomAudioPath = CustomAudioPath,
        AudioPathCopy = AudioPathCopy,
        UseCustomTxtPath = UseCustomTxtPath,
        CustomTxtPath = CustomTxtPath,
        TxtPathCopy = TxtPathCopy,
        UseCustomTablePath = UseCustomTablePath,
        CustomTablePath = CustomTablePath,
        TablePathCopy = TablePathCopy
    };
}
