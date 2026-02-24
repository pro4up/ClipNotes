using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ClipNotes.Setup.Dialogs;
using ClipNotes.Setup.Models;
using ClipNotes.Setup.Pages;

namespace ClipNotes.Setup;

public partial class MainWindow : Window
{
    private readonly InstallOptions _options = new();
    private readonly List<UserControl> _pages = [];
    private int _currentPage = 0;
    private bool _isInstalling = false;
    private readonly bool _autoStartInstall;

    public MainWindow(InstallOptions? options = null, bool autoStartInstall = false)
    {
        if (options != null) _options = options;
        _autoStartInstall = autoStartInstall;
        Loc.Load(_options.Language);
        InitializeComponent();
        BuildPages();
        BuildStepIndicator();
        NavigateTo(0, animate: false);
        if (autoStartInstall) Loaded += OnAutoStartInstall;
    }

    // ── Language ─────────────────────────────────────────────────────────────

    public void ApplyLanguage(string lang)
    {
        _options.Language = lang;
        Loc.Load(lang);

        // Rebuild pages 1–6 (preserve page 0 / WelcomePage)
        for (int i = 1; i < _pages.Count; i++)
            _pages[i] = CreatePage(i);

        if (_currentPage >= 1)
            PageContent.Content = _pages[_currentPage];

        if (_pages[0] is WelcomePage wp)
            wp.ApplyLocalization();

        UpdateNavigation();
    }

    private UserControl CreatePage(int index) => index switch
    {
        0 => new WelcomePage(_options, ApplyLanguage),
        1 => new OptionsPage(_options),
        2 => new BackendPage(_options),
        3 => new ModelPage(_options),
        4 => new SummaryPage(_options),
        5 => new ProgressPage(_options),
        6 => new FinishPage(_options),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    // ── Pages ─────────────────────────────────────────────────────────────────

    private async void OnAutoStartInstall(object sender, RoutedEventArgs e)
    {
        Loaded -= OnAutoStartInstall;
        NavigateTo(5, animate: false);
        _isInstalling = true;
        bool success = false;
        if (_pages[5] is ProgressPage pp)
            success = await pp.StartInstallAsync();
        _isInstalling = false;
        if (success) NavigateTo(6);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDwmTheme(this);
    }

    private void BuildPages()
    {
        _pages.Clear();
        for (int i = 0; i <= 6; i++)
            _pages.Add(CreatePage(i));
    }

    private void BuildStepIndicator()
    {
        // Pages 1–5 show in step indicator
        StepIndicator.Children.Clear();
        for (int i = 1; i <= 5; i++)
        {
            var ellipse = new Ellipse
            {
                Width  = 7, Height = 7,
                Margin = new Thickness(4, 0, 4, 0),
                Tag    = i,
            };
            StepIndicator.Children.Add(ellipse);
        }
        UpdateStepIndicator();
    }

    private void UpdateStepIndicator()
    {
        foreach (Ellipse ellipse in StepIndicator.Children)
        {
            int pageIdx = (int)ellipse.Tag;
            bool active = pageIdx == _currentPage;
            bool passed = pageIdx < _currentPage;
            ellipse.Width  = active ? 9 : 7;
            ellipse.Height = active ? 9 : 7;
            ellipse.Fill   = active
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : new SolidColorBrush(Color.FromRgb(0xC7, 0xC7, 0xCC));
            ellipse.Opacity = active ? 1.0 : passed ? 0.8 : 0.4;
        }
    }

    private void NavigateTo(int index, bool animate = true)
    {
        if (index < 0 || index >= _pages.Count) return;
        _currentPage = index;

        if (animate)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
            fadeOut.Completed += (_, _) =>
            {
                PageContent.Content = _pages[_currentPage];
                PageContent.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            };
            PageContent.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            PageContent.Content = _pages[_currentPage];
        }

        UpdateNavigation();
        UpdateStepIndicator();
    }

    private void UpdateNavigation()
    {
        bool isWelcome  = _currentPage == 0;
        bool isProgress = _currentPage == 5;
        bool isFinish   = _currentPage == 6;

        BackButton.Visibility = (isWelcome || isProgress || isFinish)
            ? Visibility.Hidden : Visibility.Visible;
        NextButton.Visibility = isProgress ? Visibility.Hidden : Visibility.Visible;

        BackButton.Content = Loc.T("inst_Back");
        NextButton.Content = (_currentPage == 0 || _currentPage == 4) ? Loc.T("inst_Install")
                           : isFinish                                  ? Loc.T("inst_Finish")
                                                                       : Loc.T("inst_Next");

        StepIndicator.Visibility = (isWelcome || isFinish) ? Visibility.Hidden : Visibility.Visible;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0 && !_isInstalling)
            NavigateTo(_currentPage - 1);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage == 6)
        {
            if (_pages[6] is FinishPage fp && fp.ShouldLaunch)
            {
                var exePath = System.IO.Path.Combine(_options.InstallPath, "app", "ClipNotes.exe");
                if (System.IO.File.Exists(exePath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = exePath,
                        UseShellExecute = true,
                    });
            }
            Application.Current.Shutdown();
            return;
        }

        if (_currentPage == 4)
        {
            if (NeedsAdminForPath(_options.InstallPath) && !IsAdmin())
            {
                var dlg = new UacConfirmDialog(_options.InstallPath) { Owner = this };
                dlg.ShowDialog();
                if (dlg.Confirmed) RestartAsAdmin();
                return;
            }

            NavigateTo(5);
            _isInstalling = true;
            bool success = false;
            if (_pages[5] is ProgressPage pp)
                success = await pp.StartInstallAsync();
            _isInstalling = false;
            if (success) NavigateTo(6);
            return;
        }

        if (_currentPage == 3 && _pages[4] is SummaryPage sp)
            sp.Refresh(_options);

        NavigateTo(_currentPage + 1);
    }

    public void UnlockAfterError()
    {
        _isInstalling = false;
        BackButton.Visibility = Visibility.Visible;
        BackButton.Content    = Loc.T("inst_Back");
        NextButton.Visibility = Visibility.Hidden;
    }

    // ── UAC helpers ──────────────────────────────────────────────────────────

    private static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool NeedsAdminForPath(string path)
    {
        foreach (var folder in new[]
        {
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.MyDocuments,
        })
        {
            var folderPath = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(folderPath) &&
                path.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var folder in new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.SystemX86,
        })
        {
            var folderPath = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(folderPath) &&
                path.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        try
        {
            var testDir = path;
            while (!string.IsNullOrEmpty(testDir) && !Directory.Exists(testDir))
                testDir = System.IO.Path.GetDirectoryName(testDir) ?? "";

            if (!string.IsNullOrEmpty(testDir))
            {
                var testFile = System.IO.Path.Combine(testDir, $"._clipnotes_probe_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return false;
            }
        }
        catch { }

        return true;
    }

    private void RestartAsAdmin()
    {
        var tempFile = System.IO.Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(_options));

        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                  ?? Environment.ProcessPath!;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = $"--options \"{tempFile}\" --direct-install",
                Verb            = "runas",
                UseShellExecute = true,
            });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            File.Delete(tempFile);
            MessageBox.Show(
                $"{Loc.T("inst_UacError")}\n{ex.Message}",
                "ClipNotes Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isInstalling)
        {
            var result = MessageBox.Show(
                Loc.T("inst_AbortConfirm"),
                "ClipNotes Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnClosing(e);
    }
}
