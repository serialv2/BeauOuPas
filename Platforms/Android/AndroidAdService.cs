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

    private readonly AppSettingsService _settingsService;
    private bool _rewardEarned = false;

    public AndroidAdService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsBannerReady => true;
    public bool IsInterstitialReady => true;
    public bool IsRewardedReady
        => CrossMauiMTAdmob.Current.IsRewardedLoaded(RewardedId);

    // ─── Bannière ────────────────────────────────────────────────────
    public Task LoadBannerAsync() => Task.CompletedTask;

    // ─── Interstitielle ──────────────────────────────────────────────
    public Task LoadInterstitialAsync() => Task.CompletedTask;
    public Task<bool> ShowInterstitialAsync() => Task.FromResult(false);

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
