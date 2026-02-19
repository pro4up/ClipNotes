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

    private readonly List<(string Name, bool ShowInIndicator)> _pageNames =
    [
        ("Добро пожаловать", false),
        ("Параметры",        true),
        ("Обработчик",       true),
        ("Модель",           true),
        ("Установка",        true),
        ("Готово",           false),
    ];

    public MainWindow(InstallOptions? options = null, bool autoStartInstall = false)
    {
        if (options != null) _options = options;
        _autoStartInstall = autoStartInstall;
        InitializeComponent();
        BuildPages();
        BuildStepIndicator();
        NavigateTo(0, animate: false);
        if (autoStartInstall) Loaded += OnAutoStartInstall;
    }

    private async void OnAutoStartInstall(object sender, RoutedEventArgs e)
    {
        Loaded -= OnAutoStartInstall;
        NavigateTo(4, animate: false);
        _isInstalling = true;
        bool success = false;
        if (_pages[4] is ProgressPage pp)
            success = await pp.StartInstallAsync();
        _isInstalling = false;
        if (success) NavigateTo(5);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDwmTheme(this);
    }

    private void BuildPages()
    {
        _pages.Add(new WelcomePage(_options));
        _pages.Add(new OptionsPage(_options));
        _pages.Add(new BackendPage(_options));
        _pages.Add(new ModelPage(_options));
        _pages.Add(new ProgressPage(_options));
        _pages.Add(new FinishPage(_options));
    }

    private void BuildStepIndicator()
    {
        StepIndicator.Children.Clear();
        for (int i = 0; i < _pageNames.Count; i++)
        {
            if (!_pageNames[i].ShowInIndicator) continue;
            var ellipse = new Ellipse
            {
                Width = 7, Height = 7,
                Margin = new Thickness(4, 0, 4, 0),
                Tag = i,
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
        bool isProgress = _currentPage == 4;
        bool isFinish   = _currentPage == 5;

        BackButton.Visibility = (isWelcome || isProgress || isFinish)
            ? Visibility.Hidden : Visibility.Visible;
        NextButton.Visibility = isProgress ? Visibility.Hidden : Visibility.Visible;

        NextButton.Content = (_currentPage == 0 || _currentPage == 3) ? "Установить"
                           : isFinish                                  ? "Готово"
                                                                       : "Далее";

        StepIndicator.Visibility = (isWelcome || isFinish) ? Visibility.Hidden : Visibility.Visible;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0 && !_isInstalling)
            NavigateTo(_currentPage - 1);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage == 5)
        {
            if (_pages[5] is FinishPage fp && fp.ShouldLaunch)
            {
                var exePath = System.IO.Path.Combine(_options.InstallPath, "ClipNotes.exe");
                if (System.IO.File.Exists(exePath))
                    System.Diagnostics.Process.Start(exePath);
            }
            Application.Current.Shutdown();
            return;
        }

        if (_currentPage == 3)
        {
            // Проверяем, нужны ли права Admin для выбранного пути
            if (NeedsAdminForPath(_options.InstallPath) && !IsAdmin())
            {
                var dlg = new UacConfirmDialog(_options.InstallPath) { Owner = this };
                dlg.ShowDialog();
                if (dlg.Confirmed) RestartAsAdmin();
                return;
            }

            NavigateTo(4);
            _isInstalling = true;
            bool success = false;
            if (_pages[4] is ProgressPage pp)
                success = await pp.StartInstallAsync();
            _isInstalling = false;
            if (success)
                NavigateTo(5);
            return;
        }

        NavigateTo(_currentPage + 1);
    }

    /// <summary>Вызывается ProgressPage при ошибке — возвращает кнопку «Назад».</summary>
    public void UnlockAfterError()
    {
        _isInstalling = false;
        BackButton.Visibility = Visibility.Visible;
        BackButton.Content    = "Назад";
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
        // Пути внутри профиля пользователя — права не нужны
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

        // Системные папки — права нужны
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

        // Для всего остального — проверяем реально, можем ли создать папку
        try
        {
            var testDir = path;
            // Идём вверх по дереву, пока не найдём существующий родитель
            while (!string.IsNullOrEmpty(testDir) && !Directory.Exists(testDir))
                testDir = System.IO.Path.GetDirectoryName(testDir) ?? "";

            if (!string.IsNullOrEmpty(testDir))
            {
                var testFile = System.IO.Path.Combine(testDir, $"._clipnotes_probe_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return false; // Записать можно — права не нужны
            }
        }
        catch { }

        return true; // Не удалось записать — скорее всего нужны права
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
                $"Не удалось получить права администратора:\n{ex.Message}",
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
                "Установка ещё не завершена. Прервать?",
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
