using System.Globalization;

namespace BeauOuPas.Converters;

/// <summary>
/// Convertit une valeur null en bool.
/// - Si la valeur est null     → false
/// - Si la valeur est non-null → true
///
/// Avec ConverterParameter="inverse", le résultat est inversé :
/// - Si la valeur est null     → true
/// - Si la valeur est non-null → false
///
/// Utile pour afficher/cacher un placeholder (ex: icône 📷)
/// quand une propriété (ex: PhotoSource) est null vs remplie.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNotNull = value != null;

        // Si on demande l'inverse via ConverterParameter="inverse"
        if (parameter is string p && p.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            return !isNotNull;

        return isNotNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Conversion inverse non utilisée dans notre cas
        throw new NotImplementedException();
    }
}