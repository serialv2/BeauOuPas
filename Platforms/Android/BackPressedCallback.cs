#if ANDROID
using Android.App;
using AndroidX.Activity;

namespace BeauOuPas;

public class BackPressedCallback : OnBackPressedCallback
{
    private readonly Activity _activity;
    private DateTime _lastBackPressed = DateTime.MinValue;

    public BackPressedCallback(Activity activity) : base(true)
    {
        _activity = activity;
    }

    public override void HandleOnBackPressed()
    {
        var currentPage = Shell.Current?.CurrentPage;

        // Si on est sur une page principale (tabs) → demander confirmation
        var mainPages = new[]
        {
            "DashboardPage", "VoteFeedPage", "FriendsPage",
            "MyProjectsPage", "ProfileSettingsPage"
        };

        var currentPageName = currentPage?.GetType().Name ?? "";
        bool isMainPage = mainPages.Any(p => currentPageName.Contains(p));

        if (isMainPage)
        {
            // Double appui en 2 secondes pour quitter
            if ((DateTime.Now - _lastBackPressed).TotalSeconds < 2)
            {
                _activity.FinishAffinity();
            }
            else
            {
                _lastBackPressed = DateTime.Now;
                // Toast Android
                Android.Widget.Toast.MakeText(
                    _activity,
                    "Appuyez à nouveau pour quitter",
                    Android.Widget.ToastLength.Short)?.Show();
            }
        }
        else
        {
            // Sur les autres pages → navigation normale en arrière
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await Shell.Current.GoToAsync(".."); }
                catch { }
            });
        }
    }
}
#endif
