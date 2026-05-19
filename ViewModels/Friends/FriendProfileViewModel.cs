using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Models;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Friends;

[QueryProperty(nameof(FriendshipId), "friendshipId")]
[QueryProperty(nameof(FriendUserId), "userId")]
public partial class FriendProfileViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly FriendService _friendService;
    private readonly AppSettingsService _settingsService;
    private readonly ChatService _chatService;
    private readonly VoteService _voteService;
    private readonly AuthService _authService;
    private readonly Supabase.Client _supabase;

    public FriendProfileViewModel(
        ProjectService projectService,
        FriendService friendService,
        AppSettingsService settingsService,
        ChatService chatService,
        VoteService voteService,
        AuthService authService,
        Supabase.Client supabase)
    {
        _projectService = projectService;
        _friendService = friendService;
        _settingsService = settingsService;
        _chatService = chatService;
        _voteService = voteService;
        _authService = authService;
        _supabase = supabase;
    }

    [ObservableProperty] private string _friendshipId = string.Empty;
    [ObservableProperty] private string _friendUserId = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string? _avatarUrl;
    [ObservableProperty] private string _avatarInitial = string.Empty;
    [ObservableProperty] private bool _hasAvatar = false;
    [ObservableProperty] private bool _isOnline = false;
    [ObservableProperty] private string _onlineStatusText = string.Empty;
    [ObservableProperty] private Color _onlineColor = Color.FromArgb("#8A6F4A");
    [ObservableProperty] private int _projectCount = 0;
    [ObservableProperty] private int _voteCount = 0;
    [ObservableProperty] private List<FriendProjectItem> _projects = new();
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isEmpty = false;
    [ObservableProperty] private bool _chatEnabled = false;
    [ObservableProperty] private bool _showVoteResults = false;

    public async Task LoadProfileAsync()
    {
        if (string.IsNullOrEmpty(FriendUserId)) return;

        IsLoading = true;
        try
        {
            var profileResult = await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, FriendUserId)
                .Get();

            var profile = profileResult.Models.FirstOrDefault();
            if (profile == null) return;

            Username = profile.Username;
            AvatarUrl = profile.AvatarUrl;
            HasAvatar = !string.IsNullOrEmpty(profile.AvatarUrl);
            AvatarInitial = profile.Username.Length > 0
                ? profile.Username[0].ToString().ToUpper() : "?";

            if (profile.IsOnline)
            { OnlineStatusText = L.T("Profile_Online"); OnlineColor = Color.FromArgb("#4A7A52"); }
            else if (profile.IsRecent)
            { OnlineStatusText = L.T("Profile_RecentlyOnline"); OnlineColor = Color.FromArgb("#C9943E"); }
            else
            { OnlineStatusText = string.Empty; OnlineColor = Color.FromArgb("#8A6F4A"); }

            ChatEnabled = await _settingsService.IsFriendChatEnabledAsync();
            ShowVoteResults = await _settingsService.ShowVoteResultsAsync();

            var allProjects = await _supabase
                .From<Project>()
                .Filter("owner_id", Postgrest.Constants.Operator.Equals, FriendUserId)
                .Filter("status", Postgrest.Constants.Operator.Equals, "approved")
                .Get();

            var projectList = allProjects.Models.OrderByDescending(p => p.CreatedAt).ToList();
            ProjectCount = projectList.Count;

            if (projectList.Count == 0)
            {
                Projects = new();
                IsEmpty = true;
                return;
            }

            var projectIds = projectList.Select(p => p.Id).ToList();
            var allPhotos = await _projectService.GetProjectPhotosForIdsAsync(projectIds);
            var photosByProject = allPhotos
                .GroupBy(p => p.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var votedPhotoIds = await GetVotedProjectIdsAsync();

            var items = projectList.Select(p =>
            {
                var photos = photosByProject.GetValueOrDefault(p.Id, new());
                var thumb = photos.FirstOrDefault(ph =>
                    ph.Side == "single" || ph.Side == "left")?.Url ?? string.Empty;

                return new FriendProjectItem
                {
                    Id = p.Id,
                    Title = p.Title,
                    Type = p.Type,
                    OwnerId = p.OwnerId,
                    ThumbnailUrl = thumb,
                    IsPrivate = p.IsPrivate,
                    AlreadyVoted = votedPhotoIds.Contains(p.Id),
                    CreatedAt = p.CreatedAt
                };
            }).ToList();

            Projects = items;
            IsEmpty = items.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendProfile] error: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    private async Task<HashSet<string>> GetVotedProjectIdsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return new();

            var photoVotes = await _supabase.From<PhotoVote>()
                .Where(v => v.UserId == userId).Get();
            var duelVotes = await _supabase.From<DuelVote>()
                .Where(v => v.UserId == userId).Get();

            return photoVotes.Models.Select(v => v.ProjectId)
                .Concat(duelVotes.Models.Select(v => v.ProjectId))
                .ToHashSet();
        }
        catch { return new(); }
    }

    [RelayCommand]
    
    private async Task OpenProjectAsync(FriendProjectItem item)
    {
        if (item == null) return;

        try
        {
            var photos = await _projectService.GetProjectPhotosAsync(item.Id);
            var single = photos.FirstOrDefault(p => p.Side == "single");
            var left = photos.FirstOrDefault(p => p.Side == "left");
            var right = photos.FirstOrDefault(p => p.Side == "right");

            var detailPage = IPlatformApplication.Current?.Services
                .GetService<BeauOuPas.Views.Projects.ProjectDetailPage>();

            if (detailPage == null) return;

            if (detailPage.BindingContext is ProjectDetailViewModel detailVm)
            {
                await detailVm.ShowAsync(
                    projectId: item.Id,
                    type: item.Type,
                    title: item.Title,
                    photoUrl: single?.Url ?? string.Empty,
                    photoLeftUrl: left?.Url ?? string.Empty,
                    photoRightUrl: right?.Url ?? string.Empty,
                    ownerId: item.OwnerId);
            }

            await Shell.Current.Navigation.PushAsync(detailPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendProfile] Project nav: {ex.Message}");
        }
    }
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        try
        {
            IsLoading = true;
            var chatId = await _chatService.GetOrCreateChatIdAsync(FriendUserId);
            if (!string.IsNullOrEmpty(chatId))
                await Shell.Current.GoToAsync($"ChatPage?chatId={chatId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendProfile] Chat: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RemoveFriendAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            L.T("Friends_DeleteTitle"),
            $"{L.T("Friends_DeleteConfirm")} @{Username} ?",
            L.T("Common_Yes"), L.T("Common_Cancel"));
        if (!confirm) return;

        var success = await _friendService.DeleteFriendshipAsync(FriendshipId);
        if (success)
            await Shell.Current.GoToAsync("..");
        else
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"), L.T("Friends_DeleteError"), L.T("Common_OK"));
    }
}