using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClipNotes.Models;

namespace ClipNotes.Converters;

public class MarkerTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MarkerType mt ? mt switch
        {
            MarkerType.Bug => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            MarkerType.Task => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            MarkerType.Note => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            _ => Brushes.Gray
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
