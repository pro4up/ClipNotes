using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using ClipNotes.Setup.Models;
using Microsoft.Win32;

namespace ClipNotes.Setup.Services;

public class InstallerService
{
    private readonly InstallOptions _options;
    private readonly DownloadService _download = new();

    public event Action<string>? StepChanged;
    public event Action<double, string>? ProgressChanged;
    public event Action<string>? LogMessage;

    public const string AppReleaseUrl =
        "https://github.com/pro4up/ClipNotes/releases/latest/download/ClipNotes-portable.zip";
    private const string FfmpegUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string WhisperCpuUrl =
        "https://github.com/ggerganov/whisper.cpp/releases/latest/download/whisper-bin-x64.zip";
    private const string WhisperCudaUrl =
        "https://github.com/ggerganov/whisper.cpp/releases/latest/download/whisper-cublas-12.0.0-bin-x64.zip";
    private const string ModelBaseUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public InstallerService(InstallOptions options)
    {
        _options = options;
    }

    public async Task InstallAsync(CancellationToken ct = default)
    {
        var installDir = _options.InstallPath;
        var toolsDir   = Path.Combine(installDir, "tools");
        var modelsDir  = Path.Combine(installDir, "models");

        // 1. Создать папки
        SetStep(Loc.T("inst_StepCreateDir"));
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(toolsDir);
        Directory.CreateDirectory(modelsDir);
        Log($"Dir: {installDir}");

#if OFFLINE_BUILD
        SetStep(Loc.T("inst_StepExtractApp"));
        await ExtractEmbeddedBundleAsync(installDir, toolsDir, ct);
#else
        var localSrc = GetLocalSourceDir();
        if (localSrc != null)
            Log($"Local source: {localSrc}");

        // 2. Файлы приложения
        if (localSrc != null)
        {
            SetStep(Loc.T("inst_StepCopyApp"));
            await CopyAppFilesAsync(localSrc, installDir, toolsDir, ct);
        }
        else
        {
            SetStep(Loc.T("inst_StepDownloadApp"));
            await DownloadAppAsync(installDir, ct);

            // 3. FFmpeg
            SetStep(Loc.T("inst_StepDownloadFfmpeg"));
            await DownloadAndExtractZipAsync(FfmpegUrl, toolsDir, "ffmpeg", ct,
                entry => (entry.Name == "ffmpeg.exe" || entry.Name == "ffprobe.exe")
                         && entry.FullName.Contains("bin/"));

            // 4. whisper-cli
            SetStep(Loc.T("inst_StepDownloadWhisper", _options.Backend.ToUpper()));
            var whisperUrl = _options.Backend == "cuda" ? WhisperCudaUrl : WhisperCpuUrl;
            await DownloadAndExtractZipAsync(whisperUrl, toolsDir, "whisper", ct,
                entry => entry.Name == "whisper-cli.exe" || entry.Name.EndsWith(".dll"));
        }

        // 5. Модель
        var modelFileName = $"ggml-{_options.Model}.bin";
        var modelDest     = Path.Combine(modelsDir, modelFileName);
        if (File.Exists(modelDest))
        {
            Log($"Model already present: {modelFileName}");
            ProgressChanged?.Invoke(90, "");
        }
        else
        {
            var localModel = localSrc != null
                ? Path.Combine(localSrc, "models", modelFileName) : null;

            if (localModel != null && File.Exists(localModel))
            {
                SetStep(Loc.T("inst_StepCopyModel", _options.Model));
                await CopyFileWithProgressAsync(localModel, modelDest, ct);
            }
            else
            {
                SetStep(Loc.T("inst_StepDownloadModel", _options.Model));
                await DownloadFileAsync(ModelBaseUrl + modelFileName, modelDest, "Model", ct);
            }
        }
#endif

        // 6. Ярлык
        if (_options.CreateDesktopShortcut)
        {
            SetStep(Loc.T("inst_StepShortcut"));
            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ClipNotes.lnk"),
                Path.Combine(installDir, "ClipNotes.exe"),
                "ClipNotes");
        }

