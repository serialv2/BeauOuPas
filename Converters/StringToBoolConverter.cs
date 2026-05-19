using System.Globalization;

namespace BeauOuPas.Converters;

/// <summary>
/// Convertit une string en bool : true si non null/non vide, false sinon.
/// Utilisé pour cacher des Labels dont le binding produit une string vide.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
