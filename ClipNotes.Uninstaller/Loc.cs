using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ClipNotes.Uninstaller;

public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static readonly Assembly _asm = Assembly.GetExecutingAssembly();

    public static void Load(string lang)
    {
        var name = $"ClipNotes.Uninstaller.lang.{lang}.lang.json";
        try
        {
            using var stream = _asm.GetManifestResourceStream(name);
            if (stream == null) return;
            using var reader = new StreamReader(stream);
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? new();
        }
        catch { }
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var v) ? v : key;

    public static string T(string key, params object[] args)
    {
        var s = T(key);
        return args.Length > 0 ? string.Format(s, args) : s;
    }
}
