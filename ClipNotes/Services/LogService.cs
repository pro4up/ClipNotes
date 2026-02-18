using System.Diagnostics;
using System.IO;

namespace ClipNotes.Services;

public static class LogService
{
    private static readonly string LogDir = Path.Combine(
        Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? ".", "Logs");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var logFile = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}");
            if (ex != null)
            {
                sb.AppendLine($"  {ex.GetType().Name}: {ex.Message}");
                if (ex.StackTrace != null)
                    sb.AppendLine($"  {ex.StackTrace.Replace("\n", "\n  ")}");
                if (ex.InnerException != null)
                    sb.AppendLine($"  Inner: {ex.InnerException.Message}");
            }
            File.AppendAllText(logFile, sb.ToString());
        }
        catch { }
    }
}
