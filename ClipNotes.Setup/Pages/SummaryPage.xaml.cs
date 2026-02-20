using System.Windows.Controls;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class SummaryPage : UserControl
{
    private const long AppMb       = 150;
    private const long FfmpegMb    = 100;
    private const long WhisperCpuMb  = 50;
    private const long WhisperCudaMb = 200;

    public SummaryPage(InstallOptions options)
    {
        InitializeComponent();

        PageTitle.Text        = Loc.T("inst_SummaryTitle");
        PageSubtitle.Text     = Loc.T("inst_SummarySubtitle");
        InstallPathLabel.Text = Loc.T("inst_InstallPath");
        BackendLabelText.Text = Loc.T("inst_BackendLabel");
        ModelLabelText.Text   = Loc.T("inst_ModelLabel");
        ExtrasLabelText.Text  = Loc.T("inst_Additional");
        DiskSizeLabelText.Text = Loc.T("inst_DiskSize");

        Populate(options);
    }

    public void Refresh(InstallOptions options) => Populate(options);

    private void Populate(InstallOptions options)
    {
        InstallPathText.Text = options.InstallPath;
        BackendText.Text = options.Backend == "cuda" ? "CUDA (NVIDIA)" : "CPU-BLAS";

        var model = ModelInfo.All.FirstOrDefault(m => m.Id == options.Model);
        ModelText.Text     = model?.DisplayName ?? options.Model;
        ModelSizeText.Text = model?.SizeMB ?? "";

        var extras = new List<string>();
        if (options.CreateDesktopShortcut) extras.Add(Loc.T("inst_DesktopShortcutShort"));
        if (options.RunOnStartup)          extras.Add(Loc.T("inst_RunOnStartupShort"));
        ExtrasText.Text = extras.Count > 0 ? string.Join(", ", extras) : Loc.T("inst_None");

        long modelMb  = model?.SizeMbApprox ?? 0;
        long toolsMb  = FfmpegMb + (options.Backend == "cuda" ? WhisperCudaMb : WhisperCpuMb);
        long totalMb  = AppMb + toolsMb + modelMb;

        DiskBreakdownText.Text = Loc.T("inst_DiskBreakdown", AppMb, toolsMb, modelMb);

        var mb = Loc.T("inst_MB");
        var gb = Loc.T("inst_GB");
        TotalSizeText.Text = totalMb >= 1024
            ? $"~{totalMb / 1024.0:F1} {gb}"
            : $"~{totalMb} {mb}";
    }
}
