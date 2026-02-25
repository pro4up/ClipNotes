using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClipNotes.Setup.Models;
using Microsoft.Win32;

namespace ClipNotes.Setup;

public partial class App : Application
{
    public static bool IsDarkTheme { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"{Loc.T("inst_UnexpectedError")}\n{args.Exception.Message}",
                "ClipNotes Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        IsDarkTheme = DetectDarkTheme();
        if (IsDarkTheme)
            ApplyDarkThemeResources();

        // Обработка --options <file> (перезапуск с правами Admin)
        InstallOptions? preloaded = null;
        var args = e.Args;
        var idx = Array.IndexOf(args, "--options");
        if (idx >= 0 && idx + 1 < args.Length)
        {
            var file = args[idx + 1];
            if (File.Exists(file))
            {
                try
                {
                    preloaded = JsonSerializer.Deserialize<InstallOptions>(File.ReadAllText(file));
                    File.Delete(file);

                    // Validate install path before accepting options from temp file (HIGH-3):
                    // reject system directories that an attacker might substitute via race condition.
                    if (preloaded != null && !IsValidInstallPath(preloaded.InstallPath))
                        preloaded = null;
                }
                catch { /* дефолтные опции */ }
            }
        }

        bool directInstall = Array.IndexOf(e.Args, "--direct-install") >= 0;
        var window = preloaded != null
            ? new MainWindow(preloaded, autoStartInstall: directInstall)
            : new MainWindow();
        MainWindow = window;
        window.Show();
    }

    // Reject install paths pointing at OS system directories to mitigate TOCTOU temp-file attacks.
    // Note: the GUID-named temp file (see MainWindow.RestartAsAdmin) already narrows the race window;
    // this validation is the primary defense against a substituted options file pointing at a
    // dangerous install location (system dirs, UNC shares).
    private static bool IsValidInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path)) return false;

        // Reject UNC paths (\\server\share) — installation to network shares is unsupported
        // and consistent with the IsLocalPath() check used in MainViewModel.Navigation.cs.
        if (path.StartsWith(@"\\")) return false;

        var forbidden = new[]
        {
            Environment.SystemDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        };
        return !forbidden.Any(f =>
            !string.IsNullOrEmpty(f) && path.StartsWith(f, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DetectDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch { return false; }
    }

    private void ApplyDarkThemeResources()
    {
        static SolidColorBrush B(byte r, byte g, byte b) =>
            new(Color.FromRgb(r, g, b));

        var r = Current.Resources;
        r["BgBrush"]             = B(0x1C, 0x1C, 0x1E); // #1C1C1E
        r["CardBrush"]           = B(0x2C, 0x2C, 0x2E); // #2C2C2E
        r["AccentBrush"]         = B(0x0A, 0x84, 0xFF); // #0A84FF
        r["TextPrimaryBrush"]    = new SolidColorBrush(Colors.White);
        r["TextSecondaryBrush"]  = B(0xAE, 0xAE, 0xB2); // #AEAEB2
        r["CardBorderBrush"]     = B(0x3A, 0x3A, 0x3C); // #3A3A3C
        r["SeparatorBrush"]      = B(0x48, 0x48, 0x4A); // #48484A
        r["InputBgBrush"]        = B(0x3A, 0x3A, 0x3C); // #3A3A3C
        r["ScrollBarThumbBrush"] = B(0x48, 0x48, 0x4A); // #48484A
    }

    // ── DWM titlebar ─────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR           = 35;

    public static void ApplyDwmTheme(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int dark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // DWMWA_CAPTION_COLOR: только Windows 11 22000+
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            int color = IsDarkTheme ? 0x001E1E1E : 0x00F2F2F7; // BGR
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
        }
    }
}
