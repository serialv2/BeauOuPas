

 using BeauOuPas.Models;
using BeauOuPas.Services;
using Postgrest;

namespace BeauOuPas.Services;

public class PromoMessageService
{
    private readonly Supabase.Client _supabase;
    private readonly AuthService _authService;

    public PromoMessageService(Supabase.Client supabase, AuthService authService)
    {
        _supabase = supabase;
        _authService = authService;
    }

    // Clé unique par utilisateur
    private string SeenMessagesKey
        => $"seen_promo_{_authService.CurrentUser?.Id ?? "anonymous"}";

    /// <summary>
    /// Retourne le message promo actif non encore vu par l'utilisateur.
    /// Null si aucun message à afficher.
    /// </summary>
    public async Task<PromoMessage?> GetActiveUnseenMessageAsync()
    {
        try
        {
            var result = await _supabase
                .From<PromoMessage>()
                .Filter("is_active", Postgrest.Constants.Operator.Equals, "true")
                .Get();

            if (result?.Models == null || result.Models.Count == 0)
                return null;

            // Filtrer les expirés côté client
            var active = result.Models
                .Where(m => m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow)
                .ToList();

            if (active.Count == 0) return null;

            // Récupérer les IDs déjà vus par CET utilisateur
            var seenIds = GetSeenMessageIds();

            // Retourner le premier message non vu
            return active.FirstOrDefault(m => !seenIds.Contains(m.Id));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PromoMessage] GetActiveUnseenMessageAsync: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Marque un message comme vu — ne sera plus affiché pour CET utilisateur.
    /// </summary>
    public void MarkMessageAsSeen(string messageId)
    {
        var seenIds = GetSeenMessageIds();
        if (!seenIds.Contains(messageId))
        {
            seenIds.Add(messageId);
            var json = System.Text.Json.JsonSerializer.Serialize(seenIds);
            Preferences.Default.Set(SeenMessagesKey, json);
        }
    }

    private List<string> GetSeenMessageIds()
    {
        try
        {
            var json = Preferences.Default.Get(SeenMessagesKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<string>();
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}