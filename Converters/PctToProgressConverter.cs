using System.Globalization;

namespace BeauOuPas.Converters;

/// <summary>
/// Convertit un pourcentage (0–100) en valeur de progression (0.0–1.0)
/// pour les ProgressBar MAUI.
/// </summary>
public class PctToProgressConverter : IValueConverter
{
    public object Convert(
        object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is double d)
            return Math.Clamp(d / 100.0, 0.0, 1.0);
        return 0.0;
    }

    public object ConvertBack(
        object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
