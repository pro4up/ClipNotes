using System.IO;
using System.Reflection;

namespace ClipNotes.Helpers;

public static class PathHelper
{
    public static string AppDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

    public static string FFmpegPath => Path.Combine(AppDir, "tools", "ffmpeg.exe");
    public static string FFprobePath => Path.Combine(AppDir, "tools", "ffprobe.exe");
    public static string WhisperCliPath => Path.Combine(AppDir, "tools", "whisper-cli.exe");

    public static string ModelsDir => Path.Combine(AppDir, "models");

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
