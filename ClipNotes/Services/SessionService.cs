using System.IO;
using System.Text.Json;
using ClipNotes.Models;

namespace ClipNotes.Services;

public class MarkerFileEntry
{
    public string Type { get; set; } = "Note";
    public string Timestamp { get; set; } = "00:00:00";
    public string Text { get; set; } = "";
    public bool GenerateAudio { get; set; } = true;
    public bool GenerateText { get; set; } = true;
    public string? HoldDuration { get; set; } // "mm:ss.fff", null if normal marker
}

public class MarkerFile
{
    public int Version { get; set; } = 1;
    public List<MarkerFileEntry> Markers { get; set; } = new();
}

public class SessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string CreateSessionFolder(string rootDir, string? sessionName = null)
    {
        var baseName = string.IsNullOrWhiteSpace(sessionName)
            ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            : SanitizeFileName(sessionName);

        var sessionDir = Path.Combine(rootDir, baseName);
        if (Directory.Exists(sessionDir))
        {
            int i = 2;
            while (Directory.Exists(sessionDir + $"_{i}")) i++;
            sessionDir += $"_{i}";
        }

        Directory.CreateDirectory(Path.Combine(sessionDir, "meta"));
        return sessionDir;
    }

    public void SaveSessionMeta(SessionData session)
    {
        var metaPath = Path.Combine(session.SessionFolder, "meta", "session.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(metaPath, json);
    }

    public void SaveMarkersFile(string sessionFolder, IEnumerable<Marker> markers)
    {
        var file = new MarkerFile
        {
            Markers = markers.Select(m => new MarkerFileEntry
            {
                Type = m.Type.ToString(),
                Timestamp = m.Timestamp.ToString(@"hh\:mm\:ss\.fff"),
                Text = m.Text,
                GenerateAudio = m.GenerateAudio,
                GenerateText = m.GenerateText,
                HoldDuration = m.HoldDuration?.ToString(@"hh\:mm\:ss\.fff")
            }).ToList()
        };
        var path = Path.Combine(sessionFolder, "meta", "markers.json");
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    public MarkerFile? LoadMarkersFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try { return JsonSerializer.Deserialize<MarkerFile>(File.ReadAllText(filePath), JsonOptions); }
        catch { return null; }
    }

    public SessionData? LoadSessionMeta(string sessionFolder)
    {
        var metaPath = Path.Combine(sessionFolder, "meta", "session.json");
        if (!File.Exists(metaPath)) return null;
        var json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
    }

    public async Task<string?> MoveVideoToSession(string obsOutputPath, string sessionFolder, string? videoName = null, string? effectiveVideoDir = null, CancellationToken ct = default)
    {
        // Wait for file to exist and be accessible
        for (int i = 0; i < 60; i++)
        {
            if (ct.IsCancellationRequested) return null;
            if (File.Exists(obsOutputPath))
            {
                try
                {
                    using var fs = File.Open(obsOutputPath, FileMode.Open, FileAccess.Read, FileShare.None);
                    fs.Close();
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(1000, ct);
                }
            }
            else
            {
                await Task.Delay(1000, ct);
            }
        }

        if (!File.Exists(obsOutputPath)) return null;

        var ext = Path.GetExtension(obsOutputPath);
        var videoDir = !string.IsNullOrEmpty(effectiveVideoDir)
            ? effectiveVideoDir
            : Path.Combine(sessionFolder, "video");
        Directory.CreateDirectory(videoDir);
        var fileName = string.IsNullOrWhiteSpace(videoName) ? "recording" : SanitizeFileName(videoName);
        var destPath = Path.Combine(videoDir, fileName + ext);
        File.Move(obsOutputPath, destPath, true);
        return destPath;
    }
}
