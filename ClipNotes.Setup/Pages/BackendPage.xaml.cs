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

        PageTitle.Text          = Loc.T("inst_BackendTitle");
        PageSubtitle.Text       = Loc.T("inst_BackendSubtitle");
        CpuDescText.Text        = Loc.T("inst_CpuDesc");
        CpuRecommendedText.Text = Loc.T("inst_Recommended");
        CudaDescText.Text       = Loc.T("inst_CudaDesc");

        UpdateSelection();
    }

    private void CpuCard_Click(object sender, RoutedEventArgs e)
    {
        _options.Backend = "cpu";
        UpdateSelection();
    }

    private void CudaCard_Click(object sender, RoutedEventArgs e)
    {
        _options.Backend = "cuda";
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        bool isCpu = _options.Backend == "cpu";
        CpuCard.Style  = (Style)FindResource(isCpu  ? "CardButtonSelectedStyle" : "CardButtonStyle");
        CudaCard.Style = (Style)FindResource(!isCpu ? "CardButtonSelectedStyle" : "CardButtonStyle");
    }
}
