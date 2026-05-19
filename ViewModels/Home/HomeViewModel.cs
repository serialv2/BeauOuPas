using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Home;

/// <summary>
/// ViewModel de la page d'accueil (HomePage). L'utilisateur arrive ici
/// après login. La page oriente vers :
///   • Voter (feed) — grosse carte rose
///   • Créer un quiz ou une partie — grosse carte violette → ouvre l'écran
///     de choix de contexte (sans groupe / nouveau groupe / groupe existant)
///   • Rejoindre une série par code — champ rose en haut
///   • 4 raccourcis : Mes groupes, Invitations (badge), Mes projets, Mes crédits
///   • Pastille crédits en haut à droite (cliquable → CreditsPage)
///
/// ⚡ Le bouton "Invitations" (ex "Mes séries", renommé en mai 2026) :
///   - Pointe vers MyInvitationsPage (inchangé)
///   - Icône 📩 avec badge numérique en haut-droite
///   - Compteur d'invitations = demandes d'ami + invitations à séries
///   - Rafraîchi à chaque OnAppearing
///
/// Toutes les chaînes utilisateur passent par L.T(...) / L.F(...).
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CreditService _creditService;
    private readonly ProjectService _projectService;
    private readonly GroupService _groupService;
    private readonly SeriesService _seriesService;
    private readonly SeriesInvitationService _invitationService;
    private readonly FriendService _friendService;

    public HomeViewModel(
        AuthService authService,
        CreditService creditService,
        ProjectService projectService,
        GroupService groupService,
        SeriesService seriesService,
        SeriesInvitationService invitationService,
        FriendService friendService)
    {
        _authService = authService;
        _creditService = creditService;
        _projectService = projectService;
        _groupService = groupService;
        _seriesService = seriesService;
        _invitationService = invitationService;
        _friendService = friendService;
    }

    // ─── Profil ────────────────────────────────────────────────────
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private bool _isLoading = false;

    // ─── Code rejoindre série ─────────────────────────────────────
    [ObservableProperty] private string _joinCode = string.Empty;
    [ObservableProperty] private bool _isJoining = false;
    [ObservableProperty] private string _joinError = string.Empty;
    [ObservableProperty] private bool _hasJoinError = false;

    // ─── Compteurs (mini-cards + pastille) ────────────────────────
    [ObservableProperty] private int _groupsCount = 0;
    [ObservableProperty] private int _projectsCount = 0;
    [ObservableProperty] private int _credits = 0;

    /// <summary>
    /// Nombre TOTAL d'invitations en attente pour l'utilisateur (somme des
    /// demandes d'ami reçues + invitations à des séries reçues). Mis à jour
    /// à chaque LoadAsync (donc à chaque OnAppearing de la HomePage).
    /// </summary>
    [ObservableProperty] private int _pendingInvitationsCount = 0;

    // ─── Labels pré-formés (singulier/pluriel) ────────────────────
    public string GroupsLabel => GroupsCount == 1
        ? L.F("Home_GroupsCount_Singular", GroupsCount)
        : L.F("Home_GroupsCount_Plural", GroupsCount);

    public string ProjectsLabel => ProjectsCount == 1
        ? L.F("Home_ProjectsCount_Singular", ProjectsCount)
        : L.F("Home_ProjectsCount_Plural", ProjectsCount);

    public string CreditsLabel => L.F("Home_CreditsCount", Credits);

    /// <summary>
    /// Bool exposé pour binder IsVisible sur le badge rouge (n'affiche
    /// le badge que s'il y a au moins une invitation en attente).
    /// </summary>
    public bool HasPendingInvitations => PendingInvitationsCount > 0;

    /// <summary>
    /// Texte affiché DANS le badge rouge.
    /// "1".."99" si peu d'invitations, "99+" au-delà pour ne pas
    /// déborder du petit cercle. Style Messenger.
    /// </summary>
    public string PendingInvitationsBadge => PendingInvitationsCount > 99
        ? "99+"
        : PendingInvitationsCount.ToString();

    /// <summary>
    /// Sous-titre de la mini-card "Invitations" : "X en attente" si
    /// au moins une, "Aucune" sinon.
    /// </summary>
    public string PendingInvitationsLabel => PendingInvitationsCount > 0
        ? L.F("Home_InvitationsCount_Pending", PendingInvitationsCount)
        : L.T("Home_InvitationsCount_None");

    partial void OnGroupsCountChanged(int value) => OnPropertyChanged(nameof(GroupsLabel));
    partial void OnProjectsCountChanged(int value) => OnPropertyChanged(nameof(ProjectsLabel));
    partial void OnCreditsChanged(int value) => OnPropertyChanged(nameof(CreditsLabel));
    partial void OnPendingInvitationsCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasPendingInvitations));
        OnPropertyChanged(nameof(PendingInvitationsBadge));
        OnPropertyChanged(nameof(PendingInvitationsLabel));
    }

    // ─── Chargement ───────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            await _authService.EnsureProfileExistsAsync();

            var profile = await _authService.GetCurrentProfileAsync();
            if (profile != null)
                Username = profile.Username;

            Credits = await _creditService.GetCreditsAsync();

            var projects = await _projectService.GetMyProjectsAsync();
            ProjectsCount = projects.Count;

            var groups = await _groupService.GetMyGroupsAsync();
            GroupsCount = groups.Count;

            // ⚡ Compteur d'invitations en attente (pour le badge).
            // Total = demandes d'ami reçues + invitations à séries reçues.
            // try/catch séparé pour ne pas casser la home si une source
            // échoue (le badge tombe à ce qui a été chargé avec succès).
            try
            {
                // Chargement parallèle des 2 sources
                var seriesPendingTask = _invitationService.GetMyPendingInvitationsAsync();
                var friendsTask = _friendService.GetFriendsAsync();
                await Task.WhenAll(seriesPendingTask, friendsTask);

                var seriesPendingCount = seriesPendingTask.Result.Count;
                // CanAccept = invitation d'ami reçue en attente (pas envoyée)
                var friendsPendingCount = friendsTask.Result.Count(f => f.CanAccept);

                PendingInvitationsCount = seriesPendingCount + friendsPendingCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Home] LoadInvitations: {ex.Message}");
                PendingInvitationsCount = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Home] LoadAsync: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Saisie code en haut de page ──────────────────────────────
    [RelayCommand]
    private async Task JoinByCodeAsync()
    {
        var code = JoinCode?.Trim().ToUpper() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            JoinError = L.T("Home_JoinErrorEmpty");
            HasJoinError = true;
            return;
        }

        HasJoinError = false;
        IsJoining = true;
        try
        {
            var (success, error, series) = await _seriesService.JoinSeriesByCodeAsync(code);

            if (!success || series == null)
            {
                JoinError = string.IsNullOrEmpty(error) ? L.T("Home_JoinErrorInvalid") : error;
                HasJoinError = true;
                return;
            }

            JoinCode = string.Empty;
            await Shell.Current.GoToAsync($"SeriesDetailPage?seriesId={series.Id}");
        }
        catch (Exception ex)
        {
            JoinError = L.T("Home_JoinErrorNetwork");
            HasJoinError = true;
            System.Diagnostics.Debug.WriteLine($"[Home] Join: {ex.Message}");
        }
        finally
        {
            IsJoining = false;
        }
    }

    // ─── Navigation : grandes cartes ──────────────────────────────
    [RelayCommand]
    private async Task GoToVoteAsync()
        => await Shell.Current.GoToAsync("//VoteFeedPage");

    /// <summary>
    /// ⚡ NOUVEAU : la carte violette "Créer un quiz ou une partie" ouvre
    /// l'écran de choix de contexte (sans groupe / nouveau / existant).
    /// </summary>
    [RelayCommand]
    private async Task GoToCreateAsync()
        => await Shell.Current.GoToAsync("CreateContextPage");

    // ─── Navigation : mini-cards ──────────────────────────────────
    [RelayCommand]
    private async Task GoToGroupsAsync()
        => await Shell.Current.GoToAsync("GroupsPage");

    /// <summary>
    /// ⚡ MAI 2026 : ex "GoToMySeriesAsync", renommé pour refléter que
    /// la cible est la page des invitations reçues (MyInvitationsPage),
    /// qui montre désormais demandes d'ami + invitations à séries.
    /// </summary>
    [RelayCommand]
    private async Task GoToInvitationsAsync()
        => await Shell.Current.GoToAsync("MyInvitationsPage");

    [RelayCommand]
    private async Task GoToMyProjectsAsync()
        => await Shell.Current.GoToAsync("MyProjectsPage");

    [RelayCommand]
    private async Task GoToCreditsAsync()
        => await Shell.Current.GoToAsync("CreditsPage");
}