        // 7. Автозапуск
        if (_options.RunOnStartup)
        {
            SetStep(Loc.T("inst_StepStartup"));
            SetStartupRegistry(Path.Combine(installDir, "ClipNotes.exe"));
        }

        // 8. Регистрация
        SetStep(Loc.T("inst_StepRegister"));
        RegisterUninstaller(installDir);

        // 9. Сохранить язык в settings.json
        SaveLanguageToSettings();

        SetStep(Loc.T("inst_StepDone"));
        ProgressChanged?.Invoke(100, "");
    }

    private void SaveLanguageToSettings()
    {
        try
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipNotes");
            Directory.CreateDirectory(appDataDir);
            var settingsPath = Path.Combine(appDataDir, "settings.json");

            if (File.Exists(settingsPath))
            {
                var node = JsonNode.Parse(File.ReadAllText(settingsPath));
                if (node is JsonObject obj)
                {
                    obj["Language"] = _options.Language;
                    File.WriteAllText(settingsPath, obj.ToJsonString());
                    return;
                }
            }
            File.WriteAllText(settingsPath, $"{{\"Language\":\"{_options.Language}\"}}");
        }
        catch { }
    }

    // ── Локальный источник ──────────────────────────────────────────────────

    private static string? GetLocalSourceDir()
    {
        try
        {
            var exePath  = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath == null) return null;
            var setupDir = Path.GetDirectoryName(exePath);
            if (setupDir == null) return null;
            var candidate = Path.GetFullPath(Path.Combine(setupDir, "..", "app"));
            return Directory.Exists(candidate)
                   && File.Exists(Path.Combine(candidate, "ClipNotes.exe"))
                ? candidate : null;
        }
        catch { return null; }
    }

    private async Task CopyAppFilesAsync(
        string srcDir, string installDir, string toolsDir, CancellationToken ct)
    {
        var rootFiles = Directory.GetFiles(srcDir);
        for (int i = 0; i < rootFiles.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src  = rootFiles[i];
            var name = Path.GetFileName(src);
            File.Copy(src, Path.Combine(installDir, name), overwrite: true);
            Log($"  {name}");
            ProgressChanged?.Invoke((double)(i + 1) / rootFiles.Length * 50,
                $"{i + 1}/{rootFiles.Length}: {name}");
            await Task.Yield();
        }

        var srcTools = Path.Combine(srcDir, "tools");
        if (Directory.Exists(srcTools))
        {
            var toolFiles = Directory.GetFiles(srcTools);
            for (int i = 0; i < toolFiles.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(toolFiles[i]);
                File.Copy(toolFiles[i], Path.Combine(toolsDir, name), overwrite: true);
                Log($"  tools/{name}");
                ProgressChanged?.Invoke(50 + (double)(i + 1) / toolFiles.Length * 40,
                    $"tools/{name}");
                await Task.Yield();
            }
        }

        var srcLicenses = Path.Combine(srcDir, "licenses");
        if (Directory.Exists(srcLicenses))
        {
            var licDir = Path.Combine(installDir, "licenses");
            Directory.CreateDirectory(licDir);
            foreach (var f in Directory.GetFiles(srcLicenses))
                File.Copy(f, Path.Combine(licDir, Path.GetFileName(f)), overwrite: true);
        }

        // Copy lang/ directory (localization files)
        var srcLang = Path.Combine(srcDir, "lang");
        if (Directory.Exists(srcLang))
        {
            var langDest = Path.Combine(installDir, "lang");
            foreach (var langSubDir in Directory.GetDirectories(srcLang))
            {
                var langCode = Path.GetFileName(langSubDir);
                var destLangSubDir = Path.Combine(langDest, langCode);
                Directory.CreateDirectory(destLangSubDir);
                foreach (var f in Directory.GetFiles(langSubDir))
                {
                    File.Copy(f, Path.Combine(destLangSubDir, Path.GetFileName(f)), overwrite: true);
                    Log($"  lang/{langCode}/{Path.GetFileName(f)}");
                }
            }
        }

        ProgressChanged?.Invoke(90, "");
    }

    private async Task CopyFileWithProgressAsync(string src, string dest, CancellationToken ct)
    {
        var total = new FileInfo(src).Length;
        using var fsIn  = File.OpenRead(src);
        using var fsOut = File.Create(dest);
        var buf      = new byte[1 << 17];
        long copied  = 0;
        long lastBytes = 0;
        var  sw = System.Diagnostics.Stopwatch.StartNew();
        int  read;
        var  mb = Loc.T("inst_MB");
        while ((read = await fsIn.ReadAsync(buf, ct)) > 0)
        {
            await fsOut.WriteAsync(buf.AsMemory(0, read), ct);
            copied += read;
            if (sw.ElapsedMilliseconds > 250)
            {
                double elapsed = sw.Elapsed.TotalSeconds;
                double speed   = elapsed > 0 ? (copied - lastBytes) / elapsed / 1_048_576.0 : 0;
                lastBytes = copied;
                sw.Restart();
                double pct  = total > 0 ? (double)copied / total * 100 : 0;
                string info = $"{copied / 1_048_576.0:F1} {mb} / {total / 1_048_576.0:F1} {mb} — {speed:F1} {mb}/s";
                ProgressChanged?.Invoke(pct, info);
            }
        }
        ProgressChanged?.Invoke(total > 0 ? 100.0 : 0, "");
    }

    // ── Скачивание ──────────────────────────────────────────────────────────

    private async Task DownloadAppAsync(string installDir, CancellationToken ct)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            await DownloadFileAsync(AppReleaseUrl, tempZip, "ClipNotes", ct);
            SetStep(Loc.T("inst_StepExtract", "ClipNotes"));

            using var archive = ZipFile.OpenRead(tempZip);

            // Detect and strip common top-level folder prefix (e.g. "ClipNotes/")
            // The portable ZIP wraps files in a subfolder for user convenience,
            // but the installer must extract directly into installDir.
            var fileEntries = archive.Entries.Where(e => e.Length > 0).ToList();
            string? prefix = null;
            if (fileEntries.Count > 0 && fileEntries.All(e => e.FullName.Contains('/')))
            {
                var candidate = fileEntries[0].FullName[..(fileEntries[0].FullName.IndexOf('/') + 1)];
                if (fileEntries.All(e => e.FullName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)))
                    prefix = candidate;
            }

            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;
                var relPath = entry.FullName;
                if (prefix != null && relPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    relPath = relPath[prefix.Length..];
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                var destPath = Path.Combine(installDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
                Log($"  {relPath}");
            }
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private async Task DownloadAndExtractZipAsync(
        string url, string destDir, string label, CancellationToken ct,
        Func<ZipArchiveEntry, bool>? extractFilter = null)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            await DownloadFileAsync(url, tempZip, label, ct);
            SetStep(Loc.T("inst_StepExtract", label));
            using var archive = ZipFile.OpenRead(tempZip);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;
                if (extractFilter != null && !extractFilter(entry)) continue;
                var destPath = Path.Combine(destDir, entry.Name);
                entry.ExtractToFile(destPath, overwrite: true);
                Log($"  {entry.Name}");
            }
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private async Task DownloadFileAsync(
        string url, string destPath, string label, CancellationToken ct)
    {
        Log($"Download {label}: {url}");
        var progress = new Progress<DownloadProgress>(p =>
            ProgressChanged?.Invoke(p.Percent, p.Details));
        await _download.DownloadWithProgressAsync(url, destPath, progress, ct);
    }

    // ── OFFLINE ───────────────────────────────────────────────────────────────

