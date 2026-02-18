using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClipNotes.Services;
using ClipNotes.Views;

namespace ClipNotes;

public partial class App : Application
{
    public static bool IsDark { get; private set; }

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

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

        InitTrayIcon();

        bool startHidden = e.Args.Contains("--tray");

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;

        // Always Show first so Window.Loaded fires (hotkeys init), then hide if --tray
        _mainWindow.Show();
        if (startHidden)
            HideToTray();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    // Called by MainWindow.StateChanged when minimized and MinimizeToTray is on
    public void HideToTray()
    {
        _mainWindow?.Hide();
        if (_trayIcon != null)
            _trayIcon.Visible = true;
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        if (_trayIcon != null)
            _trayIcon.Visible = false;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    public static void ApplyTitleBarTheme(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int darkValue = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, sizeof(int));
            // Caption color (Windows 11 22000+ only)
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int color = dark ? 0x001E1E1E : unchecked((int)0x00F7F7F2); // BGR
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
            }
        }
        catch { }
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/tray.ico");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream != null) return new System.Drawing.Icon(stream);
        }
        catch { }
        try
        {
            var exeDir = System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? ".";
            var icoPath = System.IO.Path.Combine(exeDir, "ClipNotes.exe");
            if (System.IO.File.Exists(icoPath))
                return System.Drawing.Icon.ExtractAssociatedIcon(icoPath) ?? System.Drawing.SystemIcons.Application;
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    private void InitTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "ClipNotes",
            Visible = false
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (s, ev) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (s, ev) => Dispatcher.Invoke(() =>
        {
            _trayIcon!.Visible = false;
            Shutdown();
        }));

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, ev) => Dispatcher.Invoke(ShowMainWindow);
    }

    public static void ApplyTheme(bool dark)
    {
        IsDark = dark;
        var r = Current.Resources;
        // Apply title bar theme to all open windows
        foreach (Window w in Current.Windows)
            ApplyTitleBarTheme(w, dark);

        if (dark)
        {
            r["BgBrush"]               = Brush(0x1C, 0x1C, 0x1E);
            r["CardBrush"]             = Brush(0x2C, 0x2C, 0x2E);
            r["AccentBrush"]           = Brush(0x0A, 0x84, 0xFF);
            r["DangerBrush"]           = Brush(0xFF, 0x45, 0x3A);
            r["SuccessBrush"]          = Brush(0x30, 0xD1, 0x58);
            r["TextPrimaryBrush"]      = Brush(0xFF, 0xFF, 0xFF);
            r["TextSecondaryBrush"]    = Brush(0xAE, 0xAE, 0xB2);
            r["BorderBrush"]           = Brush(0x3A, 0x3A, 0x3C);
            r["SeparatorBrush"]        = Brush(0x48, 0x48, 0x4A);
            r["InputBgBrush"]          = Brush(0x3A, 0x3A, 0x3C);
            r["InfoBannerBgBrush"]     = Brush(0x0A, 0x28, 0x40);
            r["InfoBannerBorderBrush"] = Brush(0x1A, 0x4A, 0x7A);
            r["InfoBannerFgBrush"]     = Brush(0x7E, 0xC8, 0xF4);
            // Marker buttons — muted for dark
            r["BugBrush"]              = Brush(0xB9, 0x1C, 0x1C);
            r["TaskBrush"]             = Brush(0x1D, 0x4E, 0xD8);
            r["NoteBrush"]             = Brush(0x15, 0x80, 0x3D);
            // Scrollbar
            r["ScrollBarThumbBrush"]   = Brush(0x48, 0x48, 0x4A);
            r["ScrollBarTrackBrush"]   = Brush(0x2C, 0x2C, 0x2E);
        }
        else
        {
            r["BgBrush"]               = Brush(0xF2, 0xF2, 0xF7); // iOS gray6
            r["CardBrush"]             = Brush(0xFA, 0xFA, 0xFC);
            r["AccentBrush"]           = Brush(0x00, 0x7A, 0xFF);
            r["DangerBrush"]           = Brush(0xFF, 0x3B, 0x30);
            r["SuccessBrush"]          = Brush(0x34, 0xC7, 0x59);
            r["TextPrimaryBrush"]      = Brush(0x1C, 0x1C, 0x1E);
            r["TextSecondaryBrush"]    = Brush(0x8E, 0x8E, 0x93);
            r["BorderBrush"]           = Brush(0xD8, 0xD8, 0xDD);
            r["SeparatorBrush"]        = Brush(0xE8, 0xE8, 0xED);
            r["InputBgBrush"]          = Brush(0xFF, 0xFF, 0xFF);
            r["InfoBannerBgBrush"]     = Brush(0xE8, 0xF3, 0xFF);
            r["InfoBannerBorderBrush"] = Brush(0xA0, 0xC8, 0xEA);
            r["InfoBannerFgBrush"]     = Brush(0x1A, 0x4A, 0x7A);
            // Marker buttons — vivid for light
            r["BugBrush"]              = Brush(0xEF, 0x44, 0x44);
            r["TaskBrush"]             = Brush(0x3B, 0x82, 0xF6);
            r["NoteBrush"]             = Brush(0x22, 0xC5, 0x5E);
            // Scrollbar
            r["ScrollBarThumbBrush"]   = Brush(0xB0, 0xB0, 0xB8);
            r["ScrollBarTrackBrush"]   = Brush(0xF2, 0xF2, 0xF7);
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
        => new(Color.FromRgb(r, g, b));
}
