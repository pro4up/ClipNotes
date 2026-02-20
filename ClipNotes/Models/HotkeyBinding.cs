using System.Windows.Input;
using ClipNotes.Helpers;
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
        HotkeyAction.MarkerBug     => LocalizationService.T("loc_HkMarkerBug"),
        HotkeyAction.MarkerTask    => LocalizationService.T("loc_HkMarkerTask"),
        HotkeyAction.MarkerNote    => LocalizationService.T("loc_HkMarkerNote"),
        HotkeyAction.MarkerSummary => LocalizationService.T("loc_HkMarkerSummary"),
        HotkeyAction.StartRecording => LocalizationService.T("loc_HkStartRecording"),
        HotkeyAction.StopRecording => LocalizationService.T("loc_HkStopRecording"),
        HotkeyAction.Generate      => LocalizationService.T("loc_HkGenerate"),
        HotkeyAction.OpenOutputFolder => LocalizationService.T("loc_HkOpenFolder"),
        _ => Action.ToString()
    };

    public void NotifyLocalizationChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HotkeyText));
    }

    public string HotkeyText
    {
        get
        {
            if (Key == Key.None) return LocalizationService.T("loc_HkNotAssigned");
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
