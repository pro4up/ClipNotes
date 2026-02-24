using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipNotes.Setup.Models;
using ClipNotes.Setup.Services;

namespace ClipNotes.Setup.Pages;

public partial class ProgressPage : UserControl
{
    private readonly InstallOptions _options;

    public ProgressPage(InstallOptions options)
    {
        _options = options;
        InitializeComponent();

        PageTitle.Text    = Loc.T("inst_ProgressTitle");
        PageSubtitle.Text = Loc.T("inst_ProgressSubtitle");
        StepText.Text     = Loc.T("inst_Preparing");
    }

    public async Task<bool> StartInstallAsync()
    {
        var installer = new InstallerService(_options);

        installer.StepChanged += step =>
            Dispatcher.Invoke(() =>
            {
                StepText.Text = step;
                AppendLog(step);
            });

        installer.ProgressChanged += (pct, details) =>
            Dispatcher.Invoke(() =>
            {
                MainProgress.Value = pct;
                DetailsText.Text   = details;
            });

        installer.LogMessage += msg =>
            Dispatcher.Invoke(() => AppendLog(msg));

        try
        {
            await installer.InstallAsync();
            return true;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowError(ex));
            return false;
        }
    }

    private void ShowError(Exception ex)
    {
        StepText.Text      = Loc.T("inst_InstallError");
        StepText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));
        DetailsText.Text   = ex.Message;
        AppendLog($"{Loc.T("inst_ErrorPrefix")} {ex.Message}");
        if (ex.InnerException != null)
            AppendLog($"  → {ex.InnerException.Message}");
        MainProgress.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        if (Window.GetWindow(this) is MainWindow mw)
            mw.UnlockAfterError();
    }

    private void AppendLog(string? message)
    {
        if (message == null) return;
        LogText.Inlines.Add(message + "\n"); // Inlines.Add avoids O(n²) string allocation
        LogScroll.ScrollToBottom();
    }
}
