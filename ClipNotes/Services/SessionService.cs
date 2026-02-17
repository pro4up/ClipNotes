using System.IO;
using System.Text.Json;
using ClipNotes.Models;

namespace ClipNotes.Services;

public class SessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string CreateSessionFolder(string rootDir, string? sessionName = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var name = string.IsNullOrWhiteSpace(sessionName) ? timestamp : $"{timestamp}_{sessionName}";
        var sessionDir = Path.Combine(rootDir, name);

        Directory.CreateDirectory(Path.Combine(sessionDir, "video"));
        Directory.CreateDirectory(Path.Combine(sessionDir, "audio"));
        Directory.CreateDirectory(Path.Combine(sessionDir, "txt"));
        Directory.CreateDirectory(Path.Combine(sessionDir, "table"));
        Directory.CreateDirectory(Path.Combine(sessionDir, "meta"));

        return sessionDir;
    }

    public void SaveSessionMeta(SessionData session)
    {
        var metaPath = Path.Combine(session.SessionFolder, "meta", "session.json");
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(metaPath, json);
    }

    public SessionData? LoadSessionMeta(string sessionFolder)
    {
        var metaPath = Path.Combine(sessionFolder, "meta", "session.json");
        if (!File.Exists(metaPath)) return null;
        var json = File.ReadAllText(metaPath);
        return JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
    }

    public async Task<string?> MoveVideoToSession(string obsOutputPath, string sessionFolder, CancellationToken ct = default)
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
        var destPath = Path.Combine(sessionFolder, "video", $"recording{ext}");
        File.Move(obsOutputPath, destPath, true);
        return destPath;
    }
}
