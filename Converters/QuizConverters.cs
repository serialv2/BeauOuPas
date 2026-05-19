using System.Globalization;

namespace BeauOuPas.Converters;

/*
 * ═══════════════════════════════════════════════════════════════════
 * BoolToCorrectBgConverter
 * ═══════════════════════════════════════════════════════════════════
 *
 * Convertit un bool en couleur de fond pour le toggle "bonne réponse" :
 *   - true  → vert clair (#E8F5E9)
 *   - false → gris clair (#F5F5F5)
 *
 * AJOUTER DANS App.xaml (dans <Application.Resources><ResourceDictionary>) :
 *   <converters:BoolToCorrectBgConverter x:Key="BoolToCorrectBgConverter"/>
 */
public class BoolToCorrectBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return Color.FromArgb("#E8F0E5");
        return Color.FromArgb("#E5DCC9");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/*
 * ═══════════════════════════════════════════════════════════════════
 * BoolToCheckEmojiConverter
 * ═══════════════════════════════════════════════════════════════════
 *
 * Convertit un bool en emoji check :
 *   - true  → "✅"
 *   - false → "⬜"
 *
 * AJOUTER DANS App.xaml :
 *   <converters:BoolToCheckEmojiConverter x:Key="BoolToCheckEmojiConverter"/>
 */
public class BoolToCheckEmojiConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return "✅";
        return "⬜";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
