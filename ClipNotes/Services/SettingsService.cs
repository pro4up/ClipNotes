using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClipNotes.Models;

namespace ClipNotes.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClipNotes", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);

                // Sanitize path fields in the JSON node BEFORE deserialization so the resulting
                // AppSettings object is constructed clean — no post-hoc mutation required.
                var node = JsonNode.Parse(json)!.AsObject();
                SanitizeNodePaths(node);

                var settings = JsonSerializer.Deserialize<AppSettings>(node.ToJsonString(), JsonOptions)
                               ?? new AppSettings();

                // Decrypt OBS password (DPAPI). The encrypted field is in the JSON node;
                // ObsPassword is [JsonIgnore] so it must be set explicitly after deserialization.
                var encB64 = node["ObsPasswordEncrypted"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(encB64))
                {
                    try
                    {
                        var dec = ProtectedData.Unprotect(
                            Convert.FromBase64String(encB64), null, DataProtectionScope.CurrentUser);
                        settings.ObsPassword = Encoding.UTF8.GetString(dec);
                    }
                    catch (Exception ex)
                    {
                        // DPAPI can fail if the profile was moved or data is corrupted.
                        // Log and clear — user will need to re-enter the password.
                        LogService.Warn($"DPAPI decrypt failed for ObsPassword: {ex.Message}");
                        settings.ObsPassword = null;
                    }
                }
                else
                {
                    // Migration: read plaintext ObsPassword from old settings (bypasses [JsonIgnore]).
                    settings.ObsPassword = node["ObsPassword"]?.GetValue<string>();
                }

                return settings;
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"Failed to load settings: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);

        // Serialize to a JSON node so we can inject the encrypted password
        // without mutating the settings object or writing plaintext.
        var node = JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();

        // ObsPassword is [JsonIgnore] so it is not in the node. Add encrypted version:
        if (!string.IsNullOrEmpty(settings.ObsPassword))
        {
            var enc = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(settings.ObsPassword), null, DataProtectionScope.CurrentUser);
            node["ObsPasswordEncrypted"] = Convert.ToBase64String(enc);
        }
        else
        {
            node.Remove("ObsPasswordEncrypted");
        }

        // Defensive: ensure no plaintext password leaks (ObsPassword is [JsonIgnore], but be safe).
        node.Remove("ObsPassword");

        // Write to GUID-named temp file first, then atomically replace.
        // GUID name narrows the race window vs a predictable ".tmp" suffix.
        var tmp = Path.Combine(Path.GetDirectoryName(SettingsPath)!, $".settings_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tmp, node.ToJsonString(JsonOptions));
        File.Move(tmp, SettingsPath, overwrite: true);
    }

    // Sanitize path-typed fields directly in the JSON node before deserialization.
    // This avoids any post-deserialization mutation of the AppSettings object.
    private static void SanitizeNodePaths(JsonObject node)
    {
        foreach (var key in new[]
        {
            "OutputRootDirectory", "GlossaryFilePath",
            "CustomVideoPath", "CustomAudioPath",
            "CustomTxtPath", "CustomTablePath", "ObsExePath"
        })
        {
            var raw = node[key]?.GetValue<string>();
            if (raw == null) continue;
            var clean = SanitizePath(raw);
            if (clean != raw)
                node[key] = clean; // replace with sanitized value (or null → removes key)
        }

        // Sanitize FolderPath inside each SessionHistory entry.
        // ClearHistory may call Directory.Delete on these values; a manipulated settings.json
        // could otherwise point them at arbitrary directories.
        var history = node["SessionHistory"]?.AsArray();
        if (history != null)
        {
            foreach (var item in history)
            {
                if (item is not JsonObject entry) continue;
                var raw = entry["FolderPath"]?.GetValue<string>();
                if (raw == null) continue;
                var clean = SanitizePath(raw);
                if (clean != raw) entry["FolderPath"] = clean;
            }
        }
    }

    // Returns null/empty if path is suspicious (relative, traversal segment, or overlong).
    // Uses segment-based ".." detection to avoid false-positives on names like "user..name".
    private static string? SanitizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (!Path.IsPathRooted(path)) return "";
        if (path.Length > 32767) return ""; // Windows MAX_PATH extended limit

        // Check for traversal segments (e.g. ".." as a standalone path component)
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(s => s == "..")) return "";

        return path;
    }
}
