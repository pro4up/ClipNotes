using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ClipNotes.Uninstaller;

public partial class MainWindow : Window
{
    private const string RegKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClipNotes";

    private string? _installDir;

    public MainWindow()
    {
        InitializeComponent();

        Title                          = Loc.T("uninst_WindowTitle");
        ConfirmTitleText.Text          = Loc.T("uninst_Title");
        DeleteUserDataCheckBox.Content = Loc.T("uninst_DeleteUserData");
        CancelButton.Content           = Loc.T("uninst_Cancel");
        UninstallButton.Content        = Loc.T("uninst_Uninstall");
        CloseButton.Content            = Loc.T("uninst_Close");

        _installDir = ReadInstallDir();
        InfoText.Text = _installDir != null
            ? Loc.T("uninst_InfoText", _installDir)
            : Loc.T("uninst_InfoNoPath");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDwmTheme(this);
    }

    private static string? ReadInstallDir()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = root.OpenSubKey(RegKey);
                var val = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                    return val;
            }
            catch { }
        }
        return null;
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_installDir == null)
        {
            ShowResult(success: false, Loc.T("uninst_NoPath"));
            return;
        }

        try
        {
            foreach (var p in Process.GetProcessesByName("ClipNotes"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }

            var selfExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";

            var shortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "ClipNotes.lnk");
            if (File.Exists(shortcut)) File.Delete(shortcut);

            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                run?.DeleteValue("ClipNotes", throwOnMissingValue: false);
            }
            catch { }

            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try { root.DeleteSubKeyTree(RegKey, throwOnMissingSubKey: false); }
                catch { }
            }

            foreach (var file in Directory.GetFiles(_installDir, "*", SearchOption.AllDirectories))
            {
                if (file.Equals(selfExe, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); } catch { }
            }
            foreach (var dir in Directory.GetDirectories(_installDir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }

            var installDir = _installDir;
            Process.Start(new ProcessStartInfo
            {
                FileName  = "cmd.exe",
                Arguments = $"/c ping 127.0.0.1 -n 3 > nul " +
                            $"&& del /f /q \"{selfExe}\" " +
                            $"&& rmdir /s /q \"{installDir}\"",
                WindowStyle    = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            });

            if (DeleteUserDataCheckBox.IsChecked == true)
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClipNotes");
                if (Directory.Exists(appData))
                {
                    try { Directory.Delete(appData, recursive: true); } catch { }
                }
            }

            ShowResult(success: true, Loc.T("uninst_Done", _installDir));
        }
        catch (Exception ex)
        {
            ShowResult(success: false, Loc.T("uninst_Failed", ex.Message));
        }
    }

    private void ShowResult(bool success, string message)
    {
        ConfirmPanel.Visibility   = Visibility.Collapsed;
        ConfirmButtons.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility    = Visibility.Visible;
        CloseButton.Visibility    = Visibility.Visible;

        ResultText.Text    = success ? Loc.T("uninst_Success") : Loc.T("uninst_Error");
        ResultDetails.Text = message;

        if (!success)
            ResultText.Foreground = (System.Windows.Media.Brush)
                Application.Current.Resources["DangerBrush"];
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Close_Click(object sender, RoutedEventArgs e)  => Close();
}
