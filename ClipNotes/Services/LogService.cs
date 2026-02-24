using System.IO;

namespace ClipNotes.Services;

public static class LogService
{
    // Logs go to %AppData%\ClipNotes\logs\ — always writable even when installed in Program Files
    public static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClipNotes", "logs");

    // In-memory buffer for UI display
    private static readonly System.Text.StringBuilder _buffer = new();
    private static readonly object _bufferLock = new();

    /// <summary>Fired after each log entry is written. NOT marshalled to UI thread.</summary>
    public static event Action<string>? LogEntryAdded;

    public static string GetBufferedText()
    {
        lock (_bufferLock) return _buffer.ToString();
    }

    public static void Info(string message)  => Write("INFO",  message);
    public static void Warn(string message)  => Write("WARN",  message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
        if (ex != null)
        {
            sb.Append($"\n  {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                sb.Append($"\n  Inner: {ex.InnerException.Message}");
        }
        var line = sb.ToString();

        // Write to file (with full stack trace)
        try
        {
            Directory.CreateDirectory(LogDir);
            var logFile = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            var fileSb = new System.Text.StringBuilder(line);
            if (ex?.StackTrace != null)
                fileSb.Append($"\n  {ex.StackTrace.Replace("\n", "\n  ")}");
            File.AppendAllText(logFile, fileSb.ToString() + "\n");
        }
        catch (Exception) { } // Can't log a logging failure — swallow silently

        // Append to in-memory buffer for UI display
        lock (_bufferLock)
        {
            if (_buffer.Length > 0) _buffer.Append('\n');
            _buffer.Append(line);
            // Keep buffer under ~50 KB
            if (_buffer.Length > 50_000)
            {
                var text = _buffer.ToString();
                var cut = text.IndexOf('\n', text.Length / 2);
                _buffer.Clear();
                if (cut >= 0) _buffer.Append(text[(cut + 1)..]);
            }
        }

        try { LogEntryAdded?.Invoke(line); } catch (Exception) { } // Prevent subscriber exceptions from crashing logger
    }
}
