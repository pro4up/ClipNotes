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

    public static string GetModelPath(string modelName)
    {
        var fileName = $"ggml-{modelName}.bin";
        return Path.Combine(ModelsDir, fileName);
    }

    public static bool ValidateTools()
    {
        return File.Exists(FFmpegPath) && File.Exists(FFprobePath) && File.Exists(WhisperCliPath);
    }
}
