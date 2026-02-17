using System.Windows;
using System.Windows.Media;
using ClipNotes.Services;

namespace ClipNotes;

public partial class App : Application
{
    public static bool IsDark { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            LogService.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show($"Неожиданная ошибка:\n{args.Exception.Message}\n\nПодробности в Logs/",
                "ClipNotes — Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogService.Error("Fatal unhandled exception", ex);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogService.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    public static void ApplyTheme(bool dark)
    {
        IsDark = dark;
        var r = Current.Resources;

        if (dark)
        {
            r["BgBrush"]           = Brush(0x1C, 0x1C, 0x1E);
            r["CardBrush"]         = Brush(0x2C, 0x2C, 0x2E);
            r["AccentBrush"]       = Brush(0x0A, 0x84, 0xFF);
            r["DangerBrush"]       = Brush(0xFF, 0x45, 0x3A);
            r["SuccessBrush"]      = Brush(0x30, 0xD1, 0x58);
            r["TextPrimaryBrush"]  = Brush(0xFF, 0xFF, 0xFF);
            r["TextSecondaryBrush"]= Brush(0xAE, 0xAE, 0xB2);
            r["BorderBrush"]       = Brush(0x3A, 0x3A, 0x3C);
            r["SeparatorBrush"]    = Brush(0x48, 0x48, 0x4A);
            r["InputBgBrush"]      = Brush(0x3A, 0x3A, 0x3C);
            r["InfoBannerBgBrush"] = Brush(0x0A, 0x28, 0x40);
            r["InfoBannerBorderBrush"] = Brush(0x1A, 0x4A, 0x7A);
            r["InfoBannerFgBrush"] = Brush(0x7E, 0xC8, 0xF4);
        }
        else
        {
            r["BgBrush"]           = Brush(0xFA, 0xFA, 0xFA);
            r["CardBrush"]         = Brush(0xFF, 0xFF, 0xFF);
            r["AccentBrush"]       = Brush(0x00, 0x7A, 0xFF);
            r["DangerBrush"]       = Brush(0xFF, 0x3B, 0x30);
            r["SuccessBrush"]      = Brush(0x34, 0xC7, 0x59);
            r["TextPrimaryBrush"]  = Brush(0x1C, 0x1C, 0x1E);
            r["TextSecondaryBrush"]= Brush(0x8E, 0x8E, 0x93);
            r["BorderBrush"]       = Brush(0xE5, 0xE5, 0xEA);
            r["SeparatorBrush"]    = Brush(0xF2, 0xF2, 0xF7);
            r["InputBgBrush"]      = Brush(0xFF, 0xFF, 0xFF);
            r["InfoBannerBgBrush"] = Brush(0xF0, 0xF7, 0xFF);
            r["InfoBannerBorderBrush"] = Brush(0xB0, 0xD4, 0xF0);
            r["InfoBannerFgBrush"] = Brush(0x1A, 0x4A, 0x7A);
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
        => new(Color.FromRgb(r, g, b));
}
