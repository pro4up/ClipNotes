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

        PageTitle.Text           = Loc.T("inst_OptionsTitle");
        PageSubtitle.Text        = Loc.T("inst_OptionsSubtitle");
        InstallFolderLabel.Text  = Loc.T("inst_InstallFolder");
        BrowseButton.Content     = Loc.T("inst_Browse");
        AdditionalLabel.Text     = Loc.T("inst_Additional");
        DesktopShortcutCheck.Content = Loc.T("inst_DesktopShortcut");
        StartupCheck.Content     = Loc.T("inst_RunOnStartup");
        SpaceText.Text           = Loc.T("inst_SpaceHint");

        PathBox.Text = _options.InstallPath;
        DesktopShortcutCheck.IsChecked = _options.CreateDesktopShortcut;
        StartupCheck.IsChecked = _options.RunOnStartup;
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
