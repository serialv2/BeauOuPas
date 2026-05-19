using CommunityToolkit.Mvvm.ComponentModel;
using BeauOuPas.Localization;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;
using BeauOuPas.Models;

namespace BeauOuPas.ViewModels.Vote;

public partial class VoteFeedViewModel : ObservableObject
{
    private readonly VoteService _voteService;
    private readonly AuthService _authService;
    private readonly CreditService _creditService;
    private readonly AppSettingsService _settingsService;
    private readonly IAdService _adService;
    private readonly ProjectService _projectService;
    private readonly Supabase.Client _supabase;
    private readonly ReportService _reportService;

    private List<FeedItemViewModel> _allProjects = new();
    private readonly HashSet<string> _votedInSession = new();
    private readonly HashSet<string> _shownInSession = new();
    private int _voteCount = 0;
    private bool _isLoadingMore = false;
    private int _feedOffset = 0;
    private string _feedSeed = Guid.NewGuid().ToString("N");
    private BeauOuPas.Models.Profile? _currentProfile;

    private const int PAGE_SIZE = 10;
    private const int PRELOAD_THRESHOLD = 3;

    private readonly Stack<FeedItemViewModel> _rewindHistory = new();

    public VoteFeedViewModel(
        VoteService voteService,
        AuthService authService,
        CreditService creditService,
        AppSettingsService settingsService,
        IAdService adService,
        ProjectService projectService,
        Supabase.Client supabase,
        ReportService reportService)
    {
        _voteService = voteService;
        _authService = authService;
        _creditService = creditService;
        _settingsService = settingsService;
        _adService = adService;
        _projectService = projectService;
        _supabase = supabase;
        _reportService = reportService;
    }

    [ObservableProperty] private FeedItemViewModel? _currentProject;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isEmpty = false;
    [ObservableProperty] private int _remainingCount = 0;
    [ObservableProperty] private string _voteAnimation = string.Empty;
    [ObservableProperty] private bool _showAnimation = false;
    [ObservableProperty] private bool _canRewind = false;
    [ObservableProperty] private bool _meetVisible = false;

    // ─── Sondage ─────────────────────────────────────────────────────
    [ObservableProperty] private List<PollOption> _pollOptions = new();
    [ObservableProperty] private bool _isPollProject = false;

