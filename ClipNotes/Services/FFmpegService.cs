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
        var psi = BuildPsi(_ffprobePath);
        psi.ArgumentList.Add("-v"); psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries"); psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of"); psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(filePath);

        var output = await RunProcessAsync(psi, ct);
        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);
        return TimeSpan.Zero;
    }

    public async Task ExtractMasterAudioAsync(string videoPath, string outputWavPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);

        var psi = BuildPsi(_ffmpegPath);
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(videoPath);
        psi.ArgumentList.Add("-vn");
        psi.ArgumentList.Add("-ar"); psi.ArgumentList.Add("16000");
        psi.ArgumentList.Add("-ac"); psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("pcm_s16le");
        psi.ArgumentList.Add(outputWavPath);

        await RunProcessAsync(psi, ct);
    }

    public async Task ExtractAudioClipAsync(string masterAudioPath, string outputPath,
        TimeSpan start, TimeSpan duration, string codec, int bitrate,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var ic = System.Globalization.CultureInfo.InvariantCulture;

        var psi = BuildPsi(_ffmpegPath);
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-ss"); psi.ArgumentList.Add(start.TotalSeconds.ToString("F3", ic));
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(masterAudioPath);
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add(duration.TotalSeconds.ToString("F3", ic));

        switch (codec.ToLower())
        {
            case "wav":
            case "pcm_s16le":
                // Fast seek + stream copy: no re-encoding, near-instant for PCM WAV
                psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("copy");
                break;
            case "mp3":
                psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("libmp3lame");
                psi.ArgumentList.Add("-b:a"); psi.ArgumentList.Add($"{bitrate}k");
                break;
            case "aac":
                psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("aac");
                psi.ArgumentList.Add("-b:a"); psi.ArgumentList.Add($"{bitrate}k");
                break;
            default:
                psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("pcm_s16le");
                break;
        }

        psi.ArgumentList.Add(outputPath);
        await RunProcessAsync(psi, ct);
    }

    private static ProcessStartInfo BuildPsi(string exe) => new()
    {
        FileName = exe,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    private static async Task<string> RunProcessAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi };
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
