using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace ClipNotes.Setup.Services;

public record DownloadProgress(long Downloaded, long Total, double SpeedMBps)
{
    public double Percent => Total > 0 ? (double)Downloaded / Total * 100.0 : 0;
    public string Details => Total > 0
        ? $"{Downloaded / 1_048_576.0:F1} МБ / {Total / 1_048_576.0:F1} МБ — {SpeedMBps:F1} МБ/с"
        : $"{Downloaded / 1_048_576.0:F1} МБ скачано";
}

public class DownloadService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromHours(2) };

    public async Task DownloadWithProgressAsync(
        string url,
        string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default,
        long maxBytes = 2L * 1024 * 1024 * 1024) // 2 GB default cap
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? -1;

        // Reject if Content-Length already exceeds limit
        if (maxBytes > 0 && total > maxBytes)
            throw new InvalidOperationException(
                $"Remote file too large: {total / 1_048_576.0:F0} MB (limit {maxBytes / 1_048_576} MB)");

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        using var src = await response.Content.ReadAsStreamAsync(ct);
        using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        var sw = Stopwatch.StartNew();
        long lastBytes = 0;

        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;

            // Enforce streaming size cap even when Content-Length is absent
            if (maxBytes > 0 && downloaded > maxBytes)
                throw new InvalidOperationException(
                    $"Download exceeded size limit ({maxBytes / 1_048_576} MB): {url}");

            if (progress != null && sw.ElapsedMilliseconds > 250)
            {
                double elapsed = sw.Elapsed.TotalSeconds;
                double speed = elapsed > 0 ? (downloaded - lastBytes) / elapsed / 1_048_576.0 : 0;
                lastBytes = downloaded;
                sw.Restart();
                progress.Report(new DownloadProgress(downloaded, total, speed));
            }
        }

        // Финальный репорт
        progress?.Report(new DownloadProgress(downloaded, total, 0));
    }
}
