using System.Diagnostics;
using System.IO;
using System.Windows;
using ClipNotes.Services;
using CommunityToolkit.Mvvm.Input;
using LogSvc = ClipNotes.Services.LogService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel
{
    /// <summary>Called from Window_Loaded — loads today's log file off the constructor hot-path.</summary>
    public void InitializeLogs()
    {
        var todayLog = Path.Combine(LogSvc.LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
        if (!File.Exists(todayLog)) return;
        try { LogText = File.ReadAllText(todayLog).TrimEnd(); }
        catch (Exception ex) { LogSvc.Warn($"InitializeLogs read failed: {ex.Message}"); }
    }

    private void OnLogEntryAdded(string _)
    {
        // Use the pre-built StringBuilder buffer — avoids O(n²) string concatenation
        Application.Current?.Dispatcher.Invoke(() => LogText = LogSvc.GetBufferedText());
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(LogSvc.LogDir);
            Process.Start(new ProcessStartInfo(LogSvc.LogDir) { UseShellExecute = true });
        }
        catch (Exception ex) { LogSvc.Error("OpenLogsFolder failed", ex); }
    }

    [RelayCommand]
    private void CopyLogsToClipboard()
    {
        try { System.Windows.Clipboard.SetText(string.IsNullOrEmpty(LogText) ? "" : LogText); }
        catch (Exception ex) { LogSvc.Error("CopyLogsToClipboard failed", ex); }
    }
}
