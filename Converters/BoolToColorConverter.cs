using System.Globalization;

namespace BeauOuPas.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b
            ? Color.FromArgb("#C2754C")
            : Color.FromArgb("#E5DCC9");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}