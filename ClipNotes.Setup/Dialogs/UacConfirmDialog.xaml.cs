using System.Windows;

namespace ClipNotes.Setup.Dialogs;

public partial class UacConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public UacConfirmDialog(string installPath)
    {
        InitializeComponent();
        MessageText.Text =
            $"Для установки в папку\n{installPath}\nнеобходимы права администратора.";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDwmTheme(this);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
