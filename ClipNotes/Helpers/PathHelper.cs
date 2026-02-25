using System.Diagnostics;
using System.IO;

namespace ClipNotes.Helpers;

public static class PathHelper
{
    public static string AppDir =>
        Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName)
        ?? AppContext.BaseDirectory;

    // tools/, models/, lang/ reside one level above the app/ subfolder
    private static string RootDir =>
        Path.GetFullPath(Path.Combine(AppDir, ".."));

    public static string FFmpegPath => Path.Combine(RootDir, "tools", "ffmpeg.exe");
    public static string FFprobePath => Path.Combine(RootDir, "tools", "ffprobe.exe");
    public static string WhisperCliPath => Path.Combine(RootDir, "tools", "whisper-cli.exe");

    public static string ModelsDir => Path.Combine(RootDir, "models");

    // Allowlist of supported model names — rejects path-traversal or arbitrary names from settings.json.
    private static readonly HashSet<string> AllowedModelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "base", "small", "medium", "large-v3", "large-v3-turbo"
    };

    public static string GetModelPath(string modelName)
    {
        if (!AllowedModelNames.Contains(modelName))
            modelName = "large-v3-turbo"; // safe default — unknown model names rejected
        var fileName = $"ggml-{modelName}.bin";
        return Path.Combine(ModelsDir, fileName);
    }

    public static bool ValidateTools()
    {
        return File.Exists(FFmpegPath) && File.Exists(FFprobePath) && File.Exists(WhisperCliPath);
    }
}
