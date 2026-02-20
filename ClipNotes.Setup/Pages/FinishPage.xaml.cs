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

        FinishTitleText.Text = Loc.T("inst_FinishTitle");
        PathText.Text        = $"{Loc.T("inst_InstalledTo")} {_options.InstallPath}";
        LaunchCheck.Content  = Loc.T("inst_LaunchCheck");
    }

    private void LaunchCheck_Changed(object sender, RoutedEventArgs e)
    {
        _options.LaunchAfterInstall = LaunchCheck.IsChecked == true;
    }
}
