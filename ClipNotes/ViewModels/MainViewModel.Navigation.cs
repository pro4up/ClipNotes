using System.Diagnostics;
using System.IO;
using System.Windows;
using ClipNotes.Helpers;
using ClipNotes.Models;
using ClipNotes.Services;
using CommunityToolkit.Mvvm.Input;
using LogSvc = ClipNotes.Services.LogService;
using Loc = ClipNotes.Helpers.LocalizationService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task TestObsConnectionAsync()
    {
        ObsStatus = Loc.T("loc_StatusObsConnecting");
        var pw = string.IsNullOrEmpty(ObsPassword) ? null : ObsPassword;
        var ok = await _obs.ConnectAsync(ObsHost, ObsPort, pw);
        ObsConnected = ok;
        ObsStatus = ok ? Loc.T("loc_StatusObsConnected") : Loc.T("loc_StatusObsFailed");
        if (ok) SaveSettings();
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.T("loc_DlgOutputDir")
        };
        if (dialog.ShowDialog() == true)
        {
            OutputRootDirectory = dialog.FolderName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void BrowseGlossaryFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("loc_DlgGlossary"),
            Filter = Loc.T("loc_FilterGlossary")
        };
        if (dialog.ShowDialog() == true)
        {
            GlossaryFilePath = dialog.FileName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void BrowseObsExe()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("loc_DlgObsExe"),
            Filter = Loc.T("loc_FilterObsExe")
        };
        if (dialog.ShowDialog() == true)
        {
            ObsExePath = dialog.FileName;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        if (DeleteFilesOnClear)
        {
            foreach (var entry in SessionHistory)
            {
                try
                {
                    if (Directory.Exists(entry.FolderPath))
                        Directory.Delete(entry.FolderPath, true);
                }
                catch (Exception ex) { LogSvc.Warn($"ClearHistory delete failed: {ex.Message}"); }
            }
        }
        SessionHistory.Clear();
        SaveSettings();
    }

    [RelayCommand]
    private void OpenSessionFolder()
    {
        var folder = _currentSession?.SessionFolder;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) && IsLocalPath(folder))
            Process.Start("explorer.exe", folder);
    }

    [RelayCommand]
    private void OpenHistoryFolder(string? folderPath)
    {
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && IsLocalPath(folderPath))
            Process.Start("explorer.exe", folderPath);
    }

    // Reject UNC paths (\\server\share) that could trigger outbound SMB connections
    // when explorer.exe opens them — a path stored in settings could be manipulated.
    private static bool IsLocalPath(string path) =>
        Path.IsPathRooted(path) && !path.StartsWith(@"\\");

    [RelayCommand]
    private void BrowseCustomPath(string which)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = Loc.T("loc_DlgSelectFolder") };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        switch (which)
        {
            case "video": CustomVideoPath = dialog.SelectedPath; break;
            case "audio": CustomAudioPath = dialog.SelectedPath; break;
            case "txt":   CustomTxtPath   = dialog.SelectedPath; break;
            case "table": CustomTablePath = dialog.SelectedPath; break;
        }
    }
}
