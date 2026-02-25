using System.Diagnostics;
using System.Windows;
using ClipNotes.Services;
using ClipNotes.Views;
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
        IsUpdateReady = false;

        try
        {
            var svc = new UpdateService();
            var result = await svc.CheckAsync(InstalledBundleHash);

            if (result.HasUpdate)
            {
                UpdateAvailable = true;
                _bundleAvailable = result.BundleUrl != null;
                UpdateUrl = result.BundleUrl ?? result.ReleaseUrl;
                _latestVersionForDialog = result.LatestVersion;
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

    /// <summary>
    /// Called when the user clicks "Install Update" or "Restart to Apply".
    /// — First call: downloads bundle + stages files + writes PS1 → shows restart dialog.
    /// — If already staged (IsUpdateReady): shows dialog directly.
    /// </summary>
    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsDownloadingUpdate) return;

        // If already staged, just show the dialog again
        if (IsUpdateReady && _pendingUpdaterScript != null)
        {
            ShowRestartDialog();
            return;
        }

        if (string.IsNullOrEmpty(UpdateUrl))
            return;

        // No bundle asset in the release → open GitHub release page in browser
        if (!_bundleAvailable)
        {
            Process.Start(new ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
            return;
        }

        IsDownloadingUpdate = true;
        UpdateStatus = Loc.T("loc_UpdateDownloading");

        try
        {
            var svc = new UpdateService();
            var progress = new Progress<double>(fraction =>
            {
                var pct = (int)(fraction * 100);
                UpdateStatus = $"{Loc.T("loc_UpdateDownloading")} {pct}%";
            });

            var stagingDir = await svc.DownloadAndStageBundleAsync(UpdateUrl, progress, CancellationToken.None);
            _pendingStagingDir = stagingDir;

            var appDir = ClipNotes.Helpers.PathHelper.AppDir;
            var pid    = Process.GetCurrentProcess().Id;
            _pendingUpdaterScript = UpdateService.WriteUpdaterScript(pid, stagingDir, appDir);

            IsUpdateReady = true;
            UpdateStatus  = Loc.T("loc_UpdateReadyShort");

            LogSvc.Info($"Update staged: {stagingDir}, script: {_pendingUpdaterScript}");

            ShowRestartDialog();
        }
        catch (Exception ex)
        {
            UpdateService.CleanupStagedUpdate(_pendingStagingDir, _pendingUpdaterScript);
            _pendingStagingDir    = null;
            _pendingUpdaterScript = null;
            IsUpdateReady = false;
            UpdateStatus  = $"{Loc.T("loc_UpdateDownloadFailed")}: {ex.Message}";
            LogSvc.Warn($"Update download failed: {ex.Message}");
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    private void ShowRestartDialog()
    {
        var version = _latestVersionForDialog ?? UpdateService.CurrentVersion;
        var dialog  = new UpdateRestartDialog(version)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();

        if (dialog.RestartNow)
        {
            SaveSettings(); // persist settings before exit
            LogSvc.Info("Restarting for update...");
            _launchingUpdater = true; // tell Cleanup() to preserve staged files for the script
            UpdateService.LaunchUpdaterScript(_pendingUpdaterScript!);
            Application.Current.Shutdown();
        }
        else
        {
            // "Later" — staging dir and script stay in place for next dialog open
            LogSvc.Info("Update postponed by user.");
        }
    }
}
