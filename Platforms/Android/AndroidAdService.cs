using Plugin.MauiMtAdmob;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;

namespace BeauOuPas;

public class AndroidAdService : IAdService
{
    private const string BannerId = "ca-app-pub-5814544077070305/9551316912";
    private const string RewardedId = "ca-app-pub-5814544077070305/8830321670";
    private const string InterstitialId = "ca-app-pub-5814544077070305/2926367650";

    private readonly AppSettingsService _settingsService;
    private bool _rewardEarned = false;

    public AndroidAdService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;

        // Logs de diagnostic : on saura si la pub charge ou échoue
        CrossMauiMTAdmob.Current.OnInterstitialLoaded += (s, e) =>
            System.Diagnostics.Debug.WriteLine("[Ad] Interstitial LOADED ✓");
        CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (s, e) =>
            System.Diagnostics.Debug.WriteLine($"[Ad] Interstitial FAILED to load: {e?.ToString() ?? "unknown"}");
    }

    public bool IsBannerReady => true;
    public bool IsInterstitialReady => CrossMauiMTAdmob.Current.IsInterstitialLoaded();
    public bool IsRewardedReady => CrossMauiMTAdmob.Current.IsRewardedLoaded();

    public Task LoadBannerAsync() => Task.CompletedTask;

    public Task LoadInterstitialAsync()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossMauiMTAdmob.Current.LoadInterstitial(InterstitialId);
            });
            System.Diagnostics.Debug.WriteLine("[Ad] LoadInterstitial appelé");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] LoadInterstitial error: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task<bool> ShowInterstitialAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync()) return false;
            if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded()) return false;

            void OnInterstitialClosed(object? s, EventArgs e)
            {
                CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                _ = LoadInterstitialAsync();
            }

            CrossMauiMTAdmob.Current.OnInterstitialClosed += OnInterstitialClosed;

            MainThread.BeginInvokeOnMainThread(() =>
                CrossMauiMTAdmob.Current.ShowInterstitial());

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitial error: {ex.Message}");
            return false;
        }
    }

    public async Task ShowInterstitialBeforeGameStartAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync())
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial skip : ads disabled");
                return;
            }

            // Si la pub n'est pas encore chargée, on patiente max 8s pour laisser
            // le préchargement Google finir (peut prendre 5-6s au tout premier appel).
            // Non-bloquant pour l'utilisateur (fire-and-forget), donc OK d'attendre.
            var timeout = DateTime.UtcNow.AddSeconds(8);
            while (!CrossMauiMTAdmob.Current.IsInterstitialLoaded() && DateTime.UtcNow < timeout)
            {
                await Task.Delay(200);
            }

            if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded())
            {
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial skip : not loaded after 8s wait");
                return;
            }

            // On attend la fermeture de la pub avant de retourner, pour que
            // le caller puisse signaler la TV ("j'ai fini de regarder la pub").
            var tcs = new TaskCompletionSource<bool>();

            void OnInterstitialClosed(object? s, EventArgs e)
            {
                CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                System.Diagnostics.Debug.WriteLine("[Ad] Interstitial closed, reload");
                _ = LoadInterstitialAsync();
                tcs.TrySetResult(true);
            }

            CrossMauiMTAdmob.Current.OnInterstitialClosed += OnInterstitialClosed;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    CrossMauiMTAdmob.Current.ShowInterstitial();
                    System.Diagnostics.Debug.WriteLine("[Ad] ShowInterstitial (game start) appelé");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitial threw: {ex.Message}");
                    CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                    tcs.TrySetResult(false);
                }
            });

            // Garde-fou : si la fermeture n'arrive jamais (cas pathologique),
            // on débloque après 60s pour ne pas hanger les callers à jamais.
            _ = Task.Delay(60_000).ContinueWith(_ =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    CrossMauiMTAdmob.Current.OnInterstitialClosed -= OnInterstitialClosed;
                    tcs.TrySetResult(false);
                }
            });

            await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] ShowInterstitialBeforeGameStart error: {ex.Message}");
        }
    }

    public async Task LoadRewardedAsync()
    {
        try
        {
            if (!await _settingsService.GetAdsEnabledAsync()) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CrossMauiMTAdmob.Current.LoadRewarded(RewardedId);
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
            if (!adsEnabled) return false;

            if (!CrossMauiMTAdmob.Current.IsRewardedLoaded())
            {
                await LoadRewardedAsync();
                var timeout = DateTime.Now.AddSeconds(5);
                while (!CrossMauiMTAdmob.Current.IsRewardedLoaded()
                       && DateTime.Now < timeout)
                    await Task.Delay(500);
            }

            if (!CrossMauiMTAdmob.Current.IsRewardedLoaded())
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
                CrossMauiMTAdmob.Current.ShowRewarded());

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ad] EXCEPTION: {ex.Message}");
            return false;
        }
    }
}