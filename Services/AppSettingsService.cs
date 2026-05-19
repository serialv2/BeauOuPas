using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class AppSettingsService
{
    private readonly Supabase.Client _supabase;
    private readonly Dictionary<string, string> _cache = new();

    public AppSettingsService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }
    public Task<bool> GetAdsEnabledAsync()
    {
        return Task.FromResult(true);
    }
    private async Task<string> GetAsync(string key, string defaultValue)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        try
        {
            var result = await _supabase
                .From<AppSetting>()
                .Filter("key", Postgrest.Constants.Operator.Equals, key)
                .Single();
            var value = result?.Value ?? defaultValue;
            _cache[key] = value;
            return value;
        }
        catch { return defaultValue; }
    }

    public void ClearCache() => _cache.Clear();
    public async Task<int> GetCreditsCostSeriesTemplateAsync()
    => ParseInt(await GetAsync("credits_cost_series_template", "2"), 2);

    public async Task<int> GetCreditsCostSeriesProjectAsync()
        => ParseInt(await GetAsync("credits_cost_series_project", "3"), 3);

    public async Task<int> GetCreditsCostVisualThemeAsync()
        => ParseInt(await GetAsync("credits_cost_visual_theme", "5"), 5);
    private static bool ParseBool(string v, bool def)
        => bool.TryParse(v, out var r) ? r : def;
    private static int ParseInt(string v, int def)
        => int.TryParse(v, out var r) ? r : def;

    // ─── Existants ───────────────────────────────────────────────
    public async Task<bool> IsMeetEnabledAsync()
        => ParseBool(await GetAsync("meet_enabled", "true"), true);
    public async Task<int> GetCreditsCostVoteReceivedAsync()
    => ParseInt(await GetAsync("credits_cost_vote_received", "1"), 1);
    public async Task<int> GetCreditsWelcomeAsync()
        => ParseInt(await GetAsync("credits_welcome", "10"), 10);
    public async Task<int> GetCreditsVoteAsync()
        => ParseInt(await GetAsync("credits_vote", "1"), 1);
    public async Task<int> GetCreditsCostProjectAsync()
        => ParseInt(await GetAsync("credits_cost_project", "5"), 5);
    public async Task<int> GetCreditsCostMeetAsync()
        => ParseInt(await GetAsync("credits_cost_meet", "3"), 3);
    public async Task<int> GetCreditsCostRewindAsync()
        => ParseInt(await GetAsync("credits_cost_rewind", "2"), 2);
    public async Task<int> GetCreditsCostBoostAsync()
        => ParseInt(await GetAsync("credits_cost_boost", "10"), 10);
    public async Task<int> GetCreditsAdBannerAsync()
        => ParseInt(await GetAsync("credits_ad_banner", "1"), 1);
    public async Task<int> GetCreditsAdRewardAsync()
        => ParseInt(await GetAsync("credits_ad_reward", "3"), 3);
    public async Task<int> GetAdFrequencyAsync()
        => ParseInt(await GetAsync("ad_frequency", "5"), 5);
    public async Task<int> GetAdRewardFrequencyAsync()
        => ParseInt(await GetAsync("ad_reward_frequency", "10"), 10);
    public async Task<int> GetCreditsGainFriendAsync()
        => ParseInt(await GetAsync("credits_gain_friend", "5"), 5);

    // ⚡ NOUVEAU : coût d'un Super Meet (rencontre prioritaire)
    public async Task<int> GetMeetSuperCostAsync()
        => ParseInt(await GetAsync("meet_super_cost", "10"), 10);

    // ─── Nombre max de projets ───────────────────────────────────────
    public async Task<int> GetMaxProjectsPerUserAsync()
        => ParseInt(await GetAsync("max_projects_per_user", "5"), 5);

    // ─── Nouveaux V2 ─────────────────────────────────────────────

    // Kill switch
    public async Task<bool> IsAppEnabledAsync()
        => ParseBool(await GetAsync("app_enabled", "true"), true);

    // Message de maintenance
    public async Task<string> GetMaintenanceMessageAsync()
        => await GetAsync("app_maintenance_message",
            "L'application est temporairement indisponible.");

    // Contrôle version
    public async Task<string> GetRequiredVersionAsync()
        => await GetAsync("app_version_required", "1.0");

    // Résultats votes visibles
    public async Task<bool> ShowVoteResultsAsync()
        => ParseBool(await GetAsync("show_vote_results", "false"), false);

    // Chat amis activé
    public async Task<bool> IsFriendChatEnabledAsync()
        => ParseBool(await GetAsync("friend_chat_enabled", "true"), true);

    // Feedback activé
    public async Task<bool> IsFeedbackEnabledAsync()
        => ParseBool(await GetAsync("feedback_enabled", "true"), true);

    // Création de quiz activée (réglable depuis l'admin)
  

    // ⚡ NOUVEAU E1 : Flag global pour autoriser/interdire la création de quiz
    // Permettra plus tard de basculer sur "premium uniquement" ou avec quotas.
    public async Task<bool> IsQuizCreationEnabledAsync()
        => ParseBool(await GetAsync("quiz_creation_enabled", "true"), true);
}
