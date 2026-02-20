using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipNotes.Models;

public partial class Marker : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private MarkerType _type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimestampFormatted))]
    private TimeSpan _timestamp;

    [ObservableProperty] private string _timecode = "";
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _generateAudio = true;
    [ObservableProperty] private bool _generateText = true;
    [ObservableProperty] private string? _audioFilePath;
    [ObservableProperty] private string? _textFilePath;

    /// <summary>Длительность удержания (только для режима Удержание). null = обычный маркер.</summary>
    public TimeSpan? HoldDuration { get; set; }

    [JsonIgnore]
    public string TimestampFormatted => Timestamp.ToString(@"hh\:mm\:ss");

    [JsonIgnore]
    public string HoldDurationText => HoldDuration.HasValue ? $"({HoldDuration.Value:mm\\:ss})" : "";
}
