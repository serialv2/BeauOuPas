using Plugin.MauiMtAdmob;
using Plugin.MauiMtAdmob.Extra;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;

namespace BeauOuPas;

public class AndroidAdService : IAdService
{
    // ─── IDs de production ───────────────────────────────────────────
    private const string BannerId = "ca-app-pub-5814544077070305/9551316912";
    private const string RewardedId = "ca-app-pub-5814544077070305/8830321670";

    // ⚠️ TODO PROD : créer le slot Interstitiel dans la console AdMob
    //    (https://apps.admob.com/ → BeauOuPas → Blocs d'annonces →
    //     Ajouter → Interstitiel → nommer "BeauOuPas - Interstitial - GameStart")
    //    puis remplacer l'ID de test ci-dessous par l'ID réel de la forme
    //    "ca-app-pub-5814544077070305/XXXXXXXXXX" AVANT le build de release.
    // ID de test Google (sert toujours une pub bidon, safe pour dev/AdMob).
    private const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";

    private readonly AppSettingsService _settingsService;
    private bool _rewardEarned = false;

    public AndroidAdService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsBannerReady => true;
    public bool IsInterstitialReady
        => CrossMauiMTAdmob.Current.IsInterstitialLoaded(InterstitialId);
    public bool IsRewardedReady
        => CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId);

    // ─── Bannière ────────────────────────────────────────────────────
    public Task LoadBannerAsync() => Task.CompletedTask;

    // ─── Interstitielle ──────────────────────────────────────────────

    public Task LoadInterstitialAsync()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossMauiMTAdmob.Current.LoadInterstitial(
                    InterstitialId,
                    new MTInterstitialAdOptions(),
                    InterstitialId);
            });
            System.Diagnostics.Debug.WriteLine("[Ad] LoadInterstitial appelé");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] LoadInterstitial error: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Affiche l'interstitielle si chargée. Retourne true si affichage déclenché.
    /// </summary>
    public async Task<bool> ShowInterstitialAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync()) return false;
            if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded(InterstitialId))
                return false;

            void OnInterstitialClosed(object? s, EventArgs e)
            {
                CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                _ = LoadInterstitialAsync();
            }

            CrossMauiMTAdmob.Current.OnInterstitialClosed += OnInterstitialClosed;

            MainThread.BeginInvokeOnMainThread(() =>
                CrossMauiMTAdmob.Current.ShowInterstitial(InterstitialId));

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitial error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Wrapper "game start" : non-bloquant, jamais throw, jamais hang.
    /// Appelé sur la transition series.status 'preparing' → 'active'.
    /// </summary>
    public async Task ShowInterstitialBeforeGameStartAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync())
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial skip : ads disabled");
                return;
            }

            if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded(InterstitialId))
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial skip : not loaded (no preload?)");
                return;
            }

            void OnInterstitialClosed(object? s, EventArgs e)
            {
                CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial closed, reload pour la prochaine");
                _ = LoadInterstitialAsync();
            }

            CrossMauiMTAdmob.Current.OnInterstitialClosed += OnInterstitialClosed;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    CrossMauiMTAdmob.Current.ShowInterstitial(InterstitialId);
                    System.Diagnostics.Debug.WriteLine("[Ad] ShowInterstitial (game start) appelé");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitial threw: {ex.Message}");
                    CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitialBeforeGameStart error: {ex.Message}");
        }
    }

    // ─── Rewarded ────────────────────────────────────────────────────
    public async Task LoadRewardedAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync()) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossMauiMTAdmob.Current.LoadRewarded(
                    RewardedId,
                    new MTRewardedAdOptions(),
                    RewardedId);
            });

            System.Diagnostics.Debug.WriteLine("[Ad] LoadRewarded appelé");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] LoadRewarded error: {ex.Message}");
        }
    }

    public async Task<bool> ShowRewardedAsync()
    {
        System.Diagnostics.Debug.WriteLine("[Ad] ShowRewardedAsync appelé");

        try
        {
            var adsEnabled = await _settingsService.GetAdsEnabledAsync();
            System.Diagnostics.Debug.WriteLine($"[Ad] adsEnabled = {adsEnabled}");

            if (!adsEnabled)
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Ads désactivées");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[Ad] IsRewardedLoaded: {CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId)}");

            if (!CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId))
            {
                await LoadRewardedAsync();
                var timeout = DateTime.Now.AddSeconds(5);
                while (!CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId)
                       && DateTime.Now < timeout)
                    await Task.Delay(500);
            }

            System.Diagnostics.Debug.WriteLine($"[Ad] IsRewardedLoaded après chargement: {CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId)}");

            if (!CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId))
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Rewarded pas chargé après timeout");
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();
            _rewardEarned = false;

            void OnRewardedOpened(object? s, EventArgs e)
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Rewarded ouvert");
            }

            void OnRewardedClosed(object? s, EventArgs e)
            {
                System.Diagnostics.Debug.WriteLine($"[Ad] Rewarded fermé - earned: {_rewardEarned}");
                CrossMauiMTAdmob.Current.OnRewardedOpened -= OnRewardedOpened;
                CrossMauiMTAdmob.Current.OnRewardedClosed -= OnRewardedClosed;
                CrossMauiMTAdmob.Current.OnUserEarnedReward -= OnUserEarnedReward;
                tcs.TrySetResult(_rewardEarned);
                _ = LoadRewardedAsync();
            }

            void OnUserEarnedReward(object? s, EventArgs e)
            {
                System.Diagnostics.Debug.WriteLine("[Ad] User earned reward");
                _rewardEarned = true;
            }

            CrossMauiMTAdmob.Current.OnRewardedOpened += OnRewardedOpened;
            CrossMauiMTAdmob.Current.OnRewardedClosed += OnRewardedClosed;
            CrossMauiMTAdmob.Current.OnUserEarnedReward += OnUserEarnedReward;

            MainThread.BeginInvokeOnMainThread(() =>
                CrossMauiMTAdmob.Current.ShowRewarded(RewardedId));

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] EXCEPTION: {ex.Message}");
            return false;
        }
    }
}