#if OFFLINE_BUILD
    private static string GetOfflineBundlePath()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (exe == null) throw new FileNotFoundException("Could not determine installer path");
        var dir = Path.GetDirectoryName(exe)
            ?? throw new FileNotFoundException("Could not determine installer directory");
        var bundle = Path.Combine(dir, "ClipNotes-offline-bundle.zip");
        if (!File.Exists(bundle))
            throw new FileNotFoundException(
                $"ClipNotes-offline-bundle.zip not found in {dir}");
        return bundle;
    }

    private async Task ExtractEmbeddedBundleAsync(string installDir, string toolsDir, CancellationToken ct)
    {
        var bundlePath = GetOfflineBundlePath();
        Log($"Offline bundle: {bundlePath}");
        var modelsDir = Path.Combine(installDir, "models");
        var licDir    = Path.Combine(installDir, "licenses");

        using var archive = ZipFile.OpenRead(bundlePath);
        int count = archive.Entries.Count(e => e.Length > 0);
        int i = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0) continue;
            ct.ThrowIfCancellationRequested();

            var parts    = entry.FullName.Split('/');
            var folder   = parts[0];
            var fileName = parts.Length > 1 ? parts[^1] : "";
            if (string.IsNullOrEmpty(fileName)) { i++; continue; }

            string? dest = folder switch
            {
                "app"          => Path.Combine(installDir, fileName),
                "tools"        => Path.Combine(toolsDir,   fileName),
                "whisper-cpu"  => _options.Backend == "cpu"  ? Path.Combine(toolsDir, fileName) : null,
                "whisper-cuda" => _options.Backend == "cuda" ? Path.Combine(toolsDir, fileName) : null,
                "models"       => Path.Combine(modelsDir, fileName),
                "licenses"     => Path.Combine(licDir,    fileName),
                "lang"         => parts.Length >= 3
                                    ? Path.Combine(installDir, "lang", parts[1], fileName)
                                    : null,
                _              => null,
            };
            if (dest == null) { i++; continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
            i++;
            ProgressChanged?.Invoke((double)i / count * 100, $"{i}/{count}: {fileName}");
            if (i % 5 == 0) await Task.Yield();
        }
    }
