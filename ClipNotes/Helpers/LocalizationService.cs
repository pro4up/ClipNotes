using System.IO;
using System.Text.Json;

namespace ClipNotes.Helpers;

/// <summary>
/// Загружает локализацию из lang/{lang}/lang.json в Application.Resources.
/// Пользователи могут добавлять свои языки, создав папку lang/{code}/lang.json.
/// </summary>
public static class LocalizationService
{
    private static string _currentLang = "ru";
    public static string CurrentLang => _currentLang;

    private const long MaxLangFileBytes = 100 * 1024; // 100 KB — lang files are small text dictionaries

    /// <summary>Загружает язык. При отсутствии файла — fallback на "en".</summary>
    public static void Load(string lang)
    {
        var path = GetLangPath(lang);
        if (!File.Exists(path))
        {
            if (lang != "en") path = GetLangPath("en");
            if (!File.Exists(path)) return;
        }

        try
        {
            // Reject oversized lang files — protects against crafted replacements in lang/ directory.
            if (new FileInfo(path).Length > MaxLangFileBytes)
            {
                ClipNotes.Services.LogService.Warn(
                    $"LocalizationService: lang file '{path}' exceeds size limit, skipped.");
                return;
            }

            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return;

            var res = System.Windows.Application.Current.Resources;
            foreach (var kv in dict)
                res[kv.Key] = kv.Value;

            _currentLang = lang;
        }
        catch (Exception ex)
        {
            ClipNotes.Services.LogService.Warn($"LocalizationService: failed to load '{lang}': {ex.Message}");
        }
    }

    /// <summary>Возвращает список доступных языков (папки в lang/).</summary>
    public static List<string> GetAvailableLanguages()
    {
        var langDir = Path.Combine(PathHelper.AppDir, "..", "lang");
        if (!Directory.Exists(langDir)) return ["ru"];
        return Directory.GetDirectories(langDir)
            .Select(d => Path.GetFileName(d)!)
            // Accept only valid BCP-47-style lang codes (2-5 lowercase letters, optional region tag)
            // to prevent loading files from attacker-controlled directories.
            .Where(s => System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-z]{2,3}(-[A-Z]{2})?$"))
            .OrderBy(s => s)
            .ToList();
    }

    /// <summary>Возвращает локализованную строку по ключу. Fallback — сам ключ.</summary>
    public static string T(string key)
    {
        var val = System.Windows.Application.Current?.Resources[key];
        return val as string ?? key;
    }

    private static string GetLangPath(string lang) =>
        Path.GetFullPath(Path.Combine(PathHelper.AppDir, "..", "lang", lang, "lang.json"));
}
