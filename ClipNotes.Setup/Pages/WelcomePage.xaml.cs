using System.Reflection;
using System.Windows.Controls;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class WelcomePage : UserControl
{
    private readonly InstallOptions _options;
    private readonly Action<string> _onLanguageChanged;
    private bool _suppressLangChange;

    public WelcomePage(InstallOptions options, Action<string> onLanguageChanged)
    {
        _options = options;
        _onLanguageChanged = onLanguageChanged;
        InitializeComponent();

        _suppressLangChange = true;
        LangCombo.SelectedIndex = _options.Language == "en" ? 1 : 0;
        _suppressLangChange = false;

        ApplyLocalization();
    }

    public void ApplyLocalization()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text    = $"{Loc.T("inst_Version")} {version?.Major}.{version?.Minor}.{version?.Build}";
        WelcomeDescText.Text = Loc.T("inst_WelcomeDesc");
        WelcomeHintText.Text = Loc.T("inst_WelcomeHint");
        LangLabel.Text       = Loc.T("inst_LangLabel");
    }

    private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLangChange) return;
        var lang = (LangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "ru";
        _onLanguageChanged(lang);
    }
}
