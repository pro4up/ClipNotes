using System.Windows;
using System.Windows.Controls;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class FinishPage : UserControl
{
    private readonly InstallOptions _options;
    public bool ShouldLaunch => LaunchCheck.IsChecked == true;

    public FinishPage(InstallOptions options)
    {
        _options = options;
        InitializeComponent();
        PathText.Text = $"Установлено в: {_options.InstallPath}";
    }

    private void LaunchCheck_Changed(object sender, RoutedEventArgs e)
    {
        _options.LaunchAfterInstall = LaunchCheck.IsChecked == true;
    }
}