#endif

    // ── Ярлыки и реестр ────────────────────────────────────────────────────

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        var esc = (string s) => s.Replace("'", "''");
        var script =
            $"$ws = New-Object -ComObject WScript.Shell; " +
            $"$s = $ws.CreateShortcut('{esc(shortcutPath)}'); " +
            $"$s.TargetPath = '{esc(targetPath)}'; " +
            $"$s.WorkingDirectory = '{esc(Path.GetDirectoryName(targetPath) ?? "")}'; " +
            $"$s.Description = '{esc(description)}'; " +
            $"$s.IconLocation = '{esc(targetPath)},0'; " +
            $"$s.Save()";

        Process.Start(new ProcessStartInfo
        {
            FileName  = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"",
            WindowStyle    = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
        })?.WaitForExit();
    }

    private static void SetStartupRegistry(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue("ClipNotes", $"\"{exePath}\" --tray");
    }

    private static void RegisterUninstaller(string installDir)
    {
        void Write(RegistryKey root)
        {
            using var k = root.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClipNotes");
            if (k == null) return;
            var uninstallerPath = Path.Combine(installDir, "ClipNotes.Uninstaller.exe");
            k.SetValue("DisplayName",     "ClipNotes");
            k.SetValue("DisplayVersion",  "1.0.0");
            k.SetValue("Publisher",       "pro4up");
            k.SetValue("InstallLocation", installDir);
            k.SetValue("DisplayIcon",     Path.Combine(installDir, "ClipNotes.exe"));
            k.SetValue("UninstallString", $"\"{uninstallerPath}\"");
            k.SetValue("NoModify",  1, RegistryValueKind.DWord);
            k.SetValue("NoRepair",  1, RegistryValueKind.DWord);
        }

        try       { Write(Registry.LocalMachine); }
        catch     { try { Write(Registry.CurrentUser); } catch { } }
    }

    private void SetStep(string step) { Log(step); StepChanged?.Invoke(step); }
    private void Log(string msg)      => LogMessage?.Invoke(msg);
}
