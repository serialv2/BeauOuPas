using Foundation;
using UIKit;

namespace BeauOuPas;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Firebase iOS retiré temporairement.
        // Les notifications push iOS seront réintégrées plus tard.
        return base.FinishedLaunching(application, launchOptions);
    }

    // Deep link via URL scheme : beauoupas://
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        System.Diagnostics.Debug.WriteLine($"[iOS] OpenUrl: {url}");
        HandleUrl(url);
        return true;
    }

    // Universal Link : https://beauoupas.fr/invite/CODE
    public override bool ContinueUserActivity(
        UIApplication application,
        NSUserActivity userActivity,
        UIApplicationRestorationHandler completionHandler)
    {
        if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb
            && userActivity.WebPageUrl != null)
        {
            System.Diagnostics.Debug.WriteLine($"[iOS] Universal link: {userActivity.WebPageUrl}");
            HandleUrl(userActivity.WebPageUrl);
        }

        return true;
    }

    private void HandleUrl(NSUrl url)
    {
        var uri = new Uri(url.ToString());
        var path = uri.AbsolutePath;

        // Invitation amis : /invite/CODE
        if (path.StartsWith("/invite/"))
        {
            var code = path.Replace("/invite/", "").Trim('/');

            if (!string.IsNullOrEmpty(code))
            {
                Preferences.Set("pending_invite_code", code);
                System.Diagnostics.Debug.WriteLine($"[iOS] Invite code stocké: {code}");

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(800);

                    if (Microsoft.Maui.Controls.Application.Current is App app)
                    {
                        await app.HandlePendingInviteCodeAsync();
                    }
                });
            }
        }
        // OAuth callback : beauoupas://callback
        else if (uri.Scheme == "beauoupas")
        {
            System.Diagnostics.Debug.WriteLine("[iOS] OAuth callback détecté");
        }
    }
}