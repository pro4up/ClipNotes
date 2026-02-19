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
    }

    /// <summary>Запустить установку. Возвращает true при успехе, false при ошибке.</summary>
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
        StepText.Text      = "Ошибка установки";
        StepText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));
        DetailsText.Text   = ex.Message;
        AppendLog($"ОШИБКА: {ex.Message}");
        if (ex.InnerException != null)
            AppendLog($"  → {ex.InnerException.Message}");
        MainProgress.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        // Показать кнопку «Назад» — разблокировать навигацию
        if (Window.GetWindow(this) is MainWindow mw)
            mw.UnlockAfterError();
    }

    private void AppendLog(string message)
    {
        LogText.Text += message + "\n";
        LogScroll.ScrollToBottom();
    }
}
