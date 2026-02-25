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

    private const string AppVersion = "1.0.0";

    public event Action<string>? StepChanged;
    public event Action<double, string>? ProgressChanged;
    public event Action<string>? LogMessage;

    public const string AppReleaseUrl =
        "https://github.com/pro4up/ClipNotes/releases/latest/download/ClipNotes-bundle.zip";
    private const string AppSha256SumsUrl =
        "https://github.com/pro4up/ClipNotes/releases/latest/download/SHA256SUMS.txt";
    private const string FfmpegUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string WhisperCpuUrl =
        "https://github.com/ggerganov/whisper.cpp/releases/latest/download/whisper-blas-bin-x64.zip";
    private const string WhisperCudaUrl =
        "https://github.com/ggerganov/whisper.cpp/releases/latest/download/whisper-cublas-12.4.0-bin-x64.zip";
    private const string ModelBaseUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    // Sanity bounds for third-party downloads (not cryptographic, but catch obviously wrong files).
    private const long FfmpegMinBytes   =  40L * 1024 * 1024; //  40 MB minimum
    private const long FfmpegMaxBytes   = 400L * 1024 * 1024; // 400 MB maximum
    private const long WhisperMinBytes  =   5L * 1024 * 1024; //   5 MB minimum
    private const long WhisperMaxBytes  = 500L * 1024 * 1024; // 500 MB maximum

    public InstallerService(InstallOptions options)
    {
        _options = options;
    }

    public async Task InstallAsync(CancellationToken ct = default)
    {
        var installDir = _options.InstallPath;
        var appDir     = Path.Combine(installDir, "app");
        var toolsDir   = Path.Combine(installDir, "tools");
        var modelsDir  = Path.Combine(installDir, "models");

        // 1. Создать папки
        SetStep(Loc.T("inst_StepCreateDir"));
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(toolsDir);
        Directory.CreateDirectory(modelsDir);
        Log($"Dir: {installDir}");

#if OFFLINE_BUILD
        SetStep(Loc.T("inst_StepExtractApp"));
        await ExtractEmbeddedBundleAsync(installDir, appDir, toolsDir, ct);
#else
        var localSrc = GetLocalSourceDir();
        if (localSrc != null)
            Log($"Local source: {localSrc}");

        // 2. Файлы приложения
        if (localSrc != null)
        {
            SetStep(Loc.T("inst_StepCopyApp"));
            await CopyAppFilesAsync(localSrc, appDir, installDir, ct);
        }
        else
        {
            SetStep(Loc.T("inst_StepDownloadApp"));
            await DownloadAppAsync(installDir, ct);

            // 3. FFmpeg
            SetStep(Loc.T("inst_StepDownloadFfmpeg"));
            await DownloadAndExtractZipAsync(FfmpegUrl, toolsDir, "ffmpeg", ct,
                entry => (entry.Name == "ffmpeg.exe" || entry.Name == "ffprobe.exe")
                         && entry.FullName.Contains("bin/"),
                minBytes: FfmpegMinBytes, maxBytes: FfmpegMaxBytes);

            // 4. whisper-cli
            SetStep(Loc.T("inst_StepDownloadWhisper", _options.Backend.ToUpper()));
            var whisperUrl = _options.Backend == "cuda" ? WhisperCudaUrl : WhisperCpuUrl;
            await DownloadAndExtractZipAsync(whisperUrl, toolsDir, "whisper", ct,
                entry => entry.Name == "whisper-cli.exe" || entry.Name.EndsWith(".dll"),
                minBytes: WhisperMinBytes, maxBytes: WhisperMaxBytes);
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

        // 6. Ярлыки (Desktop + Start Menu)
        var exePath = Path.Combine(appDir, "ClipNotes.exe");
        if (_options.CreateDesktopShortcut)
        {
            SetStep(Loc.T("inst_StepShortcut"));
            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ClipNotes.lnk"),
                exePath, "ClipNotes");
        }

        // Start Menu shortcut
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "ClipNotes");
        Directory.CreateDirectory(startMenuDir);
        CreateShortcut(Path.Combine(startMenuDir, "ClipNotes.lnk"), exePath, "ClipNotes");

        // 7. Автозапуск
        if (_options.RunOnStartup)
        {
            SetStep(Loc.T("inst_StepStartup"));
            SetStartupRegistry(exePath);
        }

        // 8. Регистрация
        SetStep(Loc.T("inst_StepRegister"));
        RegisterUninstaller(installDir, appDir);

        // 9. Сохранить настройки в settings.json (Language, WhisperModel, StartWithWindows)
        SaveSettingsToAppData();

        SetStep(Loc.T("inst_StepDone"));
        ProgressChanged?.Invoke(100, "");
    }

    private void SaveSettingsToAppData()
    {
        try
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipNotes");
            Directory.CreateDirectory(appDataDir);
            var settingsPath = Path.Combine(appDataDir, "settings.json");

            JsonObject obj;
            if (File.Exists(settingsPath))
            {
                var node = JsonNode.Parse(File.ReadAllText(settingsPath));
                obj = node as JsonObject ?? new JsonObject();
            }
            else
            {
                obj = new JsonObject();
            }

            // Allowlist Language and Model before writing to settings.json — the values come from
            // the options temp file in UAC-elevated mode, so validate before persisting.
            var validLangs   = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ru", "en" };
            var validModels  = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "base", "small", "medium", "large-v3", "large-v3-turbo" };

            obj["Language"]         = validLangs.Contains(_options.Language) ? _options.Language : "en";
            obj["WhisperModel"]     = validModels.Contains(_options.Model) ? _options.Model : "large-v3-turbo";
            obj["StartWithWindows"] = _options.RunOnStartup;
            File.WriteAllText(settingsPath, obj.ToJsonString());
        }
        catch (Exception ex) { Log($"SaveSettings error: {ex.Message}"); }
    }

    // ── Локальный источник ──────────────────────────────────────────────────

    private string? GetLocalSourceDir()
    {
        try
        {
            var exePath  = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath == null) return null;
            var setupDir = Path.GetDirectoryName(exePath);
            if (setupDir == null) return null;
            // Build root is at setupDir/../app; app files are in setupDir/../app/app/
            var buildRoot = Path.GetFullPath(Path.Combine(setupDir, "..", "app"));
            var appSubDir = Path.Combine(buildRoot, "app");
            return Directory.Exists(appSubDir)
                   && File.Exists(Path.Combine(appSubDir, "ClipNotes.exe"))
                ? appSubDir : null;
        }
        catch (Exception ex) { Log($"GetLocalSourceDir error: {ex.Message}"); return null; }
    }

    private async Task CopyAppFilesAsync(
        string srcAppDir, string destAppDir, string destRootDir, CancellationToken ct)
    {
        var srcRoot = Path.GetFullPath(Path.Combine(srcAppDir, ".."));

        await CopyFlatFilesAsync(srcAppDir, destAppDir, "app", 0, 40, ct);
        await CopyFlatFilesAsync(
            Path.Combine(srcRoot, "tools"), Path.Combine(destRootDir, "tools"), "tools", 40, 35, ct);

        var srcLicenses = Path.Combine(srcRoot, "licenses");
        if (Directory.Exists(srcLicenses))
        {
            var licDir = Path.Combine(destRootDir, "licenses");
            Directory.CreateDirectory(licDir);
            foreach (var f in Directory.GetFiles(srcLicenses))
                File.Copy(f, Path.Combine(licDir, Path.GetFileName(f)), overwrite: true);
        }

        CopyLangDir(Path.Combine(srcRoot, "lang"), Path.Combine(destRootDir, "lang"));
        ProgressChanged?.Invoke(90, "");
    }

    private async Task CopyFlatFilesAsync(
        string srcDir, string destDir, string logLabel,
        double progressStart, double progressRange, CancellationToken ct)
    {
        if (!Directory.Exists(srcDir)) return;
        Directory.CreateDirectory(destDir);
        var files = Directory.GetFiles(srcDir);
        for (int i = 0; i < files.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(files[i]);
            File.Copy(files[i], Path.Combine(destDir, name), overwrite: true);
            Log($"  {logLabel}/{name}");
            if (progressRange > 0)
                ProgressChanged?.Invoke(
                    progressStart + (double)(i + 1) / files.Length * progressRange,
                    $"{i + 1}/{files.Length}: {name}");
            await Task.Yield();
        }
    }

    private void CopyLangDir(string srcLang, string destLang)
    {
        if (!Directory.Exists(srcLang)) return;
        foreach (var langSubDir in Directory.GetDirectories(srcLang))
        {
            var langCode  = Path.GetFileName(langSubDir);
            var destSubDir = Path.Combine(destLang, langCode);
            Directory.CreateDirectory(destSubDir);
            foreach (var f in Directory.GetFiles(langSubDir))
            {
                File.Copy(f, Path.Combine(destSubDir, Path.GetFileName(f)), overwrite: true);
                Log($"  lang/{langCode}/{Path.GetFileName(f)}");
            }
        }
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

    // Fetches SHA256SUMS.txt from our release and verifies the downloaded bundle.
    private async Task VerifyAppBundleSha256Async(string zipPath, CancellationToken ct)
    {
        var tempSums = Path.GetTempFileName();
        try
        {
            Log("[SHA256] Fetching SHA256SUMS.txt...");
            await _download.DownloadWithProgressAsync(AppSha256SumsUrl, tempSums, null, ct,
                maxBytes: 64 * 1024); // SHA256SUMS is a tiny text file

            var lines = File.ReadAllLines(tempSums);
            // Format: "HASH  filename" (sha256sum standard)
            var expected = lines
                .Select(l => l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length >= 2 &&
                            p[1].Equals("ClipNotes-bundle.zip", StringComparison.OrdinalIgnoreCase))
                .Select(p => p[0].ToUpperInvariant())
                .FirstOrDefault();

            if (expected == null)
            {
                Log("[SHA256] WARNING: ClipNotes-bundle.zip not found in SHA256SUMS.txt — skipping hash check.");
                return;
            }

            Log("[SHA256] Computing hash of downloaded bundle...");
            string actual;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(zipPath))
            {
                actual = BitConverter.ToString(await Task.Run(() => sha256.ComputeHash(fs), ct))
                    .Replace("-", "").ToUpperInvariant();
            }

            if (actual != expected)
                throw new InvalidOperationException(
                    $"ClipNotes bundle SHA-256 mismatch!\nExpected: {expected}\nGot:      {actual}");

            Log("[SHA256] ClipNotes bundle verified OK.");
        }
        finally
        {
            if (File.Exists(tempSums)) File.Delete(tempSums);
        }
    }

    private async Task DownloadAppAsync(string installDir, CancellationToken ct)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            await DownloadFileAsync(AppReleaseUrl, tempZip, "ClipNotes", ct);

            // Verify integrity before extracting — supply chain protection for our own bundle.
            SetStep(Loc.T("inst_StepVerify"));
            await VerifyAppBundleSha256Async(tempZip, ct);
            SetStep(Loc.T("inst_StepExtract", "ClipNotes"));

            using var archive = ZipFile.OpenRead(tempZip);

            // Detect and strip common top-level folder prefix (e.g. "ClipNotes/")
            // The bundle ZIP may wrap files in a subfolder;
            // the installer must extract directly into installDir.
            // Normalize backslashes → forward slashes (Windows PowerShell may produce backslash paths).
            var fileEntries = archive.Entries
                .Where(e => e.Length > 0)
                .Select(e => (entry: e, name: e.FullName.Replace('\\', '/')))
                .ToList();

            string? prefix = null;
            if (fileEntries.Count > 0 && fileEntries.All(e => e.name.Contains('/')))
            {
                var candidate = fileEntries[0].name[..(fileEntries[0].name.IndexOf('/') + 1)];
                if (fileEntries.All(e => e.name.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)))
                    prefix = candidate;
            }

            // Canonical base path for traversal guard (with trailing separator)
            var canonBase = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;

            foreach (var (entry, normalizedName) in fileEntries)
            {
                var relPath = normalizedName;
                if (prefix != null && relPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    relPath = relPath[prefix.Length..];
                if (string.IsNullOrWhiteSpace(relPath)) continue;

                var destPath = Path.GetFullPath(
                    Path.Combine(installDir, relPath.Replace('/', Path.DirectorySeparatorChar)));

                // Guard against path traversal (e.g. "../../Windows/evil.exe" in crafted ZIP)
                if (!destPath.StartsWith(canonBase, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  [BLOCKED] path traversal: {relPath}");
                    continue;
                }

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
        Func<ZipArchiveEntry, bool>? extractFilter = null,
        long minBytes = 0, long maxBytes = 0)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            await _download.DownloadWithProgressAsync(url, tempZip,
                new Progress<DownloadProgress>(p => ProgressChanged?.Invoke(p.Percent, p.Details)),
                ct, maxBytes: maxBytes);

            // Sanity-check downloaded file size
            var fileSize = new FileInfo(tempZip).Length;
            if (minBytes > 0 && fileSize < minBytes)
                throw new InvalidOperationException(
                    $"{label}: downloaded file too small ({fileSize / 1_048_576.0:F1} MB, expected ≥{minBytes / 1_048_576} MB)");

            SetStep(Loc.T("inst_StepExtract", label));
            SetStep(Loc.T("inst_StepExtract", label));

            Directory.CreateDirectory(destDir);
            var canonBase = Path.GetFullPath(destDir).TrimEnd(Path.DirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(tempZip);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;
                if (extractFilter != null && !extractFilter(entry)) continue;
                var destPath = Path.GetFullPath(Path.Combine(destDir, entry.Name));
                // Guard against path traversal in downloaded ZIPs
                if (!destPath.StartsWith(canonBase, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  [BLOCKED] path traversal: {entry.Name}");
                    continue;
                }
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

    private async Task ExtractEmbeddedBundleAsync(string installDir, string appDir, string toolsDir, CancellationToken ct)
    {
        var bundlePath = GetOfflineBundlePath();
        Log($"Offline bundle: {bundlePath}");
        var modelsDir = Path.Combine(installDir, "models");
        var licDir    = Path.Combine(installDir, "licenses");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(licDir);

        var canonBase = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(bundlePath);
        int count = archive.Entries.Count(e => e.Length > 0);
        int i = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0) continue;
            ct.ThrowIfCancellationRequested();

            // Normalize backslashes (Windows PowerShell may produce backslash ZIP entry names)
            var normalizedName = entry.FullName.Replace('\\', '/');
            var parts    = normalizedName.Split('/');
            var folder   = parts[0];
            var fileName = parts.Length > 1 ? parts[^1] : "";
            if (string.IsNullOrEmpty(fileName)) { i++; continue; }

            string? dest = folder switch
            {
                "app"          => Path.Combine(appDir,    fileName),
                "tools"        => Path.Combine(toolsDir,  fileName),
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

            // Guard against path traversal in embedded bundle
            var canonDest = Path.GetFullPath(dest);
            if (!canonDest.StartsWith(canonBase, StringComparison.OrdinalIgnoreCase))
            {
                Log($"  [BLOCKED] path traversal: {normalizedName}");
                i++; continue;
            }

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
        // Single-quoted PS strings: only ' needs escaping (as ''). Dollar signs and backticks
        // are treated literally — no variable expansion. Using -EncodedCommand bypasses outer
        // command-line quoting entirely, preventing any injection via path characters.
        var esc  = (string s) => s.Replace("'", "''");
        var work = Path.GetDirectoryName(targetPath) ?? "";
        var script = string.Join("\n",
            "$ws = New-Object -ComObject WScript.Shell",
            $"$s = $ws.CreateShortcut('{esc(shortcutPath)}')",
            $"$s.TargetPath = '{esc(targetPath)}'",
            $"$s.WorkingDirectory = '{esc(work)}'",
            $"$s.Description = '{esc(description)}'",
            $"$s.IconLocation = '{esc(targetPath)},0'",
            "$s.Save()"
        );
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        Process.Start(new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            WindowStyle     = ProcessWindowStyle.Hidden,
            CreateNoWindow  = true,
            UseShellExecute = false,
        })?.WaitForExit();
    }

    private static void SetStartupRegistry(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue("ClipNotes", $"\"{exePath}\" --tray");
    }

    private static void RegisterUninstaller(string installDir, string appDir)
    {
        void Write(RegistryKey root)
        {
            using var k = root.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClipNotes");
            if (k == null) return;
            var uninstallerPath = Path.Combine(appDir, "ClipNotes.Uninstaller.exe");
            k.SetValue("DisplayName",     "ClipNotes");
            k.SetValue("DisplayVersion",  AppVersion);
            k.SetValue("Publisher",       "pro4up");
            k.SetValue("InstallLocation", installDir);
            k.SetValue("DisplayIcon",     Path.Combine(appDir, "ClipNotes.exe"));
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
