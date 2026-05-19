using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.Localization;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

/// <summary>
/// ⚡ MAI 2026 — Hub d'invitations unifié.
///
/// AVANT : cette page n'affichait QUE les invitations à des séries
/// (table series_invitations). Les demandes d'ami (table friendships
/// avec status="pending") étaient affichées séparément dans FriendsPage,
/// ce qui était trompeur pour l'utilisateur qui ne savait pas où
/// chercher.
///
/// MAINTENANT : la page liste les 2 types d'invitations en 2 sections :
///   • 👋 Demandes d'ami reçues  (CanAccept == true côté FriendService)
///   • 📩 Invitations à des séries / quiz
///
/// Le badge sur la HomePage compte la somme des 2.
/// La FriendsPage continue d'afficher la liste des amis confirmés
/// (et invitations envoyées) — elle n'est pas modifiée par ce chantier.
///
/// ⚡ FIX MAI 2026 (B5) : protection contre les appels concurrents de
/// LoadAsync. Sans cette garde, OnAppearing + RefreshView déclenchent
/// 2 appels simultanés qui s'entrelacent : le Clear() du 1er est annulé
/// par les Add() du 2e qui passe entre-temps → doublons affichés.
/// + Déduplication par ID côté UI au cas où la base ramènerait par erreur
/// des doublons (ceinture + bretelles).
/// </summary>
public partial class MyInvitationsViewModel : ObservableObject
{
    private readonly SeriesInvitationService _invitationService;
    private readonly SeriesService _seriesService;
    private readonly FriendService _friendService;

    // ⚡ FIX B5 : sémaphore pour empêcher les appels concurrents de LoadAsync.
    // SemaphoreSlim(1,1) = un seul thread à la fois. Si un 2e appel arrive
    // pendant qu'un Load est déjà en cours, on le skippe immédiatement
    // (pas d'attente : ce serait inutile car les données seraient identiques).
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public MyInvitationsViewModel(
        SeriesInvitationService invitationService,
        SeriesService seriesService,
        FriendService friendService)
    {
        _invitationService = invitationService;
        _seriesService = seriesService;
        _friendService = friendService;
    }

    [ObservableProperty] private bool _isLoading = false;

    // ─── Section 1 : Demandes d'ami reçues ──────────────────────────
    public ObservableCollection<FriendRequestItem> FriendRequests { get; } = new();
    [ObservableProperty] private bool _hasFriendRequests = false;
    [ObservableProperty] private int _friendRequestsCount = 0;

    // ─── Section 2 : Invitations à des séries ───────────────────────
    public ObservableCollection<InvitationItem> SeriesInvitations { get; } = new();
    [ObservableProperty] private bool _hasSeriesInvitations = false;
    [ObservableProperty] private int _seriesInvitationsCount = 0;

    // ─── État global ────────────────────────────────────────────────
    /// <summary>True si AU MOINS une invitation (de n'importe quel type).</summary>
    public bool HasAnyInvitation => HasFriendRequests || HasSeriesInvitations;
    public bool IsEmpty => !HasAnyInvitation;

