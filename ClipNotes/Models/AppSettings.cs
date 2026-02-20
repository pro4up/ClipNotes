using System.Windows.Input;

namespace ClipNotes.Models;

public class AppSettings
{
    // OBS Connection
    public string ObsHost { get; set; } = "localhost";
    public int ObsPort { get; set; } = 4455;
    public string? ObsPassword { get; set; }

    // Output
    public string OutputRootDirectory { get; set; } = "";

    // Audio
    public double PreSeconds { get; set; } = 5.0;
    public double PostSeconds { get; set; } = 5.0;
    public string AudioCodec { get; set; } = "wav"; // wav, mp3, aac
    public int AudioBitrate { get; set; } = 192; // kbps for mp3/aac

    // Transcription
    public string WhisperModel { get; set; } = "large-v3-turbo";
    public string TranscriptionLanguage { get; set; } = "auto";
    public string Glossary { get; set; } = "";
    public string? GlossaryFilePath { get; set; }

    // Hotkeys
    public List<HotkeyBindingData> Hotkeys { get; set; } = GetDefaultHotkeys();

    // OBS auto-start
    public bool AutoStartObs { get; set; } = false;
    public string ObsExePath { get; set; } = "";

    // Tray / startup
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;

    // Theme
    public string Theme { get; set; } = "Светлая";
    public bool ThemeSetByUser { get; set; } = false;

    // Custom output paths per subdirectory
    public bool UseCustomVideoPath { get; set; } = false;
    public string CustomVideoPath { get; set; } = "";
    public bool VideoPathCopy { get; set; } = false;   // false=move, true=copy
    public bool UseCustomAudioPath { get; set; } = false;
    public string CustomAudioPath { get; set; } = "";
    public bool AudioPathCopy { get; set; } = false;
    public bool UseCustomTxtPath { get; set; } = false;
    public string CustomTxtPath { get; set; } = "";
    public bool TxtPathCopy { get; set; } = false;
    public bool UseCustomTablePath { get; set; } = false;
    public string CustomTablePath { get; set; } = "";
    public bool TablePathCopy { get; set; } = false;

    // Session naming
    public bool AskSessionName { get; set; } = true;
    public bool AppendDateSuffix { get; set; } = false;
    public string DateSuffixFormat { get; set; } = "{yyyy}.{MM}.{dd}";
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SessionVideoName { get; set; } // runtime only, not persisted

    // Hold mode
    public bool HoldModeEnabled { get; set; } = false;
    public double HoldPreSeconds { get; set; } = 2.0;
    public double HoldPostSeconds { get; set; } = 2.0;

    // Localization
    public string Language { get; set; } = "ru";

    // History
    public List<SessionHistoryEntry> SessionHistory { get; set; } = new();
    public int MaxHistoryCount { get; set; } = 20;
    public bool DeleteFilesOnClear { get; set; } = false;
    public bool ClearMarkersOnVideoLoad { get; set; } = true;

    public static List<HotkeyBindingData> GetDefaultHotkeys() => new()
    {
        new() { Action = HotkeyAction.MarkerBug, Key = (int)Key.F1, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.MarkerTask, Key = (int)Key.F2, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.MarkerNote, Key = (int)Key.F3, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.MarkerSummary, Key = (int)Key.F4, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.StartRecording, Key = (int)Key.F9, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.StopRecording, Key = (int)Key.F10, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.Generate, Key = (int)Key.F11, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.OpenOutputFolder, Key = (int)Key.F12, Modifiers = (int)ModifierKeys.Control },
    };
}

public class HotkeyBindingData
{
    public HotkeyAction Action { get; set; }
    public int Key { get; set; }
    public int Modifiers { get; set; }
}

public class SessionHistoryEntry
{
    public string SessionName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int MarkerCount { get; set; }
}
