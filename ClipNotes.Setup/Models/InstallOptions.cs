namespace ClipNotes.Setup.Models;

public class InstallOptions
{
    public string InstallPath { get; set; } = DefaultInstallPath();
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public string Backend { get; set; } = "cpu";   // "cpu" | "cuda"
    public string Model { get; set; } = "large-v3-turbo";
    public bool LaunchAfterInstall { get; set; } = true;
    public string Language { get; set; } = "ru";

    private static string DefaultInstallPath()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(pf, "ClipNotes");
    }
}

public static class ModelInfo
{
    public record Model(string Id, string DisplayName, string SizeMB, long SizeMbApprox, string VramGB, string AccuracyKey, bool Recommended);

    public static readonly Model[] All =
    [
        new("base",            "Base",            "141 MB",   141,  "~1 GB",  "inst_AccLow",    false),
        new("small",           "Small",           "244 MB",   244,  "~2 GB",  "inst_AccMedium", false),
        new("medium",          "Medium",          "769 MB",   769,  "~5 GB",  "inst_AccGood",   false),
        new("large-v3-turbo",  "Large-v3-turbo",  "1.6 GB",  1600,  "~6 GB",  "inst_AccHigh",   true),
        new("large-v3",        "Large-v3",        "3.1 GB",  3100,  "~10 GB", "inst_AccMax",    false),
    ];
}
