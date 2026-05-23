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

    /// <summary>
    /// Wrapper "game start" : affiche une interstitielle si elle est prête.
    /// Ne bloque JAMAIS le jeu (fire-and-forget côté show). Skip silencieux
    /// si les pubs sont désactivées ou si la pub n'est pas chargée.
    /// Appelé au moment où une série/quiz/partie bascule de 'preparing' à 'active'.
    /// </summary>
    Task ShowInterstitialBeforeGameStartAsync();

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
    public Task ShowInterstitialBeforeGameStartAsync() => Task.CompletedTask;

    // Rewarded → retourne toujours true en dev
    // pour ne pas bloquer les tests
    public Task LoadRewardedAsync() => Task.CompletedTask;
    public Task<bool> ShowRewardedAsync() => Task.FromResult(true);
    public bool IsRewardedReady => true;
}
