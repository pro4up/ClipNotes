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

        var psi = new ProcessStartInfo
        {
            FileName = _whisperCliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(_modelPath);
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(audioClipPath);
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add(Math.Min(Environment.ProcessorCount, 8).ToString());

        // Strictly whitelist language codes (ISO 639-1/3: 2-8 lowercase alpha chars)
        if (language != "auto" && !string.IsNullOrWhiteSpace(language)
            && System.Text.RegularExpressions.Regex.IsMatch(language, @"^[a-z]{2,8}$"))
        {
            psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(language);
        }

        if (!string.IsNullOrWhiteSpace(glossary))
        {
            psi.ArgumentList.Add("--prompt"); psi.ArgumentList.Add(glossary);
        }

        psi.ArgumentList.Add("--output-txt");
        psi.ArgumentList.Add("-of"); psi.ArgumentList.Add(outputBasePath);

        await RunProcessAsync(psi, ct);

        var txtPath = outputBasePath + ".txt";
        if (File.Exists(txtPath))
            return await File.ReadAllTextAsync(txtPath, ct);
        return "";
    }

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
            throw new InvalidOperationException($"whisper-cli error (exit {process.ExitCode}): {stderr}");

        return stdout;
    }
}
