using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using BeauOuPas.Services;
using Microsoft.Extensions.DependencyInjection;
using Plugin.MauiMtAdmob;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas;



[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "beauoupas",
    DataHost = "callback")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "beauoupas",
    DataHost = "invite")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "beauoupas",
    DataHost = "join")]   // ⚡ NOUVEAU : scan QR code TV pour rejoindre une série
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // ✅ Init recadrage - version compatible
        new ImageCropper.Maui.Platform().Init(this);

        CrossMauiMTAdmob.Current.Init(this, "ca-app-pub-5814544077070305~4139208130");

        System.Diagnostics.Debug.WriteLine("[MainActivity] OnCreate");

        if (Intent?.Data != null)
        {
            var scheme = Intent.Data.Scheme;
            var path = Intent.Data.Path;

            if (scheme == "https" && path != null && path.StartsWith("/invite/"))
            {
                var code = path.Replace("/invite/", "").Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    Preferences.Set("pending_invite_code", code);
                    System.Diagnostics.Debug.WriteLine($"[MainActivity] Invite code stocké: {code}");
                }
            }
            else if (scheme == "beauoupas")
            {
                var host = Intent.Data.Host;
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] OnCreate → deep link beauoupas://{host} détecté");

                if (host == "join")
                {
                    // ⚡ NOUVEAU : QR code TV scanné → stocker le code de série
                    // L'app récupèrera ce code au lancement et navigera vers JoinSeriesPage
                    var query = Intent.Data.EncodedQuery ?? string.Empty;
                    var seriesCode = ExtractCodeFromQuery(query);
                    if (!string.IsNullOrEmpty(seriesCode))
                    {
                        Preferences.Set("pending_series_code", seriesCode);
                        System.Diagnostics.Debug.WriteLine(
                            $"[MainActivity] Series code stocké: {seriesCode}");
                    }
                }
                else
                {
                    // beauoupas://callback, beauoupas://email-confirmed → OAuth flow
                    var uri = new Uri(Intent.Data.ToString()!);
                    HandleOAuthCallback(uri);
                }
            }
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        System.Diagnostics.Debug.WriteLine($"[MainActivity] OnNewIntent scheme={intent?.Data?.Scheme}");

        if (intent?.Data != null)
        {
            var scheme = intent.Data.Scheme;
            var path = intent.Data.Path;

            if (scheme == "https" && path != null && path.StartsWith("/invite/"))
            {
                var code = path.Replace("/invite/", "").Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    Preferences.Set("pending_invite_code", code);
                    System.Diagnostics.Debug.WriteLine($"[MainActivity] Invite code stocké (NewIntent): {code}");
                }
            }
            else if (scheme == "beauoupas")
            {
                var host = intent.Data.Host;
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] OnNewIntent → deep link beauoupas://{host} détecté");

                if (host == "join")
                {
                    // ⚡ NOUVEAU : QR code TV scanné depuis app déjà ouverte
                    var query = intent.Data.EncodedQuery ?? string.Empty;
                    var seriesCode = ExtractCodeFromQuery(query);
                    if (!string.IsNullOrEmpty(seriesCode))
                    {
                        Preferences.Set("pending_series_code", seriesCode);
                        System.Diagnostics.Debug.WriteLine(
                            $"[MainActivity] Series code stocké (NewIntent): {seriesCode}");

                        // L'app est déjà ouverte → on déclenche immédiatement le traitement
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (Microsoft.Maui.Controls.Application.Current is App app)
                                await app.HandlePendingSeriesCodeAsync();
                        });
                    }
                }
                else
                {
                    var uri = new Uri(intent.Data.ToString()!);
                    HandleOAuthCallback(uri);
                }
            }
        }
    }

    /// <summary>
    /// Extrait le paramètre code= depuis une query string.
    /// Ex : "code=ABC123&type=tv" → "ABC123"
    /// </summary>
    private string ExtractCodeFromQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) return string.Empty;
        try
        {
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2 && parts[0] == "code")
                {
                    return Uri.UnescapeDataString(parts[1]).Trim().ToUpper();
                }
            }
        }
        catch { }
        return string.Empty;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode,
        global::Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        // ✅ Compatibilité recadrage
        new ImageCropper.Maui.Platform().OnActivityResult(requestCode, (int)resultCode, data);
    }

    private async void HandleOAuthCallback(Uri uri)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[OAuth] URI reçue : {uri}");

            var fragment = uri.Fragment.TrimStart('#');
            var raw = string.IsNullOrEmpty(fragment)
                ? uri.Query.TrimStart('?')
                : fragment;

            var parameters = raw.Split('&')
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(
                    p => Uri.UnescapeDataString(p[0]),
                    p => Uri.UnescapeDataString(p[1]));

            if (parameters.TryGetValue("access_token", out var accessToken) &&
                parameters.TryGetValue("refresh_token", out var refreshToken))
            {
                var authService = IPlatformApplication.Current?
                    .Services.GetService<AuthService>();

                if (authService == null) return;

                var result = await authService.SetSessionAsync(accessToken, refreshToken);

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(300);

                    if (result.IsBanned)
                    {
                        var banDate = result.BannedAt.HasValue
                            ? result.BannedAt.Value.ToString("dd/MM/yyyy")
                            : "date inconnue";
                        var reason = string.IsNullOrWhiteSpace(result.BannedReason)
                            ? "Aucune raison spécifiée."
                            : result.BannedReason;

                        var popup = new BeauOuPas.Views.Auth.BanPopup(reason, banDate);
                        await Microsoft.Maui.Controls.Application.Current!.MainPage!.ShowPopupAsync(popup);
                        await Shell.Current.GoToAsync("//LoginPage");
                        return;
                    }

                    if (result.Success)
                    {
                        var creditService = IPlatformApplication.Current?
                            .Services.GetService<CreditService>();
                        if (creditService != null)
                            await creditService.GrantWelcomeCreditsIfNeededAsync();

                        if (Shell.Current is AppShell shell)
                            shell.GoToMainApp();

                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                var notifService = IPlatformApplication.Current?
                                    .Services.GetService<NotificationService>();
                                if (notifService != null)
                                    await notifService.InitializeAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"FCM error: {ex.Message}");
                            }
                        });

                        await Task.Delay(500);
                        if (Microsoft.Maui.Controls.Application.Current is App app)
                            await app.ShowPromoMessageIfNeededAsync();
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OAuth] Erreur : {ex.Message}");
        }
    }
}