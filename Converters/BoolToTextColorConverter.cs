using System.Globalization;

namespace BeauOuPas.Converters;

public class BoolToTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b
            ? Colors.White
            : Color.FromArgb("#3D2817");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