    // ─── Charger le feed ─────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadFeedAsync()
    {
        IsLoading = true;
        IsEmpty = false;
        _votedInSession.Clear();
        _shownInSession.Clear();
        _allProjects.Clear();
        _rewindHistory.Clear();
        CanRewind = false;

        try
        {
            _currentProfile = await _authService.GetCurrentProfileAsync();
            if (_currentProfile == null) return;

            MeetVisible = await _settingsService.IsMeetEnabledAsync();

            // Nouveau moteur de feed : ordre aléatoire stable et exclusions côté Supabase.
            _feedOffset = 0;
            _feedSeed = Guid.NewGuid().ToString("N");

            await LoadMoreProjectsAsync();
            ShowNextProject();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Feed error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Charger plus de projets ──────────────────────────────────────
    private async Task LoadMoreProjectsAsync()
    {
        if (_isLoadingMore || _currentProfile == null) return;
        _isLoadingMore = true;

        try
        {
            var projects = await _voteService.GetFeedProjectsPageAsync(
                _currentProfile, PAGE_SIZE, _feedOffset, _feedSeed);

            _feedOffset += PAGE_SIZE;

            if (projects.Count == 0) return;

            var projectIds = projects.Select(p => p.Id).ToList();
            var allPhotos = await _projectService.GetProjectPhotosForIdsAsync(projectIds);
            var photosByProject = allPhotos
                .GroupBy(p => p.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ownerIds = projects.Select(p => p.OwnerId).Distinct().ToList();
            var allProfiles = await _projectService.GetProfilesForIdsAsync(ownerIds);
            var profilesById = allProfiles.ToDictionary(p => p.Id, p => p);

            foreach (var project in projects)
            {
                if (_shownInSession.Contains(project.Id)) continue;

                var photos = photosByProject.GetValueOrDefault(project.Id, new());
                var left = photos.FirstOrDefault(p => p.Side == "left" || p.Side == "single");
                var right = photos.FirstOrDefault(p => p.Side == "right");

                profilesById.TryGetValue(project.OwnerId, out var ownerProfile);

                // ⚡ Pré-calcul des URLs thumbnail (300px) pour les backgrounds flous
                // → évite de charger 2× la même image 1080px en RAM dans VoteFeedPage.
                var leftUrl = left?.Url ?? string.Empty;
                var rightUrl = right?.Url ?? string.Empty;

                _allProjects.Add(new FeedItemViewModel
                {
                    Id = project.Id,
                    Title = project.Title,
                    Description = project.Description ?? string.Empty,
                    OwnerId = project.OwnerId,
                    OwnerUsername = ownerProfile?.Username ?? string.Empty,
                    Type = project.Type,
                    PhotoUrl = leftUrl,
                    PhotoLeftUrl = leftUrl,
                    PhotoRightUrl = rightUrl,
                    PhotoThumbUrl = FeedItemViewModel.BuildThumbUrl(leftUrl),
                    PhotoLeftThumbUrl = FeedItemViewModel.BuildThumbUrl(leftUrl),
                    PhotoRightThumbUrl = FeedItemViewModel.BuildThumbUrl(rightUrl),
                    GenderFilter = project.GenderFilter,
                    MinAge = project.MinAge,
                    MaxAge = project.MaxAge
                });

                _shownInSession.Add(project.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadMore error: {ex.Message}");
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    // ─── Afficher le prochain projet ─────────────────────────────────
    private void ShowNextProject()
    {
        IsPollProject = false;
        PollOptions = new();

        if (_allProjects.Any())
        {
            CurrentProject = _allProjects.First();
            _allProjects.RemoveAt(0);
            RemainingCount = _allProjects.Count;
            IsEmpty = false;

            if (CurrentProject.IsPoll)
            {
                IsPollProject = true;
                _ = LoadPollOptionsAsync(CurrentProject.Id);
            }

            if (_allProjects.Count <= PRELOAD_THRESHOLD && !_isLoadingMore)
                _ = LoadMoreProjectsAsync();
        }
        else
        {
            CurrentProject = null;
            IsEmpty = true;
        }
    }

    // ─── Charger options sondage ──────────────────────────────────────
    private async Task LoadPollOptionsAsync(string projectId)
    {
        try
        {
            var result = await _supabase
                .From<PollOption>()
                .Filter("project_id", Postgrest.Constants.Operator.Equals, projectId)
                .Get();

            PollOptions = result.Models.OrderBy(o => o.Position).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Poll] LoadOptions: {ex.Message}");
        }
    }

    // ─── Vote photo ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task VoteAsync(string rating)
    {
        if (CurrentProject == null || _currentProfile == null) return;
        if (!CurrentProject.IsPhotoVote) return;

        var projectId = CurrentProject.Id;
        var ownerId = CurrentProject.OwnerId;
        var ratingInt = int.Parse(rating);

        _rewindHistory.Push(CurrentProject);
        CanRewind = true;
        _votedInSession.Add(projectId);
        _shownInSession.Add(projectId);

        ShowAnimation = false;
        ShowNextProject();

        await _voteService.VotePhotoAsync(projectId, ratingInt, _currentProfile);
        await _creditService.SpendForVoteReceivedAsync(ownerId);
        await HandlePostVoteAsync();
    }

    // ─── Vote duel ────────────────────────────────────────────────────
    [RelayCommand]
    private async Task VoteDuelAsync(string side)
    {
        if (CurrentProject == null || _currentProfile == null) return;
        if (!CurrentProject.IsDuel) return;

        var projectId = CurrentProject.Id;
        var ownerId = CurrentProject.OwnerId;

        _rewindHistory.Push(CurrentProject);
        CanRewind = true;
        _votedInSession.Add(projectId);
        _shownInSession.Add(projectId);

        ShowAnimation = false;
        ShowNextProject();

        await _voteService.VoteDuelAsync(projectId, side, _currentProfile);
        await _creditService.SpendForVoteReceivedAsync(ownerId);
        await HandlePostVoteAsync();
    }

    // ─── Vote sondage ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task VotePollAsync(string optionId)
    {
        if (CurrentProject == null || _currentProfile == null) return;
        if (!CurrentProject.IsPoll) return;

        var projectId = CurrentProject.Id;
        var ownerId = CurrentProject.OwnerId;

        _rewindHistory.Push(CurrentProject);
        CanRewind = true;
        _votedInSession.Add(projectId);
        _shownInSession.Add(projectId);

        ShowAnimation = false;
        ShowNextProject();

        try
        {
            await _supabase.From<PollVote>().Insert(new PollVote
            {
                UserId = _currentProfile.Id,
                ProjectId = projectId,
                OptionId = optionId,
                VoterGender = _currentProfile.Gender,
                VoterAge = _currentProfile.Age > 0 ? _currentProfile.Age : null
            });
            await _creditService.SpendForVoteReceivedAsync(ownerId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Poll] VotePoll: {ex.Message}");
        }

        await HandlePostVoteAsync();
    }

    // ─── Rencontrer ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task MeetAsync()
    {
        if (CurrentProject == null || _currentProfile == null) return;
        if (!CurrentProject.IsPhotoVote) return;

        var meetEnabled = await _settingsService.IsMeetEnabledAsync();
        if (!meetEnabled)
        {
            await Shell.Current.DisplayAlert("Rencontres désactivées",
                "Cette fonctionnalité n'est pas disponible.", "OK");
            return;
        }

        if (!_currentProfile.DatingEnabled)
        {
            await Shell.Current.DisplayAlert("💘 Rencontres",
                "Activez les rencontres dans vos paramètres.", "OK");
            return;
        }

        var cost = await _settingsService.GetCreditsCostMeetAsync();
        var credits = await _creditService.GetCreditsAsync();

        if (credits < cost)
        {
            await Shell.Current.DisplayAlert("💰 Crédits insuffisants",
                $"Il vous faut {cost} crédits.\n\nVous avez : {credits} crédits", "OK");
            return;
        }

        var projectId = CurrentProject.Id;
        var toUserId = CurrentProject.OwnerId;
        var fromUserId = _currentProfile.Id;

        _rewindHistory.Push(CurrentProject);
        CanRewind = true;
        _votedInSession.Add(projectId);
        _shownInSession.Add(projectId);

        var (success, error) = await _creditService.SpendForMeetAsync();
        if (!success) { await Shell.Current.DisplayAlert("Erreur", error, "OK"); return; }

        VoteAnimation = "💘";
        ShowAnimation = true;

        await _voteService.VotePhotoAsync(projectId, 3, _currentProfile);
        await _creditService.SpendForVoteReceivedAsync(toUserId);
        ShowNextProject();
        await HandlePostVoteAsync();

        var isMatch = await RequestMeetAsync(fromUserId, toUserId, projectId);

        await Task.Delay(500);
        ShowAnimation = false;

        if (isMatch)
        {
            await Shell.Current.DisplayAlert("💘 C'est un Match !",
                "Vous vous êtes mutuellement choisis !\nVous pouvez maintenant vous envoyer des messages.",
                "🎉 Super !");
        }
    }

    // ─── Rewind ───────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RewindAsync()
    {
        if (IsLoading) return;

        if (_rewindHistory.Count == 0)
        {
            await Shell.Current.DisplayAlert("Rewind", "Aucun vote à annuler.", "OK");
            return;
        }

        var (success, error) = await _creditService.SpendForRewindAsync();
        if (!success)
        {
            await Shell.Current.DisplayAlert("💰 Crédits insuffisants", error, "OK");
            return;
        }

        IsLoading = true;
        try
        {
            var projectToRewind = _rewindHistory.Pop();
            CanRewind = _rewindHistory.Count > 0;

            bool deleteOk;
            if (projectToRewind.IsDuel)
            {
                var (delSuccess, _) = await _voteService.RewindLastDuelVoteAsync();
                deleteOk = delSuccess;
            }
            else if (projectToRewind.IsPoll)
            {
                var lastVote = await _supabase.From<PollVote>()
                    .Filter("user_id", Postgrest.Constants.Operator.Equals, _currentProfile!.Id)
                    .Filter("project_id", Postgrest.Constants.Operator.Equals, projectToRewind.Id)
                    .Get();
                if (lastVote.Models.Any())
                {
                    await _supabase.From<PollVote>()
                        .Filter("id", Postgrest.Constants.Operator.Equals, lastVote.Models.First().Id)
                        .Delete();
                    deleteOk = true;

                    // Rembourser le créateur via RPC Supabase
                    await _supabase.Rpc("add_vote_credits", new Dictionary<string, object>
                    {
                        { "p_user_id", projectToRewind.OwnerId }
                    });
                }
                else deleteOk = false;
            }
            else
            {
                var (delSuccess, _) = await _voteService.RewindLastPhotoVoteAsync();
                deleteOk = delSuccess;

                if (delSuccess)
                {
                    // Rembourser le créateur via RPC Supabase
                    await _supabase.Rpc("add_vote_credits", new Dictionary<string, object>
                    {
                        { "p_user_id", projectToRewind.OwnerId }
                    });
                }
            }

            if (!deleteOk)
            {
                await _creditService.AddCreditsAsync(
                    await _settingsService.GetCreditsCostRewindAsync(),
                    "rewind_refund", "Remboursement rewind (erreur)");
                await Shell.Current.DisplayAlert("Erreur", "Impossible d'annuler le vote.", "OK");
                _rewindHistory.Push(projectToRewind);
                CanRewind = true;
                return;
            }

            _votedInSession.Remove(projectToRewind.Id);

            if (CurrentProject != null)
                _allProjects.Insert(0, CurrentProject);

            CurrentProject = projectToRewind;
            RemainingCount = _allProjects.Count;
            IsEmpty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RewindAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Signaler un projet ───────────────────────────────────────────
    [RelayCommand]
    private async Task ReportAsync()
    {
        if (CurrentProject == null) return;

        var projectToReport = CurrentProject;

        var reason = await Shell.Current.DisplayActionSheet(
            L.T("Report_Title"), L.T("Common_Cancel"), null,
            L.T("Report_Nudity"), L.T("Report_Spam"),
            L.T("Report_Fake"), L.T("Report_Other"));

        if (string.IsNullOrEmpty(reason) || reason == L.T("Common_Cancel")) return;

        var success = await _reportService.ReportProjectAsync(projectToReport.Id, reason);

        _votedInSession.Add(projectToReport.Id);
        _shownInSession.Add(projectToReport.Id);
        ShowAnimation = false;
        _rewindHistory.Clear();
        CanRewind = false;
        ShowNextProject();

        if (success)
            await Shell.Current.DisplayAlert("✅", L.T("Report_Success"), L.T("Common_OK"));
    }

    // ─── Post-vote ────────────────────────────────────────────────────
    private async Task HandlePostVoteAsync()
    {
        _voteCount++;
        await _creditService.AddVoteCreditsAsync();

        var adFreq = await _settingsService.GetAdFrequencyAsync();
        if (_voteCount % adFreq == 0)
            await _adService.ShowInterstitialAsync();

        var rewardFreq = await _settingsService.GetAdRewardFrequencyAsync();
        if (_voteCount % rewardFreq == 0)
        {
            var watched = await _adService.ShowRewardedAsync();
            if (watched) await _creditService.AddAdRewardCreditsAsync();
        }
    }

    // ─── Helper RPC rencontre ─────────────────────────────────────────
    private async Task<bool> RequestMeetAsync(
        string fromUserId, string toUserId, string projectId)
    {
        try
        {
            var result = await _supabase.Rpc<bool>(
                "request_meet",
                new Dictionary<string, object>
                {
                    { "p_from_user_id", fromUserId },
                    { "p_to_user_id",   toUserId },
                    { "p_project_id",   projectId }
                });
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Meet error: {ex.Message}");
            return false;
        }
    }
}

// ─── ViewModel item du feed ───────────────────────────────────────────
public class FeedItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    // ─── URLs HD (1080px) — pour la photo principale (AspectFit) ────
    public string PhotoUrl { get; set; } = string.Empty;
    public string PhotoLeftUrl { get; set; } = string.Empty;
    public string PhotoRightUrl { get; set; } = string.Empty;

    // ─── URLs basse résolution (300px) — pour les backgrounds flous ──
    // Calculées une seule fois à la création de l'item.
    // ⚠️ Le cadrage des thumbs peut différer (recadrage à l'upload),
    // MAIS c'est invisible : ces URLs ne servent QUE pour les
    // backgrounds AspectFill avec Opacity faible et voile noir.
    public string PhotoThumbUrl { get; set; } = string.Empty;
    public string PhotoLeftThumbUrl { get; set; } = string.Empty;
    public string PhotoRightThumbUrl { get; set; } = string.Empty;

    public string GenderFilter { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public int MaxAge { get; set; }

    public bool IsPhotoVote => Type == "photo_vote";
    public bool IsDuel => Type == "duel";
    public bool IsPoll => Type == "poll";
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>
    /// Convertit une URL "display/" (1080px) en URL "thumbnails/" (300px).
    /// Convention de stockage Supabase : les images sont uploadées en 2
    /// versions dans le bucket "photos" :
    ///   - .../photos/display/{userId}/{timestamp}.jpg     (1080px)
    ///   - .../photos/thumbnails/{userId}/{timestamp}.jpg  (300px)
    /// Voir PhotoUploadService.UploadPhotoAsync.
    /// Si l'URL n'est pas au format attendu, on retourne l'URL d'origine.
    /// </summary>
    public static string BuildThumbUrl(string displayUrl)
    {
        if (string.IsNullOrEmpty(displayUrl)) return string.Empty;
        return displayUrl.Replace("/photos/display/", "/photos/thumbnails/");
    }
}