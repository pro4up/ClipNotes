using System.Windows;
using System.Windows.Controls;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class BackendPage : UserControl
{
    private readonly InstallOptions _options;

    public BackendPage(InstallOptions options)
    {
        _options = options;
        InitializeComponent();

#if OFFLINE_BUILD
        CudaCard.IsEnabled = false;
        CudaCard.Style = (Style)FindResource("CardButtonStyle");
        CudaUnavailableBadge.Visibility = Visibility.Visible;
        CudaSubtitle.Text = "Недоступно";
        _options.Backend = "cpu";
#endif

        UpdateSelection();
    }

    private void CpuCard_Click(object sender, RoutedEventArgs e)
    {
        _options.Backend = "cpu";
        UpdateSelection();
    }

    private void CudaCard_Click(object sender, RoutedEventArgs e)
    {
#if !OFFLINE_BUILD
        _options.Backend = "cuda";
        UpdateSelection();
#endif
    }

    private void UpdateSelection()
    {
        bool isCpu = _options.Backend == "cpu";
        CpuCard.Style = (Style)FindResource(isCpu ? "CardButtonSelectedStyle" : "CardButtonStyle");
#if !OFFLINE_BUILD
        CudaCard.Style = (Style)FindResource(!isCpu ? "CardButtonSelectedStyle" : "CardButtonStyle");
#endif
    }
}
