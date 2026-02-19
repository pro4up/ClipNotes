using System.Windows;
using System.Windows.Controls;
using ClipNotes.Setup.Helpers;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class OptionsPage : UserControl
{
    private readonly InstallOptions _options;

    public OptionsPage(InstallOptions options)
    {
        _options = options;
        InitializeComponent();
        PathBox.Text = _options.InstallPath;
        DesktopShortcutCheck.IsChecked = _options.CreateDesktopShortcut;
        StartupCheck.IsChecked = _options.RunOnStartup;
        UpdateSpaceText();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = FolderPicker.Browse(System.IO.Path.GetDirectoryName(PathBox.Text) ?? PathBox.Text);
        if (selected != null)
            PathBox.Text = System.IO.Path.Combine(selected, "ClipNotes");
    }

    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _options.InstallPath = PathBox.Text;
        UpdateSpaceText();
    }

    private void UpdateSpaceText()
    {
        // Базовый размер приложения без модели (офлайн ~450 MB, онлайн определяется моделью)
        SpaceText.Text = "Потребуется минимум ~300 МБ свободного места";
    }

    private void DesktopShortcutCheck_Changed(object sender, RoutedEventArgs e)
    {
        _options.CreateDesktopShortcut = DesktopShortcutCheck.IsChecked == true;
    }

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        _options.RunOnStartup = StartupCheck.IsChecked == true;
    }
}
