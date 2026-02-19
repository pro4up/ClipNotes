using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
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
        SetStep("Создание папки установки...");
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(toolsDir);
        Directory.CreateDirectory(modelsDir);
        Log($"Папка: {installDir}");

#if OFFLINE_BUILD
        SetStep("Распаковка файлов приложения...");
        await ExtractEmbeddedBundleAsync(installDir, ct);
#else
        // Ищем локальный app/ рядом с Setup.exe
        var localSrc = GetLocalSourceDir();
        if (localSrc != null)
            Log($"Локальный источник: {localSrc}");

        // 2. Файлы приложения
        if (localSrc != null)
        {
            SetStep("Копирование файлов приложения...");
            await CopyAppFilesAsync(localSrc, installDir, toolsDir, ct);
        }
        else
        {
            SetStep("Загрузка ClipNotes...");
            await DownloadAppAsync(installDir, ct);

            // 3. FFmpeg
            SetStep("Загрузка FFmpeg...");
            await DownloadAndExtractZipAsync(FfmpegUrl, toolsDir, "ffmpeg", ct,
                entry => (entry.Name == "ffmpeg.exe" || entry.Name == "ffprobe.exe")
                         && entry.FullName.Contains("bin/"));

            // 4. whisper-cli
            SetStep($"Загрузка whisper-cli ({_options.Backend.ToUpper()})...");
            var whisperUrl = _options.Backend == "cuda" ? WhisperCudaUrl : WhisperCpuUrl;
            await DownloadAndExtractZipAsync(whisperUrl, toolsDir, "whisper", ct,
                entry => entry.Name == "whisper-cli.exe" || entry.Name.EndsWith(".dll"));
        }

        // 5. Модель (локально или скачать)
        var modelFileName = $"ggml-{_options.Model}.bin";
        var modelDest     = Path.Combine(modelsDir, modelFileName);
        if (File.Exists(modelDest))
        {
            Log($"Модель уже на месте: {modelFileName}");
            ProgressChanged?.Invoke(90, "");
        }
        else
        {
            var localModel = localSrc != null
                ? Path.Combine(localSrc, "models", modelFileName) : null;

            if (localModel != null && File.Exists(localModel))
            {
                SetStep($"Копирование модели {_options.Model}...");
                await CopyFileWithProgressAsync(localModel, modelDest, ct);
            }
            else
            {
                SetStep($"Загрузка модели {_options.Model}...");
                await DownloadFileAsync(ModelBaseUrl + modelFileName, modelDest, "Модель", ct);
            }
        }
