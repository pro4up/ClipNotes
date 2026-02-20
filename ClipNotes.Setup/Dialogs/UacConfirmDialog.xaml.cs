using System.Windows;

namespace ClipNotes.Setup.Dialogs;

public partial class UacConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public UacConfirmDialog(string installPath)
    {
        InitializeComponent();
        UacTitleText.Text  = Loc.T("inst_UacTitle");
        MessageText.Text   = Loc.T("inst_UacMessage", installPath);
        CancelButton.Content  = Loc.T("inst_UacCancel");
        ConfirmButton.Content = Loc.T("inst_UacConfirm");
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
