namespace BeauOuPas.Services;

public class PresenceService
{
    private readonly Supabase.Client _supabase;
    private System.Timers.Timer? _timer;
    private bool _isRunning = false;

    public PresenceService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    // ─── Démarrer le suivi de présence ───────────────────────────
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        // Mise à jour immédiate
        _ = UpdatePresenceAsync();

        // Puis toutes les 60 secondes
        _timer = new System.Timers.Timer(60000);
        _timer.Elapsed += async (s, e) => await UpdatePresenceAsync();
        _timer.AutoReset = true;
        _timer.Start();

        System.Diagnostics.Debug.WriteLine("[Presence] Service démarré");
    }

    // ─── Arrêter le suivi ────────────────────────────────────────
    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _isRunning = false;
        _ = UpdatePresenceAsync(); // Dernière mise à jour avant de partir
        System.Diagnostics.Debug.WriteLine("[Presence] Service arrêté");
    }

    // ─── Mettre à jour last_seen ─────────────────────────────────
    public async Task UpdatePresenceAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return;

            await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.LastSeen, DateTime.UtcNow)
                .Update();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Presence] UpdatePresence error: {ex.Message}");
        }
    }

    // ─── Enregistrer la plateforme ───────────────────────────────
    public async Task SetPlatformAsync(string platform = "android")
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return;

            await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Platform, platform)
                .Set(p => p.LastSeen, DateTime.UtcNow)
                .Update();

            // Enregistrer dans connection_logs
            var log = new BeauOuPas.Models.ConnectionLog
            {
                UserId = userId,
                Platform = platform,
                ConnectedAt = DateTime.UtcNow
            };
            await _supabase.From<BeauOuPas.Models.ConnectionLog>().Insert(log);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Presence] SetPlatform error: {ex.Message}");
        }
    }
}
