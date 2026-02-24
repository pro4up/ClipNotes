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
        string audioClipPath,
        string outputBasePath,
        string language,
        string? glossary,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputBasePath)!);

        var threads = Math.Min(Environment.ProcessorCount, 8);
        var args = $"-m \"{_modelPath}\" -f \"{audioClipPath}\" -t {threads}";

        // Strictly whitelist language codes (ISO 639-1/3: 2-8 lowercase alpha chars) before injecting
        if (language != "auto" && !string.IsNullOrWhiteSpace(language)
            && System.Text.RegularExpressions.Regex.IsMatch(language, @"^[a-z]{2,8}$"))
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
        // Read stdout and stderr concurrently to prevent pipe buffer deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"whisper-cli error (exit {process.ExitCode}): {stderr}");

        return stdout;
    }
}
