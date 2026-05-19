using System.Globalization;

namespace BeauOuPas.Helpers;

/// <summary>
/// ⚡ MAI 2026 — Helper centralisé pour ouvrir les pages légales hébergées
/// sur GitHub Pages. Évite de dupliquer les URLs (et de devoir les changer
/// à 5 endroits si on migre vers beauoupas.fr en custom domain).
///
/// Utilisation :
/// <code>
///   await LegalLinks.OpenPrivacyAsync();
///   await LegalLinks.OpenTermsAsync();
///   await LegalLinks.OpenLegalAsync();
/// </code>
///
/// Les liens sont ouverts en in-app browser (BrowserLaunchMode.SystemPreferred),
/// ce qui garde l'utilisateur dans le contexte BeauOuPas (barre de navigation
/// avec bouton retour). Si l'in-app browser plante, MAUI fait automatiquement
/// un fallback sur le navigateur externe.
///
/// Pour la traduction : si l'utilisateur est en anglais, on l'envoie sur
/// privacy-en.html ; sinon FR. Les autres langues utilisent FR comme fallback
/// pour la v1 (versions traduites à venir post-lancement).
/// </summary>
public static class LegalLinks
{
    /// <summary>
    /// URL de base du site web BeauOuPas. Si tu migres vers
    /// https://www.beauoupas.fr (custom domain GitHub Pages), change juste
    /// cette constante et tous les liens de l'app suivront.
    /// </summary>
    public const string BaseUrl = "https://serialv2.github.io/beauoupas-web";

    /// <summary>
    /// Ouvre la Politique de confidentialité. EN si l'utilisateur est en
    /// anglais, FR sinon (FR = version autoritative).
    /// </summary>
    public static Task OpenPrivacyAsync()
        => OpenLocalizedAsync("privacy");

    /// <summary>Ouvre les CGU. FR uniquement (pas encore traduit pour v1).</summary>
    public static Task OpenTermsAsync()
        => OpenAsync($"{BaseUrl}/terms.html");

    /// <summary>Ouvre les mentions légales. FR uniquement.</summary>
    public static Task OpenLegalAsync()
        => OpenAsync($"{BaseUrl}/legal.html");

    // ─── Helpers internes ─────────────────────────────────────────────

    private static Task OpenLocalizedAsync(string pageBase)
    {
        // Si l'utilisateur est en anglais, on essaie d'abord la version EN
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var url = lang == "en"
            ? $"{BaseUrl}/{pageBase}-en.html"
            : $"{BaseUrl}/{pageBase}.html";
        return OpenAsync(url);
    }

    private static async Task OpenAsync(string url)
    {
        try
        {
            await Browser.Default.OpenAsync(
                new Uri(url),
                new BrowserLaunchOptions
                {
                    LaunchMode = BrowserLaunchMode.SystemPreferred,
                    TitleMode = BrowserTitleMode.Show,
                    PreferredToolbarColor = Color.FromArgb("#C2754C"),
                    PreferredControlColor = Color.FromArgb("#FFFFFF")
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LegalLinks] OpenAsync failed: {ex.Message}");
            // Fallback : laisser MAUI ouvrir avec son comportement par défaut
            try { await Launcher.OpenAsync(new Uri(url)); }
            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine($"[LegalLinks] Launcher fallback failed: {ex2.Message}");
            }
        }
    }
}
