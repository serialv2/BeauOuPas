#if ANDROID
using Android.Content;
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace BeauOuPas.Platforms.Android;

/// <summary>
/// ⚡ MAI 2026 — FIX P2 v4
/// Custom ShellRenderer Android qui injecte un tracker custom sur la
/// BottomNavigationView. Le tracker attache un OnItemReselectedListener
/// natif qui détecte le retap de l'onglet courant (cas où MAUI Shell
/// ne déclenche aucun event public côté C#).
///
/// Quand l'utilisateur retap l'onglet Home (index 0), on appelle
/// AppShell.ForceGoHomeAsync() qui reset la stack à la HomePage racine.
/// </summary>
public class AppShellRenderer : ShellRenderer
{
    public AppShellRenderer(Context context) : base(context)
    {
    }

    protected override IShellBottomNavViewAppearanceTracker
        CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
    {
        return new HomeReselectBottomNavTracker();
    }
}

/// <summary>
/// Tracker custom qui attache un listener "Reselected" à la BottomNavigationView
/// fournie par MAUI Shell. SetAppearance est appelée par le renderer à chaque
/// fois que MAUI met à jour la TabBar, donc on a toujours la bonne instance.
/// </summary>
public class HomeReselectBottomNavTracker : IShellBottomNavViewAppearanceTracker
{
    public void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        bottomView.SetOnItemReselectedListener(new HomeReselectedListener(bottomView));
    }

    public void ResetAppearance(BottomNavigationView bottomView)
    {
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Listener natif déclenché EXACTEMENT quand l'utilisateur retap
    /// l'onglet courant de la BottomNavigationView.
    /// </summary>
    private class HomeReselectedListener : Java.Lang.Object,
        NavigationBarView.IOnItemReselectedListener
    {
        private readonly BottomNavigationView _bottomView;

        public HomeReselectedListener(BottomNavigationView bottomView)
        {
            _bottomView = bottomView;
        }

        public void OnNavigationItemReselected(IMenuItem item)
        {
            var index = GetMenuIndex(item);

            System.Diagnostics.Debug.WriteLine(
                $"[TabBar] Reselected index={index}, itemId={item.ItemId}");

            // Onglet Home = index 0 dans la TabBar AppShell.xaml
            if (index != 0)
                return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Shell.Current is AppShell shell)
                    await shell.ForceGoHomeAsync();
            });
        }

        private int GetMenuIndex(IMenuItem item)
        {
            var menu = _bottomView.Menu;
            for (int i = 0; i < menu.Size(); i++)
            {
                if (menu.GetItem(i).ItemId == item.ItemId)
                    return i;
            }
            return -1;
        }
    }
}
#endif