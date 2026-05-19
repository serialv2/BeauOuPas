using System.Globalization;

namespace BeauOuPas.Converters;

/// <summary>
/// Retourne #FCE4EC (rose très pâle) si true, sinon White.
/// Utilisé pour les cartes sélectionnables dans les formulaires.
/// </summary>
public class BoolToSelectedBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b
            ? Color.FromArgb("#E5DCC9")
            : Color.FromArgb("#FFFFFF");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
