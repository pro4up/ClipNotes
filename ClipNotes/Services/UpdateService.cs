using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;

namespace ClipNotes.Services;

public record UpdateCheckResult(
    bool HasUpdate,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    UpdateChangeReason Reason);

public enum UpdateChangeReason { None, NewVersion, FilesChanged }

public class UpdateService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders = { { "User-Agent", "ClipNotes-App" } }
    };

    private const string ApiUrl =
        "https://api.github.com/repos/pro4up/ClipNotes/releases/latest";
    private const string Sha256SumsUrl =
        "https://github.com/pro4up/ClipNotes/releases/latest/download/SHA256SUMS.txt";
    private const long MaxSumsBytes = 64 * 1024; // SHA256SUMS.txt is always tiny

    /// <summary>Returns the current app version from the assembly manifest.</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Checks GitHub for a newer release or changed bundle files.
    /// <paramref name="installedBundleHash"/> is the SHA-256 of ClipNotes-bundle.zip that was
    /// used during installation (stored in settings). Pass null to skip hash comparison.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        string? installedBundleHash, CancellationToken ct = default)
    {
        var current = CurrentVersion;

        // 1. Fetch latest release metadata from GitHub API
        var json = await _http.GetStringAsync(ApiUrl, ct);
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("GitHub API returned invalid JSON");

        var tagName  = node["tag_name"]?.GetValue<string>() ?? "";
        var htmlUrl  = node["html_url"]?.GetValue<string>()  ?? "";
        var latest   = tagName.TrimStart('v');

        // 2. Version comparison
        if (IsVersionNewer(latest, current))
            return new UpdateCheckResult(true, current, latest, htmlUrl, UpdateChangeReason.NewVersion);

        // 3. Same version — check if the published bundle hash differs (files changed in-place)
        if (!string.IsNullOrEmpty(installedBundleHash))
        {
            var publishedHash = await FetchBundleHashAsync(ct);
            if (publishedHash != null &&
                !string.Equals(publishedHash, installedBundleHash, StringComparison.OrdinalIgnoreCase))
                return new UpdateCheckResult(true, current, latest, htmlUrl, UpdateChangeReason.FilesChanged);
        }

        return new UpdateCheckResult(false, current, latest, htmlUrl, UpdateChangeReason.None);
    }

    /// <summary>Downloads SHA256SUMS.txt and returns the hash for ClipNotes-bundle.zip.</summary>
    public static async Task<string?> FetchBundleHashAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Sha256SumsUrl);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            if (!resp.IsSuccessStatusCode) return null;

            // Guard against unexpectedly large responses
            if (resp.Content.Headers.ContentLength > MaxSumsBytes) return null;

            var text = await resp.Content.ReadAsStringAsync(ct);
            return ParseBundleHash(text);
        }
        catch
        {
            return null;
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
        // Fallback: lexicographic (handles pre-release tags like "1.0.1-rc1")
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
