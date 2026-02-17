using System.Diagnostics;
using System.IO;

namespace ClipNotes.Services;

public class WhisperService
{
    private string _whisperCliPath = "whisper-cli.exe";
    private string _modelPath = "";

    public void SetPaths(string whisperCliPath, string modelPath)
    {
        _whisperCliPath = whisperCliPath;
        _modelPath = modelPath;
    }

    public async Task<string> TranscribeSegmentAsync(
        string audioPath,
        TimeSpan offset,
        TimeSpan duration,
        string outputBasePath,
        string language,
        string? glossary,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputBasePath)!);

        var offsetMs = (int)offset.TotalMilliseconds;
        var durationMs = (int)duration.TotalMilliseconds;

        var args = $"-m \"{_modelPath}\" -f \"{audioPath}\"";
        args += $" --offset-t {offsetMs} --duration {durationMs}";

        if (language != "auto" && !string.IsNullOrWhiteSpace(language))
            args += $" -l {language}";

        if (!string.IsNullOrWhiteSpace(glossary))
            args += $" --prompt \"{glossary.Replace("\"", "\\\"")}\"";

        args += $" --output-txt -of \"{outputBasePath}\"";

        await RunProcessAsync(_whisperCliPath, args, ct);

        var txtPath = outputBasePath + ".txt";
        if (File.Exists(txtPath))
            return await File.ReadAllTextAsync(txtPath, ct);
        return "";
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
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"whisper-cli error (exit {process.ExitCode}): {stderr}");

        return stdout;
    }
}