    partial void OnHasFriendRequestsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasAnyInvitation));
        OnPropertyChanged(nameof(IsEmpty));
    }
    partial void OnHasSeriesInvitationsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasAnyInvitation));
        OnPropertyChanged(nameof(IsEmpty));
    }

    // ─── Chargement ────────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadAsync()
    {
        // ⚡ FIX B5 : empêcher les appels concurrents.
        // WaitAsync(0) tente d'acquérir le verrou immédiatement ; si pris,
        // on skippe le 2e appel plutôt que d'attendre (sinon flash visuel
        // pénible quand OnAppearing + RefreshView s'enchaînent au démarrage).
        if (!await _loadLock.WaitAsync(0))
        {
            System.Diagnostics.Debug.WriteLine("[MyInvitations] LoadAsync skipped (concurrent call)");
            return;
        }

        IsLoading = true;
        try
        {
            // Chargement en parallèle des 2 sources (gain de temps)
            var friendsTask = _friendService.GetFriendsAsync();
            var seriesInvitesTask = _invitationService.GetMyPendingInvitationsAsync();

            await Task.WhenAll(friendsTask, seriesInvitesTask);

            await PopulateFriendRequestsAsync(friendsTask.Result);
            await PopulateSeriesInvitationsAsync(seriesInvitesTask.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MyInvitations] LoadAsync: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
        }
    }

    // ─── Section 1 : Construction des demandes d'ami ───────────────
    private Task PopulateFriendRequestsAsync(List<FriendItem> friends)
    {
        FriendRequests.Clear();

        // On garde uniquement les invitations reçues en attente
        // (CanAccept = Status == "pending" && !IsRequester)
        // ⚡ FIX B5 : déduplication par FriendshipId au cas où.
        var received = friends
            .Where(f => f.CanAccept)
            .GroupBy(f => f.FriendshipId)
            .Select(g => g.First())
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        foreach (var f in received)
        {
            FriendRequests.Add(new FriendRequestItem
            {
                FriendshipId = f.FriendshipId,
                UserId = f.UserId,
                Username = f.Username,
                AvatarUrl = f.AvatarUrl,
                AvatarInitial = f.AvatarInitial,
                HasAvatar = f.HasAvatar,
                CreatedAt = f.CreatedAt
            });
        }
        FriendRequestsCount = FriendRequests.Count;
        HasFriendRequests = FriendRequests.Count > 0;
        return Task.CompletedTask;
    }

    // ─── Section 2 : Construction des invitations à séries ─────────
    private async Task PopulateSeriesInvitationsAsync(List<SeriesInvitation> invitations)
    {
        SeriesInvitations.Clear();

        if (invitations.Count > 0)
        {
            // ⚡ FIX B5 (renforcement) : déduplication par Id (clé primaire) au cas
            // où la base ramènerait des doublons. Ceinture + bretelles.
            var uniqueInvitations = invitations
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .ToList();

            var seriesIds = uniqueInvitations.Select(i => i.SeriesId).Distinct().ToList();
            var inviterIds = uniqueInvitations.Select(i => i.InvitedBy).Distinct().ToList();

            // Charger les séries (séquentiel car pas de batch get côté service)
            var seriesById = new Dictionary<string, Series>();
            foreach (var sid in seriesIds)
            {
                var s = await _seriesService.GetSeriesAsync(sid);
                if (s != null) seriesById[sid] = s;
            }

            // Charger les profils des inviteurs (batch)
            var profiles = await _invitationService.GetProfilesByIdsAsync(inviterIds);
            var profilesById = profiles.ToDictionary(p => p.Id, p => p);

            foreach (var inv in uniqueInvitations.OrderByDescending(i => i.CreatedAt))
            {
                seriesById.TryGetValue(inv.SeriesId, out var series);
                profilesById.TryGetValue(inv.InvitedBy, out var inviter);

                SeriesInvitations.Add(new InvitationItem
                {
                    Id = inv.Id,
                    SeriesId = inv.SeriesId,
                    SeriesTitle = series?.Title ?? L.T("MyInvitations_DeletedSeries"),
                    InviterName = inviter?.Username ?? L.T("MyInvitations_Someone"),
                    CreatedAt = inv.CreatedAt
                });
            }
        }
        SeriesInvitationsCount = SeriesInvitations.Count;
        HasSeriesInvitations = SeriesInvitations.Count > 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // SECTION 1 — Actions sur les demandes d'ami
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task AcceptFriendAsync(FriendRequestItem item)
    {
        if (item == null || item.IsProcessing) return;
        item.IsProcessing = true;

        try
        {
            var success = await _friendService.AcceptFriendshipAsync(item.FriendshipId);
            if (success)
            {
                FriendRequests.Remove(item);
                FriendRequestsCount = FriendRequests.Count;
                HasFriendRequests = FriendRequests.Count > 0;

                await Shell.Current.DisplayAlert(
                    L.T("MyInvitations_FriendAcceptedTitle"),
                    string.Format(L.T("MyInvitations_FriendAcceptedMessage"), item.Username),
                    L.T("Common_OK"));
            }
            else
            {
                item.IsProcessing = false;
                await Shell.Current.DisplayAlert(
                    L.T("Common_Error"),
                    L.T("Friends_AcceptError"),
                    L.T("Common_OK"));
            }
        }
        catch (Exception ex)
        {
            item.IsProcessing = false;
            System.Diagnostics.Debug.WriteLine($"[MyInvitations] AcceptFriend: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeclineFriendAsync(FriendRequestItem item)
    {
        if (item == null || item.IsProcessing) return;

        var confirm = await Shell.Current.DisplayAlert(
            L.T("MyInvitations_DeclineFriendTitle"),
            string.Format(L.T("MyInvitations_DeclineFriendMessage"), item.Username),
            L.T("MyInvitations_Decline"),
            L.T("Common_Cancel"));
        if (!confirm) return;

        item.IsProcessing = true;
        try
        {
            // Côté backend, "refuser" une demande d'ami = supprimer la ligne friendships
            var success = await _friendService.DeleteFriendshipAsync(item.FriendshipId);
            if (success)
            {
                FriendRequests.Remove(item);
                FriendRequestsCount = FriendRequests.Count;
                HasFriendRequests = FriendRequests.Count > 0;
            }
            else
            {
                item.IsProcessing = false;
            }
        }
        catch (Exception ex)
        {
            item.IsProcessing = false;
            System.Diagnostics.Debug.WriteLine($"[MyInvitations] DeclineFriend: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SECTION 2 — Actions sur les invitations à séries (inchangé fonctionnellement)
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task AcceptSeriesAsync(InvitationItem item)
    {
        if (item == null || item.IsProcessing) return;
        item.IsProcessing = true;

        var (success, error) = await _invitationService.AcceptInvitationAsync(item.Id);
        if (success)
        {
            SeriesInvitations.Remove(item);
            SeriesInvitationsCount = SeriesInvitations.Count;
            HasSeriesInvitations = SeriesInvitations.Count > 0;
            await Shell.Current.DisplayAlert(
                L.T("MyInvitations_SeriesAcceptedTitle"),
                L.T("MyInvitations_SeriesAcceptedMessage"),
                L.T("Common_OK"));
        }
        else
        {
            item.IsProcessing = false;
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
        }
    }

    [RelayCommand]
    private async Task DeclineSeriesAsync(InvitationItem item)
    {
        if (item == null || item.IsProcessing) return;

        var confirm = await Shell.Current.DisplayAlert(
            L.T("MyInvitations_DeclineSeriesTitle"),
            string.Format(L.T("MyInvitations_DeclineSeriesMessage"), item.SeriesTitle),
            L.T("MyInvitations_Decline"),
            L.T("Common_Cancel"));
        if (!confirm) return;

        item.IsProcessing = true;
        var (success, _) = await _invitationService.DeclineInvitationAsync(item.Id);
        if (success)
        {
            SeriesInvitations.Remove(item);
            SeriesInvitationsCount = SeriesInvitations.Count;
            HasSeriesInvitations = SeriesInvitations.Count > 0;
        }
        else
        {
            item.IsProcessing = false;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// MODÈLES D'ITEMS POUR LA UI
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Invitation à une série/quiz reçue, prête à être bindée par la UI.
/// </summary>
public partial class InvitationItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string SeriesId { get; set; } = string.Empty;
    public string SeriesTitle { get; set; } = string.Empty;
    public string InviterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string SubLabel => string.Format(L.T("MyInvitations_InvitedBy"), InviterName);

    [ObservableProperty] private bool _isProcessing = false;
}

/// <summary>
/// Demande d'ami reçue, prête à être bindée par la UI.
/// </summary>
public partial class FriendRequestItem : ObservableObject
{
    public string FriendshipId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string AvatarInitial { get; set; } = "?";
    public bool HasAvatar { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>"@username" préfixé pour cohérence visuelle avec le reste de l'app.</summary>
    public string DisplayName => $"@{Username}";

    [ObservableProperty] private bool _isProcessing = false;
}