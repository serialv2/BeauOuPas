using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Models.Stats;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels;

public partial class ProjectDetailViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly VoteService _voteService;
    private readonly Supabase.Client _supabase;
    private readonly AppSettingsService _settingsService;

    private string _projectId = string.Empty;
    private string _projectType = string.Empty;

    public ProjectDetailViewModel(
        ProjectService projectService,
        VoteService voteService,
        Supabase.Client supabase,
        AppSettingsService settingsService)
    {
        _projectService = projectService;
        _voteService = voteService;
        _supabase = supabase;
        _settingsService = settingsService;
    }

    [ObservableProperty] private bool _isVisible = false;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isSubmittingVote = false;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _photoUrl = string.Empty;
    [ObservableProperty] private string _photoLeftUrl = string.Empty;
    [ObservableProperty] private string _photoRightUrl = string.Empty;

    [ObservableProperty] private bool _isPhoto = false;
    [ObservableProperty] private bool _isDuel = false;
    [ObservableProperty] private bool _isPoll = false;   // ← NOUVEAU

    [ObservableProperty] private string _ownerUsername = string.Empty;
    [ObservableProperty] private string _ownerAvatar = string.Empty;
    [ObservableProperty] private int _ownerAge = 0;

    [ObservableProperty] private PhotoVoteStats? _photoStats;
    [ObservableProperty] private DuelVoteStats? _duelStats;
    [ObservableProperty] private List<PollOption> _pollStats = new(); // ← NOUVEAU

    [ObservableProperty] private bool _hasPhotoStats = false;
    [ObservableProperty] private bool _hasDuelStats = false;
    [ObservableProperty] private bool _hasPollStats = false;  // ← NOUVEAU
    [ObservableProperty] private bool _noStats = false;

    [ObservableProperty] private bool _statsHiddenByAdmin = false;

    [ObservableProperty] private bool _hasVoted = false;
    [ObservableProperty] private bool _showVoteResults = false;

    // ⚡ NOUVEAU : true quand le proprietaire ouvre la popup depuis "Mes projets".
    //   Dans ce mode : pas de zone de vote, on charge directement les stats.
    [ObservableProperty] private bool _isOwnerView = false;

    public bool CanVotePhoto =>
        IsPhoto && !HasVoted && !IsLoading && !IsSubmittingVote && !IsOwnerView;

    public bool CanVoteDuel =>
        IsDuel && !HasVoted && !IsLoading && !IsSubmittingVote && !IsOwnerView;

    public bool CanVotePoll =>
        IsPoll && !HasVoted && !IsLoading && !IsSubmittingVote && !IsOwnerView;  // ← NOUVEAU

    public bool VoteDoneStatsHidden =>
        HasVoted && !ShowVoteResults && !IsPoll;  // Les sondages montrent toujours les résultats

    public async Task ShowAsync(
        string projectId,
        string type,
        string title,
        string photoUrl,
        string photoLeftUrl,
        string photoRightUrl,
        string ownerId,
        bool isOwnerView = false)   // ⚡ NOUVEAU : true depuis "Mes projets"
    {
        _projectId = projectId;
        _projectType = type;
        IsOwnerView = isOwnerView;  // ⚡ NOUVEAU

        Title = title;
        PhotoUrl = photoUrl;
        PhotoLeftUrl = photoLeftUrl;
        PhotoRightUrl = photoRightUrl;

        IsPhoto = type == "photo_vote";
        IsDuel = type == "duel";
        IsPoll = type == "poll";  // ← NOUVEAU

        PhotoStats = null;
        DuelStats = null;
        PollStats = new List<PollOption>();  // ← NOUVEAU

        HasPhotoStats = false;
        HasDuelStats = false;
        HasPollStats = false;  // ← NOUVEAU
        NoStats = false;
        StatsHiddenByAdmin = false;

        HasVoted = false;
        ShowVoteResults = false;

        IsLoading = true;
        IsVisible = true;

        RefreshVoteVisibility();

        try
        {
            await LoadOwnerAsync(ownerId);

            ShowVoteResults = await _settingsService.ShowVoteResultsAsync();

            await CheckAlreadyVotedAsync();

            // ⚡ NOUVEAU : en mode proprietaire, on charge directement les stats
            //    sans tenir compte du parametre ShowVoteResults ni du fait
            //    que l'utilisateur ait vote ou non.
            if (IsOwnerView)
            {
                await LoadStatsAsync();
            }
            else if (HasVoted)
            {
                // Les sondages affichent toujours les résultats après vote
                if (ShowVoteResults || IsPoll)
                    await LoadStatsAsync();
                else
                    NoStats = false;
            }
            else
            {
                NoStats = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] error: {ex.Message}");
            NoStats = true;
        }
        finally
        {
            IsLoading = false;
            RefreshVoteVisibility();
        }
    }

    private async Task LoadOwnerAsync(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
            return;

        try
        {
            var ownerResult = await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, ownerId)
                .Get();

            var owner = ownerResult.Models.FirstOrDefault();

            OwnerUsername = owner?.Username ?? string.Empty;
            OwnerAvatar = owner?.AvatarUrl ?? string.Empty;
            OwnerAge = owner?.Age ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] Owner error: {ex.Message}");

            OwnerUsername = string.Empty;
            OwnerAvatar = string.Empty;
            OwnerAge = 0;
        }
    }

    private async Task CheckAlreadyVotedAsync()
    {
        if (IsPhoto)
        {
            var result = await _voteService.HasVotedPhotoAsync(_projectId);
            HasVoted = result.HasVoted;
        }
        else if (IsDuel)
        {
            var result = await _voteService.HasVotedDuelAsync(_projectId);
            HasVoted = result.HasVoted;
        }
        else if (IsPoll)  // ← NOUVEAU
        {
            try
            {
                var userId = _supabase.Auth.CurrentUser?.Id;
                if (!string.IsNullOrEmpty(userId))
                {
                    var result = await _supabase
                        .From<PollVote>()
                        .Where(v => v.UserId == userId && v.ProjectId == _projectId)
                        .Get();
                    HasVoted = result.Models.Any();
                }
            }
            catch
            {
                HasVoted = false;
            }
        }

        RefreshVoteVisibility();
    }

    private async Task LoadStatsAsync()
    {
        PhotoStats = null;
        DuelStats = null;
        PollStats = new List<PollOption>();  // ← NOUVEAU
        HasPhotoStats = false;
        HasDuelStats = false;
        HasPollStats = false;  // ← NOUVEAU
        NoStats = false;
        StatsHiddenByAdmin = false;

        // Pour les photo/duel, respecter le flag admin
        // Pour les sondages, on affiche toujours
        // ⚡ NOUVEAU : en mode proprietaire, on ignore ShowVoteResults
        if (!IsOwnerView && !ShowVoteResults && !IsPoll)
        {
            NoStats = false;
            RefreshVoteVisibility();
            return;
        }

        if (IsPhoto)
        {
            var stats = await _projectService.GetPhotoStatsAsync(_projectId);
            PhotoStats = stats;
            HasPhotoStats = stats != null && stats.TotalVotes > 0;
            NoStats = !HasPhotoStats;
        }
        else if (IsDuel)
        {
            var stats = await _projectService.GetDuelStatsAsync(_projectId);
            DuelStats = stats;
            HasDuelStats = stats != null && stats.TotalVotes > 0;
            NoStats = !HasDuelStats;
        }
        else if (IsPoll)  // ← NOUVEAU
        {
            try
            {
                var stats = await _projectService.GetPollStatsAsync(_projectId);

                PollStats = stats.Select(s =>
                {
                    s.Option.VoteCount = s.Count;
                    s.Option.Percentage = s.Percentage;
                    return s.Option;
                }).ToList();

                HasPollStats = PollStats.Any();
                NoStats = !HasPollStats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProjectDetail] PollStats error: {ex.Message}");
                NoStats = true;
            }
        }

        RefreshVoteVisibility();
    }

    [RelayCommand]
    private async Task VotePhotoAsync(string vote)
    {
        if (IsSubmittingVote || HasVoted || string.IsNullOrEmpty(_projectId))
            return;

        int rating = vote switch
        {
            "like" => 3,
            "neutral" => 2,
            "dislike" => 1,
            _ => 0
        };

        if (rating == 0)
            return;

        IsSubmittingVote = true;
        RefreshVoteVisibility();

        try
        {
            var voterProfile = await GetCurrentProfileAsync();

            if (voterProfile == null)
            {
                await Shell.Current.DisplayAlert(
                    L.T("Common_Error"),
                    L.T("ProjectDetail_NoProfile_Msg"),
                    L.T("Common_OK"));
                return;
            }

            var result = await _voteService.VotePhotoAsync(
                _projectId,
                rating,
                voterProfile);

            if (!result.Success)
            {
                await Shell.Current.DisplayAlert(
                    L.T("ProjectDetail_VoteImpossible_Title"),
                    result.Error,
                    L.T("Common_OK"));
                return;
            }

            HasVoted = true;

            if (ShowVoteResults)
            {
                await LoadStatsAsync();
            }
            else
            {
                NoStats = false;
                HasPhotoStats = false;
                HasDuelStats = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] VotePhoto error: {ex.Message}");

            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("ProjectDetail_VoteError_Msg"),
                L.T("Common_OK"));
        }
        finally
        {
            IsSubmittingVote = false;
            RefreshVoteVisibility();
        }
    }

    [RelayCommand]
    private async Task VoteDuelAsync(string choice)
    {
        if (IsSubmittingVote || HasVoted || string.IsNullOrEmpty(_projectId))
            return;

        string side = choice switch
        {
            "A" => "left",
            "B" => "right",
            "left" => "left",
            "right" => "right",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(side))
            return;

        IsSubmittingVote = true;
        RefreshVoteVisibility();

        try
        {
            var voterProfile = await GetCurrentProfileAsync();

            if (voterProfile == null)
            {
                await Shell.Current.DisplayAlert(
                    L.T("Common_Error"),
                    L.T("ProjectDetail_NoProfile_Msg"),
                    L.T("Common_OK"));
                return;
            }

            var result = await _voteService.VoteDuelAsync(
                _projectId,
                side,
                voterProfile);

            if (!result.Success)
            {
                await Shell.Current.DisplayAlert(
                    L.T("ProjectDetail_VoteImpossible_Title"),
                    result.Error,
                    L.T("Common_OK"));
                return;
            }

            HasVoted = true;

            if (ShowVoteResults)
            {
                await LoadStatsAsync();
            }
            else
            {
                NoStats = false;
                HasPhotoStats = false;
                HasDuelStats = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] VoteDuel error: {ex.Message}");

            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("ProjectDetail_VoteError_Msg"),
                L.T("Common_OK"));
        }
        finally
        {
            IsSubmittingVote = false;
            RefreshVoteVisibility();
        }
    }

    // ─── Vote sondage ─────────────────────────────────────────────── NOUVEAU
    [RelayCommand]
    private async Task VotePollAsync(string optionId)
    {
        if (IsSubmittingVote || HasVoted || string.IsNullOrEmpty(_projectId))
            return;

        IsSubmittingVote = true;
        RefreshVoteVisibility();

        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId))
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("ProjectDetail_NotConnected_Msg"), L.T("Common_OK"));
                return;
            }

            var voterProfile = await GetCurrentProfileAsync();

            await _supabase.From<PollVote>().Insert(new PollVote
            {
                UserId = userId,
                ProjectId = _projectId,
                OptionId = optionId,
                VoterGender = voterProfile?.Gender,
                VoterAge = voterProfile?.Age > 0 ? voterProfile.Age : null
            });

            HasVoted = true;

            // Toujours afficher les résultats après un vote sondage
            await LoadStatsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] VotePoll error: {ex.Message}");

            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("ProjectDetail_VoteError_Msg"),
                L.T("Common_OK"));
        }
        finally
        {
            IsSubmittingVote = false;
            RefreshVoteVisibility();
        }
    }

    private async Task<BeauOuPas.Models.Profile?> GetCurrentProfileAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;

            if (string.IsNullOrEmpty(userId))
                return null;

            var result = await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            return result.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectDetail] Current profile error: {ex.Message}");
            return null;
        }
    }

    private void RefreshVoteVisibility()
    {
        OnPropertyChanged(nameof(CanVotePhoto));
        OnPropertyChanged(nameof(CanVoteDuel));
        OnPropertyChanged(nameof(CanVotePoll));           // ← NOUVEAU
        OnPropertyChanged(nameof(VoteDoneStatsHidden));
    }

    partial void OnIsPhotoChanged(bool value) => RefreshVoteVisibility();
    partial void OnIsDuelChanged(bool value) => RefreshVoteVisibility();
    partial void OnIsPollChanged(bool value) => RefreshVoteVisibility();  // ← NOUVEAU
    partial void OnHasVotedChanged(bool value) => RefreshVoteVisibility();
    partial void OnShowVoteResultsChanged(bool value) => RefreshVoteVisibility();
    partial void OnIsOwnerViewChanged(bool value) => RefreshVoteVisibility();   // ⚡ NOUVEAU
    partial void OnIsLoadingChanged(bool value) => RefreshVoteVisibility();
    partial void OnIsSubmittingVoteChanged(bool value) => RefreshVoteVisibility();

    [RelayCommand]
    public void Close()
    {
        IsVisible = false;
    }
}