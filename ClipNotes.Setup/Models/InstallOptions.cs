namespace ClipNotes.Setup.Models;

public class InstallOptions
{
    public string InstallPath { get; set; } = DefaultInstallPath();
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public string Backend { get; set; } = "cpu";   // "cpu" | "cuda"
    public string Model { get; set; } = "large-v3-turbo";
    public bool LaunchAfterInstall { get; set; } = true;

    private static string DefaultInstallPath()
    {
        // Пробуем Program Files — если есть права, иначе LocalAppData
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(pf, "ClipNotes");
    }
}

public static class ModelInfo
{
    public record Model(string Id, string DisplayName, string SizeMB, string VramGB, string Accuracy, bool Recommended);

    public static readonly Model[] All =
    [
        new("base",            "Base",            "141 MB",  "~1 GB",  "Низкая",      false),
        new("small",           "Small",           "244 MB",  "~2 GB",  "Средняя",     false),
        new("medium",          "Medium",          "769 MB",  "~5 GB",  "Хорошая",     false),
        new("large-v3-turbo",  "Large-v3-turbo",  "1.6 GB",  "~6 GB",  "Высокая",     true),
        new("large-v3",        "Large-v3",        "3.1 GB",  "~10 GB", "Максимальная", false),
    ];
}
