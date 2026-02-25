using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace ClipNotes.Services;

public record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string? BundleUrl,
    UpdateChangeReason Reason);

public enum UpdateChangeReason { None, NewVersion, FilesChanged }

public class UpdateService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "ClipNotes-App" } }
    };

    private const string ApiUrl =
        "https://api.github.com/repos/pro4up/ClipNotes/releases/latest";
    private const string Sha256SumsUrl =
        "https://github.com/pro4up/ClipNotes/releases/latest/download/SHA256SUMS.txt";
    private const long MaxSumsBytes   = 64 * 1024;          // SHA256SUMS.txt is always tiny
    private const long MaxBundleBytes = 500L * 1024 * 1024; // 500 MB hard cap

    /// <summary>Returns the current app version from the assembly manifest.</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Checks GitHub for a newer release or changed bundle files.
    /// <paramref name="installedBundleHash"/> is the SHA-256 of ClipNotes-bundle.zip that was
    /// used during installation. Pass null to skip hash comparison.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        string? installedBundleHash, CancellationToken ct = default)
    {
        var current = CurrentVersion;

        var json = await _http.GetStringAsync(ApiUrl, ct);
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("GitHub API returned invalid JSON");

        var tagName = node["tag_name"]?.GetValue<string>() ?? "";
        var htmlUrl = node["html_url"]?.GetValue<string>()  ?? "";
        var latest  = tagName.TrimStart('v');

        // Find ClipNotes-bundle.zip in assets
        var bundleUrl = ParseBundleUrl(node);

        if (IsVersionNewer(latest, current))
            return new UpdateCheckResult(true, current, latest, htmlUrl, bundleUrl, UpdateChangeReason.NewVersion);

        // Same version — check if the published bundle hash differs
        if (!string.IsNullOrEmpty(installedBundleHash))
        {
            var publishedHash = await FetchBundleHashAsync(ct);
            if (publishedHash != null &&
                !string.Equals(publishedHash, installedBundleHash, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheckResult(true, current, latest, htmlUrl, bundleUrl, UpdateChangeReason.FilesChanged);
        }

        return new UpdateCheckResult(false, current, latest, htmlUrl, bundleUrl, UpdateChangeReason.None);
    }

    /// <summary>Downloads ClipNotes-bundle.zip and extracts app files to a temp staging directory.</summary>
    /// <returns>Path to the staging directory ready for copy into AppDir.</returns>
    public async Task<string> DownloadAndStageBundleAsync(
        string bundleUrl,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), $"ClipNotes-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        var zipPath = Path.Combine(Path.GetTempPath(), $"ClipNotes-bundle-{Guid.NewGuid():N}.zip");
        try
        {
            // Download with streaming progress
            using var resp = await _http.GetAsync(bundleUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            if (total > MaxBundleBytes)
                throw new InvalidOperationException($"Bundle is too large ({total / 1_048_576} MB).");

            await using (var netStream = await resp.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = File.Create(zipPath))
            {
                var buf = new byte[81_920];
                long downloaded = 0;
                int read;
                while ((read = await netStream.ReadAsync(buf, ct)) > 0)
                {
                    await fileStream.WriteAsync(buf.AsMemory(0, read), ct);
                    downloaded += read;
                    if (downloaded > MaxBundleBytes)
                        throw new InvalidOperationException("Bundle exceeded size limit during download.");
                    if (total > 0)
                        progress?.Report((double)downloaded / total);
                }
            }

            // Verify SHA-256 before extracting (MITM / corruption guard)
            var expectedHash = await FetchBundleHashAsync(ct);
            if (expectedHash == null)
                throw new InvalidOperationException(
                    "Could not verify bundle checksum — SHA256SUMS.txt unavailable.");

            await using (var hashStream = File.OpenRead(zipPath))
            {
                var hashBytes = await SHA256.HashDataAsync(hashStream, ct);
                var actualHash = Convert.ToHexString(hashBytes);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Bundle checksum mismatch — download may be corrupted or tampered with.");
            }

            // Extract ClipNotes/app/* → stagingDir
            ExtractBundleToStaging(zipPath, stagingDir);
        }
        catch
        {
            TryDeleteDirectory(stagingDir);
            throw;
        }
        finally
        {
            TryDeleteFile(zipPath);
        }

        return stagingDir;
    }

    /// <summary>
    /// Writes a PowerShell updater script to %TEMP%.
    /// The script waits for the given PID to exit, then copies files and restarts the app.
    /// </summary>
    public static string WriteUpdaterScript(int currentPid, string stagingDir, string appDir)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"ClipNotes-updater-{Guid.NewGuid():N}.ps1");
        var appExe = Path.Combine(appDir, "ClipNotes.exe");

        var sb = new StringBuilder();
        sb.AppendLine($"$pidToWait = {currentPid}");
        sb.AppendLine($"$src = '{EscapePs(stagingDir)}'");
        sb.AppendLine($"$dst = '{EscapePs(appDir)}'");
        sb.AppendLine($"$exe = '{EscapePs(appExe)}'");
        sb.AppendLine($"$script = '{EscapePs(scriptPath)}'");
        sb.AppendLine($"$log = Join-Path $env:TEMP 'ClipNotes-update.log'");
        sb.AppendLine();
        // Wait for the app to exit (up to 60 s)
        sb.AppendLine("for ($i = 0; $i -lt 120; $i++) {");
        sb.AppendLine("    if (-not (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue)) { break }");
        sb.AppendLine("    Start-Sleep -Milliseconds 500");
        sb.AppendLine("}");
        sb.AppendLine();
        // Copy files — Stop on error so partial writes are visible in the log
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("try {");
        sb.AppendLine("    Get-ChildItem -Path $src -Recurse | ForEach-Object {");
        sb.AppendLine("        $rel = $_.FullName.Substring($src.Length + 1)");
        sb.AppendLine("        $target = Join-Path $dst $rel");
        sb.AppendLine("        if ($_.PSIsContainer) {");
        sb.AppendLine("            New-Item -ItemType Directory -Path $target -Force | Out-Null");
        sb.AppendLine("        } else {");
        sb.AppendLine("            Copy-Item -Path $_.FullName -Destination $target -Force");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("} catch {");
        sb.AppendLine("    \"[ClipNotes updater] Copy failed: $_\" | Out-File $log -Append");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine();
        // Cleanup and restart — errors here are non-fatal
        sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
        sb.AppendLine("Remove-Item $src -Recurse -Force");
        sb.AppendLine();
        // Start updated app
        sb.AppendLine("Start-Process $exe");
        sb.AppendLine();
        // Self-delete
        sb.AppendLine("Start-Sleep -Milliseconds 500");
        sb.AppendLine("Remove-Item $script -Force");

        File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);
        return scriptPath;
    }

    /// <summary>Launches the updater PS1 script detached and returns immediately.</summary>
    public static void LaunchUpdaterScript(string scriptPath)
    {
        // Use -EncodedCommand (Base64 UTF-16LE) to avoid any path-escaping issues
        // with spaces or special characters in %TEMP% / user profile paths.
        var command = $"& '{EscapePs(scriptPath)}'";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        Process.Start(new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = true,
            WindowStyle     = ProcessWindowStyle.Hidden
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Downloads SHA256SUMS.txt and returns the hash for ClipNotes-bundle.zip.</summary>
    public static async Task<string?> FetchBundleHashAsync(CancellationToken ct = default)
    {
        try
        {
            using var req  = new HttpRequestMessage(HttpMethod.Get, Sha256SumsUrl);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            if (!resp.IsSuccessStatusCode) return null;

            if (resp.Content.Headers.ContentLength > MaxSumsBytes) return null;

            var text = await resp.Content.ReadAsStringAsync(ct);
            return ParseBundleHash(text);
        }
        catch { return null; }
    }

    private static string? ParseBundleUrl(JsonNode releaseNode)
    {
        try
        {
            var assets = releaseNode["assets"]?.AsArray();
            if (assets == null) return null;
            foreach (var asset in assets)
            {
                var name = asset?["name"]?.GetValue<string>() ?? "";
                if (name.Equals("ClipNotes-bundle.zip", StringComparison.OrdinalIgnoreCase))
                    return asset?["browser_download_url"]?.GetValue<string>();
            }
        }
        catch { }
        return null;
    }

    private static void ExtractBundleToStaging(string zipPath, string stagingDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        // Bundle structure: ClipNotes/app/<files>  — strip the "ClipNotes/app/" prefix
        const string prefix = "ClipNotes/app/";
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = name[prefix.Length..];
            if (string.IsNullOrEmpty(relative) || relative.EndsWith('/'))
                continue; // skip directory entries

            // Safety: no path traversal
            if (relative.Contains(".."))
                continue;

            var destPath = Path.GetFullPath(Path.Combine(stagingDir, relative));
            if (!destPath.StartsWith(stagingDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    private static string? ParseBundleHash(string sumsText)
    {
        foreach (var line in sumsText.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                parts[1].Equals("ClipNotes-bundle.zip", StringComparison.OrdinalIgnoreCase))
                return parts[0].ToUpperInvariant();
        }
        return null;
    }

    private static bool IsVersionNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var vLatest) &&
            Version.TryParse(current, out var vCurrent))
            return vLatest > vCurrent;
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string EscapePs(string path) => path.Replace("'", "''");

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    /// <summary>Cleans up a staging directory and its updater script (called on "Later" / app close).</summary>
    public static void CleanupStagedUpdate(string? stagingDir, string? scriptPath)
    {
        if (stagingDir != null) TryDeleteDirectory(stagingDir);
        if (scriptPath != null) TryDeleteFile(scriptPath);
    }
}
