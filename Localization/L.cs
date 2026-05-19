using System.Globalization;
using BeauOuPas.Resources.Strings;

namespace BeauOuPas.Localization;

public static class L
{
    public static string T(string key)
        => AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object[] args)
        => string.Format(CultureInfo.CurrentUICulture, T(key), args);
}
