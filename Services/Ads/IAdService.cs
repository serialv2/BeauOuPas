namespace BeauOuPas.Services.Ads;

public interface IAdService
{
    // ─── Bannière ────────────────────────────────────────────────────
    Task LoadBannerAsync();
    bool IsBannerReady { get; }

    // ─── Interstitielle ──────────────────────────────────────────────
    Task LoadInterstitialAsync();
    Task<bool> ShowInterstitialAsync();
    bool IsInterstitialReady { get; }

    // ─── Rewarded ────────────────────────────────────────────────────
    Task LoadRewardedAsync();
    Task<bool> ShowRewardedAsync();
    bool IsRewardedReady { get; }
}

// ─── Mock pour le développement ──────────────────────────────────────
public class MockAdService : IAdService
{
    // Bannière
    public Task LoadBannerAsync() => Task.CompletedTask;
    public bool IsBannerReady => false;

    // Interstitielle
    public Task LoadInterstitialAsync() => Task.CompletedTask;
    public Task<bool> ShowInterstitialAsync() => Task.FromResult(false);
    public bool IsInterstitialReady => false;

    // Rewarded → retourne toujours true en dev
    // pour ne pas bloquer les tests
    public Task LoadRewardedAsync() => Task.CompletedTask;
    public Task<bool> ShowRewardedAsync() => Task.FromResult(true);
    public bool IsRewardedReady => true;
}