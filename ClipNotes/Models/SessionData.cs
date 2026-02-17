namespace ClipNotes.Models;

public class SessionData
{
    public string SessionName { get; set; } = "";
    public string SessionFolder { get; set; } = "";
    public string? VideoFilePath { get; set; }
    public string? MasterAudioPath { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public List<Marker> Markers { get; set; } = new();
    public AppSettings SettingsSnapshot { get; set; } = new();
    public string? ObsOutputPath { get; set; }
}
