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
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return;

            var res = System.Windows.Application.Current.Resources;
            foreach (var kv in dict)
                res[kv.Key] = kv.Value;

            _currentLang = lang;
        }
        catch { }
    }

    /// <summary>Возвращает список доступных языков (папки в lang/).</summary>
    public static List<string> GetAvailableLanguages()
    {
        var langDir = Path.Combine(PathHelper.AppDir, "..", "lang");
        if (!Directory.Exists(langDir)) return ["ru"];
        return Directory.GetDirectories(langDir)
            .Select(d => Path.GetFileName(d)!)
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
