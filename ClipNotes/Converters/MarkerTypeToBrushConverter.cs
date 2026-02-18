using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClipNotes.Models;

namespace ClipNotes.Converters;

public class MarkerTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MarkerType mt) return Brushes.Gray;
        var key = mt switch
        {
            MarkerType.Bug => "BugBrush",
            MarkerType.Task => "TaskBrush",
            MarkerType.Note => "NoteBrush",
            _ => null
        };
        return key != null && System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Brush b
            ? b : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
