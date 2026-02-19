using System.Reflection;
using System.Windows.Controls;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class WelcomePage : UserControl
{
    public WelcomePage(InstallOptions options)
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Версия {version?.Major}.{version?.Minor}.{version?.Build}";
    }
}
