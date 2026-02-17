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

    // Theme
    public string Theme { get; set; } = "Светлая";

    // History
    public List<SessionHistoryEntry> SessionHistory { get; set; } = new();

    public static List<HotkeyBindingData> GetDefaultHotkeys() => new()
    {
        new() { Action = HotkeyAction.MarkerBug, Key = (int)Key.F1, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.MarkerTask, Key = (int)Key.F2, Modifiers = (int)ModifierKeys.Control },
        new() { Action = HotkeyAction.MarkerNote, Key = (int)Key.F3, Modifiers = (int)ModifierKeys.Control },
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
