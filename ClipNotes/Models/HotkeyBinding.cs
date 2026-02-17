using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipNotes.Models;

public partial class HotkeyBinding : ObservableObject
{
    [ObservableProperty] private HotkeyAction _action;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyText))]
    private Key _key = Key.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyText))]
    private ModifierKeys _modifiers = ModifierKeys.None;

    public string DisplayName => Action switch
    {
        HotkeyAction.MarkerBug => "Маркер: Баг",
        HotkeyAction.MarkerTask => "Маркер: Задача",
        HotkeyAction.MarkerNote => "Маркер: Заметка",
        HotkeyAction.StartRecording => "Начать запись",
        HotkeyAction.StopRecording => "Остановить запись",
        HotkeyAction.Generate => "Генерация",
        HotkeyAction.OpenOutputFolder => "Открыть папку",
        _ => Action.ToString()
    };

    public string HotkeyText
    {
        get
        {
            if (Key == Key.None) return "Не назначено";
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
