using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class CreditService
{
    private readonly Supabase.Client _supabase;
    private readonly AppSettingsService _settingsService;

    public CreditService(
        Supabase.Client supabase,
        AppSettingsService settingsService)
    {
        _supabase = supabase;
        _settingsService = settingsService;
    }

    public async Task<int> GetCreditsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return 0;
            var result = await _supabase.From<Profile>()
                .Where(p => p.Id == userId).Single();
            return result?.Credits ?? 0;
        }
        catch { return 0; }
    }

    public async Task<bool> AddCreditsAsync(
        int amount, string type, string description)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;

            var profile = await _supabase.From<Profile>()
                .Where(p => p.Id == userId).Single();
            if (profile == null) return false;

            await _supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Credits, profile.Credits + amount)
                .Update();

            await AddTransactionRpcAsync(amount, type, description);
            return true;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Error)> SpendCreditsAsync(
        int amount, string type, string description)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté");

            var profile = await _supabase.From<Profile>()
                .Where(p => p.Id == userId).Single();
            if (profile == null) return (false, "Profil introuvable");

            if (profile.Credits < amount)
                return (false,
                    $"Crédits insuffisants.\n" +
                    $"Vous avez {profile.Credits} crédits " +
                    $"et il vous en faut {amount}.");

            await _supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Credits, profile.Credits - amount)
                .Update();

            await AddTransactionRpcAsync(-amount, type, description);
            return (true, string.Empty);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> HasReceivedWelcomeCreditsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;
            var result = await _supabase.From<CreditTransaction>()
                .Where(t => t.UserId == userId).Get();
            return result.Models.Any(t => t.Type == "welcome");
        }
        catch { return false; }
    }

    public async Task GrantWelcomeCreditsIfNeededAsync()
    {
        try
        {
            var alreadyReceived = await HasReceivedWelcomeCreditsAsync();
            if (alreadyReceived) return;
            var amount = await _settingsService.GetCreditsWelcomeAsync();
            await AddCreditsAsync(amount, "welcome", "Crédits de bienvenue !");
        }
        catch { }
    }

    public async Task AddWelcomeCreditsAsync()
    {
        try
        {
            var amount = await _settingsService.GetCreditsWelcomeAsync();
            await AddCreditsAsync(amount, "welcome", "Crédits de bienvenue !");
        }
        catch { }
    }

    public async Task AddVoteCreditsAsync()
    {
        try
        {
            var amount = await _settingsService.GetCreditsVoteAsync();
            await AddCreditsAsync(amount, "vote", "Crédit gagné en votant");
        }
        catch { }
    }

    public async Task AddAdBannerCreditsAsync()
    {
        try
        {
            var amount = await _settingsService.GetCreditsAdBannerAsync();
            await AddCreditsAsync(amount, "ad_banner", "Crédit pub bannière");
        }
        catch { }
    }

    public async Task AddAdRewardCreditsAsync()
    {
        try
        {
            var amount = await _settingsService.GetCreditsAdRewardAsync();
            await AddCreditsAsync(amount, "ad_reward", "Crédits pub rewarded");
        }
        catch { }
    }

    public async Task<(bool Success, string Error)> SpendForProjectAsync()
    {
        var amount = await _settingsService.GetCreditsCostProjectAsync();
        return await SpendCreditsAsync(amount, "project_submit", "Soumission d'un projet");
    }

    public async Task<(bool Success, string Error)> SpendForBoostAsync()
    {
        var amount = await _settingsService.GetCreditsCostBoostAsync();
        return await SpendCreditsAsync(amount, "project_boost", "Boost d'un projet");
    }

    public async Task<(bool Success, string Error)> SpendForMeetAsync()
    {
        var amount = await _settingsService.GetCreditsCostMeetAsync();
        return await SpendCreditsAsync(amount, "meet", "Demande de rencontre");
    }

    public async Task<(bool Success, string Error)> SpendForRewindAsync()
    {
        var amount = await _settingsService.GetCreditsCostRewindAsync();
        return await SpendCreditsAsync(amount, "rewind", "Rewind");
    }
    public async Task SpendForVoteReceivedAsync(string projectOwnerId)
    {
        try
        {
            var cost = await _settingsService.GetCreditsCostVoteReceivedAsync();
            if (cost <= 0) return;

            var profile = await _supabase.From<Profile>()
                .Where(p => p.Id == projectOwnerId).Single();
            if (profile == null) return;

            await _supabase.From<Profile>()
                .Where(p => p.Id == projectOwnerId)
                .Set(p => p.Credits, Math.Max(0, profile.Credits - cost))
                .Update();

            await _supabase.Rpc("insert_credit_transaction",
                new Dictionary<string, object>
                {
                { "p_user_id",     projectOwnerId },
                { "p_amount",      -cost },
                { "p_type",        "vote_received" },
                { "p_description", "Vote reçu sur un projet" }
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpendForVoteReceived: {ex.Message}");
        }
    }
    // ✅ Limité aux 10 dernières opérations, trié côté Supabase
    public async Task<List<CreditTransaction>> GetTransactionsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return new List<CreditTransaction>();

            var result = await _supabase
                .From<CreditTransaction>()
                .Where(t => t.UserId == userId)
                .Order(t => t.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Limit(10)
                .Get();

            return result.Models;
        }
        catch { return new List<CreditTransaction>(); }
    }

    private async Task AddTransactionRpcAsync(
        int amount, string type, string description)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return;

            await _supabase.Rpc(
                "insert_credit_transaction",
                new Dictionary<string, object>
                {
                    { "p_user_id",     userId },
                    { "p_amount",      amount },
                    { "p_type",        type },
                    { "p_description", description }
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Transaction error: {ex.Message}");
        }
    }
}