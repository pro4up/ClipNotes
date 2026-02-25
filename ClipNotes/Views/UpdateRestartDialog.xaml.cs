using System.Windows;
using ClipNotes.Helpers;

namespace ClipNotes.Views;

public partial class UpdateRestartDialog : Window
{
    public bool RestartNow { get; private set; }

    public UpdateRestartDialog(string version)
    {
        InitializeComponent();
        TitleText.Text   = LocalizationService.T("loc_UpdateReadyTitle");
        MessageText.Text = string.Format(LocalizationService.T("loc_UpdateReadyMsg"), version);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        App.ApplyTitleBarTheme(this, App.IsDark);
        RestartBtn.Focus();
    }

    private void RestartNow_Click(object sender, RoutedEventArgs e)
    {
        RestartNow = true;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        RestartNow = false;
        Close();
    }
}
