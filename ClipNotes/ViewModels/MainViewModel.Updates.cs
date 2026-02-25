using System.Diagnostics;
using ClipNotes.Services;
using CommunityToolkit.Mvvm.Input;
using Loc = ClipNotes.Helpers.LocalizationService;
using LogSvc = ClipNotes.Services.LogService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        UpdateStatus = Loc.T("loc_UpdateChecking");
        UpdateAvailable = false;
        UpdateUrl = null;

        try
        {
            var svc = new UpdateService();
            var result = await svc.CheckAsync(InstalledBundleHash);

            if (result.HasUpdate)
            {
                UpdateAvailable = true;
                UpdateUrl = result.ReleaseUrl;
                UpdateStatus = result.Reason == UpdateChangeReason.FilesChanged
                    ? Loc.T("loc_UpdateFilesChanged")
                    : string.Format(Loc.T("loc_UpdateAvailable"), result.LatestVersion);
            }
            else
            {
                UpdateStatus = string.Format(Loc.T("loc_UpdateUpToDate"), result.CurrentVersion);
            }

            LogSvc.Info($"Update check: current={result.CurrentVersion}, latest={result.LatestVersion}, hasUpdate={result.HasUpdate}");
        }
        catch (Exception ex)
        {
            UpdateStatus = $"{Loc.T("loc_UpdateFailed")}: {ex.Message}";
            LogSvc.Warn($"Update check failed: {ex.Message}");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private void OpenUpdateUrl()
    {
        if (!string.IsNullOrEmpty(UpdateUrl))
            Process.Start(new ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
    }
}
