using System.Globalization;
using BeauOuPas.Resources.Strings;

namespace BeauOuPas.Extensions;

[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider)
        => AppResources.ResourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
        => ProvideValue(serviceProvider);
}
