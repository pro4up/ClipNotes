using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ClipNotes.Uninstaller;

public partial class App : Application
{
    public static bool IsDarkTheme { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Loc.Load(DetectLanguage());

        IsDarkTheme = DetectDarkTheme();
        if (IsDarkTheme) ApplyDarkTheme();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static string DetectLanguage()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipNotes", "settings.json");
            if (File.Exists(settingsPath))
            {
                var node = JsonNode.Parse(File.ReadAllText(settingsPath));
                if (node is JsonObject obj)
                {
                    var lang = obj["Language"]?.GetValue<string>();
                    if (lang == "en" || lang == "ru") return lang;
                }
            }
        }
        catch { }
        return "ru";
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

    private void ApplyDarkTheme()
    {
        static SolidColorBrush B(byte r, byte g, byte b) => new(System.Windows.Media.Color.FromRgb(r, g, b));
        var r = Current.Resources;
        r["BgBrush"]            = B(0x1C, 0x1C, 0x1E);
        r["CardBrush"]          = B(0x2C, 0x2C, 0x2E);
        r["AccentBrush"]        = B(0x0A, 0x84, 0xFF);
        r["TextPrimaryBrush"]   = new SolidColorBrush(Colors.White);
        r["TextSecondaryBrush"] = B(0xAE, 0xAE, 0xB2);
        r["CardBorderBrush"]    = B(0x3A, 0x3A, 0x3C);
        r["SeparatorBrush"]     = B(0x48, 0x48, 0x4A);
        r["DangerBrush"]        = B(0xFF, 0x45, 0x3A);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int v, int size);

    public static void ApplyDwmTheme(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            int color = IsDarkTheme ? 0x001E1E1E : 0x00F2F2F7;
            DwmSetWindowAttribute(hwnd, 35, ref color, sizeof(int));
        }
    }
}
