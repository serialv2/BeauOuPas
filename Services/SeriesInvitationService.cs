using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class SeriesInvitationService
{
    private readonly Supabase.Client _supabase;
    private readonly FriendService _friendService;

    public SeriesInvitationService(Supabase.Client supabase, FriendService friendService)
    {
        _supabase = supabase;
        _friendService = friendService;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ─── Inviter un utilisateur à rejoindre une série ─────────────
    // Pré-requis (vérifié côté DB par RLS) : l'inviteur doit être participant.
    public async Task<(bool Success, string Error)> InviteUserAsync(
        string seriesId, string invitedUserId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "Non connecté");

            if (invitedUserId == userId)
                return (false, "Tu ne peux pas t'inviter toi-même");

            // L'invité est-il déjà participant ?
            var existingParticipant = await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == invitedUserId)
                .Get();
            if (existingParticipant.Models.Any())
                return (false, "Cette personne participe déjà à la série");

            // Y a-t-il déjà une invitation pour cette personne ?
            var existing = await _supabase.From<SeriesInvitation>()
                .Where(i => i.SeriesId == seriesId && i.InvitedUserId == invitedUserId)
                .Get();

            var existingInvite = existing.Models.FirstOrDefault();

            if (existingInvite != null)
            {
                if (existingInvite.Status == "pending")
                    return (false, "Cette personne a déjà une invitation en attente");
                if (existingInvite.Status == "accepted")
                    return (false, "Cette personne participe déjà");
                // status == "declined" → on ré-invite : update + reset status
                await _supabase.From<SeriesInvitation>()
                    .Where(i => i.Id == existingInvite.Id)
                    .Set(i => i.Status, "pending")
                    .Set(i => i.InvitedBy, userId)
                    .Set(i => i.CreatedAt, DateTime.UtcNow)
                    .Set(i => i.RespondedAt, (DateTime?)null)
                    .Update();
                return (true, string.Empty);
            }

            await _supabase.From<SeriesInvitation>().Insert(new SeriesInvitation
            {
                SeriesId       = seriesId,
                InvitedUserId  = invitedUserId,
                InvitedBy      = userId,
                Status         = "pending",
                CreatedAt      = DateTime.UtcNow
            });
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InviteUser: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── Mes invitations en attente ───────────────────────────────
    public async Task<List<SeriesInvitation>> GetMyPendingInvitationsAsync()
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return new();

            var result = await _supabase.From<SeriesInvitation>()
                .Where(i => i.InvitedUserId == userId && i.Status == "pending")
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMyInvitations: {ex.Message}");
            return new();
        }
    }

    // ─── Invitations envoyées pour une série donnée ──────────────
    public async Task<List<SeriesInvitation>> GetSeriesInvitationsAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<SeriesInvitation>()
                .Where(i => i.SeriesId == seriesId)
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSeriesInvitations: {ex.Message}");
            return new();
        }
    }

    // ─── Accepter une invitation (appelle RPC atomique) ──────────
    public async Task<(bool Success, string Error)> AcceptInvitationAsync(string invitationId)
    {
        try
        {
            await _supabase.Rpc("accept_series_invitation",
                new Dictionary<string, object> { { "p_invitation_id", invitationId } });
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AcceptInvitation: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── Refuser une invitation ──────────────────────────────────
    public async Task<(bool Success, string Error)> DeclineInvitationAsync(string invitationId)
    {
        try
        {
            await _supabase.Rpc("decline_series_invitation",
                new Dictionary<string, object> { { "p_invitation_id", invitationId } });
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeclineInvitation: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── L'utilisateur courant est-il participant ? ──────────────
    public async Task<bool> IsCurrentUserParticipantAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;
            var result = await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == userId)
                .Get();
            return result.Models.Any();
        }
        catch { return false; }
    }

    // ─── Récupère les profils invités pour affichage ─────────────
    public async Task<List<Profile>> GetProfilesByIdsAsync(IEnumerable<string> userIds)
    {
        try
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var result = await _supabase.From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.In, ids)
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProfilesByIds: {ex.Message}");
            return new();
        }
    }

    // ─── Rechercher des utilisateurs (par username) à inviter ────
    // S'appuie sur FriendService.SearchUsersAsync mais filtre les
    // utilisateurs déjà participants ou déjà invités à cette série.
    public async Task<List<UserSearchResult>> SearchUsersToInviteAsync(
        string seriesId, string query)
    {
        var users = await _friendService.SearchUsersAsync(query);
        if (users.Count == 0) return users;

        // Récupérer les participants existants
        var participants = await _supabase.From<SeriesParticipant>()
            .Where(p => p.SeriesId == seriesId).Get();
        var participantIds = participants.Models.Select(p => p.UserId).ToHashSet();

        // Récupérer les invitations en attente / acceptées
        var invitations = await _supabase.From<SeriesInvitation>()
            .Where(i => i.SeriesId == seriesId).Get();
        var invitedIds = invitations.Models
            .Where(i => i.Status == "pending" || i.Status == "accepted")
            .Select(i => i.InvitedUserId)
            .ToHashSet();

        // Filtrer
        return users
            .Where(u => !participantIds.Contains(u.UserId)
                     && !invitedIds.Contains(u.UserId))
            .ToList();
    }
}
