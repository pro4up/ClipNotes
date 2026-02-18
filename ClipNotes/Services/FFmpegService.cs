using System.Diagnostics;
using System.IO;

namespace ClipNotes.Services;

public class FFmpegService
{
    private string _ffmpegPath = "ffmpeg.exe";
    private string _ffprobePath = "ffprobe.exe";

    public void SetPaths(string ffmpegPath, string ffprobePath)
    {
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    public async Task<TimeSpan> GetDurationAsync(string filePath, CancellationToken ct = default)
    {
        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";
        var output = await RunProcessAsync(_ffprobePath, args, ct);
        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);
        return TimeSpan.Zero;
    }

    public async Task ExtractMasterAudioAsync(string videoPath, string outputWavPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);
        var args = $"-y -i \"{videoPath}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le \"{outputWavPath}\"";
        await RunProcessAsync(_ffmpegPath, args, ct);
    }

    public async Task ExtractAudioClipAsync(string masterAudioPath, string outputPath,
        TimeSpan start, TimeSpan duration, string codec, int bitrate,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var ic = System.Globalization.CultureInfo.InvariantCulture;

        string args;
        if (codec.ToLower() == "wav" || codec.ToLower() == "pcm_s16le")
        {
            // Fast seek + stream copy: no re-encoding, near-instant for PCM WAV
            args = $"-y -ss {start.TotalSeconds.ToString("F3", ic)} -i \"{masterAudioPath}\" -t {duration.TotalSeconds.ToString("F3", ic)} -c:a copy \"{outputPath}\"";
        }
        else
        {
            var codecArgs = codec.ToLower() switch
            {
                "mp3" => $"-c:a libmp3lame -b:a {bitrate}k",
                "aac" => $"-c:a aac -b:a {bitrate}k",
                _ => $"-c:a pcm_s16le"
            };
            args = $"-y -ss {start.TotalSeconds.ToString("F3", ic)} -i \"{masterAudioPath}\" -t {duration.TotalSeconds.ToString("F3", ic)} {codecArgs} \"{outputPath}\"";
        }

        await RunProcessAsync(_ffmpegPath, args, ct);
    }

    private static async Task<string> RunProcessAsync(string exe, string args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();
        // Read stdout and stderr concurrently to prevent pipe buffer deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}: {stderr}");

        return stdout;
    }
}