#endif

        // 6. Ярлык на рабочем столе
        if (_options.CreateDesktopShortcut)
        {
            SetStep("Создание ярлыка...");
            CreateShortcut(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ClipNotes.lnk"),
                Path.Combine(installDir, "ClipNotes.exe"),
                "ClipNotes — таймкодированные заметки");
            Log("Ярлык на рабочем столе создан");
        }

        // 7. Автозапуск
        if (_options.RunOnStartup)
        {
            SetStep("Настройка автозапуска...");
            SetStartupRegistry(Path.Combine(installDir, "ClipNotes.exe"));
            Log("Автозапуск настроен");
        }

        // 8. Запись в Add/Remove Programs
        SetStep("Регистрация в системе...");
        RegisterUninstaller(installDir);
        Log("Приложение зарегистрировано в системе");

        SetStep("Установка завершена!");
        ProgressChanged?.Invoke(100, "");
    }

    // ── Локальный источник ──────────────────────────────────────────────────

    /// <summary>
    /// Ищет ../app/ рядом с Setup.exe.
    /// Возвращает путь если там есть ClipNotes.exe, иначе null.
    /// </summary>
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

    /// <summary>Копирует exe/dll/json из srcDir и tools/ в installDir.</summary>
    private async Task CopyAppFilesAsync(
        string srcDir, string installDir, string toolsDir, CancellationToken ct)
    {
        // Корень: exe, dll, json
        var rootFiles = Directory.GetFiles(srcDir);
        for (int i = 0; i < rootFiles.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var src  = rootFiles[i];
            var name = Path.GetFileName(src);
            File.Copy(src, Path.Combine(installDir, name), overwrite: true);
            Log($"  {name}");
            ProgressChanged?.Invoke((double)(i + 1) / rootFiles.Length * 50,
                $"Скопировано {i + 1}/{rootFiles.Length}: {name}");
            await Task.Yield();
        }

        // tools/
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
                    $"Скопировано tools/{name}");
                await Task.Yield();
            }
        }

        // licenses/
        var srcLicenses = Path.Combine(srcDir, "licenses");
        if (Directory.Exists(srcLicenses))
        {
            var licDir = Path.Combine(installDir, "licenses");
            Directory.CreateDirectory(licDir);
            foreach (var f in Directory.GetFiles(srcLicenses))
                File.Copy(f, Path.Combine(licDir, Path.GetFileName(f)), overwrite: true);
        }

        ProgressChanged?.Invoke(90, "Файлы приложения скопированы");
    }

    /// <summary>Копирует один файл с отчётом прогресса.</summary>
    private async Task CopyFileWithProgressAsync(string src, string dest, CancellationToken ct)
    {
        var total = new FileInfo(src).Length;
        using var fsIn  = File.OpenRead(src);
        using var fsOut = File.Create(dest);
        var buf      = new byte[1 << 17]; // 128 KB
        long copied  = 0;
        long lastBytes = 0;
        var  sw = System.Diagnostics.Stopwatch.StartNew();
        int  read;
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
                string info = $"{copied / 1_048_576.0:F1} МБ / {total / 1_048_576.0:F1} МБ — {speed:F1} МБ/с";
                ProgressChanged?.Invoke(pct, info);
            }
        }
        double finalPct = total > 0 ? 100.0 : 0;
        ProgressChanged?.Invoke(finalPct, $"{total / 1_048_576.0:F1} МБ — готово");
    }

    // ── Скачивание ──────────────────────────────────────────────────────────

    private async Task DownloadAppAsync(string installDir, CancellationToken ct)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            await DownloadFileAsync(AppReleaseUrl, tempZip, "ClipNotes", ct);
            SetStep("Распаковка ClipNotes...");
            ZipFile.ExtractToDirectory(tempZip, installDir, overwriteFiles: true);
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
            SetStep($"Распаковка {label}...");
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
        Log($"Загрузка {label}: {url}");
        var progress = new Progress<DownloadProgress>(p =>
            ProgressChanged?.Invoke(p.Percent, p.Details));
        await _download.DownloadWithProgressAsync(url, destPath, progress, ct);
    }

    // ── OFFLINE (embedded bundle) ───────────────────────────────────────────

#if OFFLINE_BUILD
    private async Task ExtractEmbeddedBundleAsync(string installDir, CancellationToken ct)
    {
        var asm = Assembly.GetExecutingAssembly();
        const string res = "ClipNotes.Setup.Resources.app-bundle.zip";
        using var stream = asm.GetManifestResourceStream(res)
            ?? throw new InvalidOperationException("Встроенный bundle не найден");

        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            using (var fs = File.Create(tempZip))
                await stream.CopyToAsync(fs, ct);

            using var archive = ZipFile.OpenRead(tempZip);
            int count = archive.Entries.Count;
            int i = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;
                var dest = Path.Combine(installDir,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
                i++;
                ProgressChanged?.Invoke((double)i / count * 100,
                    $"Распаковано {i}/{count} файлов");
                if (i % 10 == 0) await Task.Yield();
            }
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }
#endif

    // ── Ярлыки и реестр ────────────────────────────────────────────────────

    private static void CreateShortcut(
        string shortcutPath, string targetPath, string description)
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
        key?.SetValue("ClipNotes", $"\"{exePath}\" --minimized");
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
        catch     { try { Write(Registry.CurrentUser); } catch { /* нет прав */ } }
    }

    private void SetStep(string step) { Log(step); StepChanged?.Invoke(step); }
    private void Log(string msg)      => LogMessage?.Invoke(msg);
}
