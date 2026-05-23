using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Models;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Friends;

public partial class FriendsViewModel : ObservableObject
{
    private readonly FriendService _friendService;

    public FriendsViewModel(FriendService friendService)
    {
        _friendService = friendService;
    }

    // ─── Liste d'amis ────────────────────────────────────────────────
    [ObservableProperty] private List<FriendItem> _friends = new();
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isEmpty = false;

    public int AcceptedCount => Friends.Count(f => f.IsAccepted);
    public int PendingCount => Friends.Count(f => f.IsPending);

    // ─── Recherche ───────────────────────────────────────────────────
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private List<UserSearchResult> _searchResults = new();
    [ObservableProperty] private bool _isSearching = false;
    [ObservableProperty] private bool _isSearchMode = false;
    [ObservableProperty] private bool _hasSearchResults = false;
    [ObservableProperty] private bool _noSearchResults = false;
    [ObservableProperty] private string _searchError = string.Empty;
    [ObservableProperty] private bool _hasSearchError = false;

    // ─── Charger les amis ────────────────────────────────────────────
    [RelayCommand]
    public async Task LoadFriendsAsync()
    {
        IsLoading = true;
        try
        {
            Friends = await _friendService.GetFriendsAsync();
            IsEmpty = Friends.Count == 0;
            OnPropertyChanged(nameof(AcceptedCount));
            OnPropertyChanged(nameof(PendingCount));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadFriends error: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // ─── Rechercher des utilisateurs ────────────────────────────────
    [RelayCommand]
    private async Task SearchUsersAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            ExitSearchMode();
            return;
        }

        IsSearchMode = true;
        IsSearching = true;
        HasSearchResults = false;
        NoSearchResults = false;
        HasSearchError = false;
        SearchResults = new();

        try
        {
            var results = await _friendService.SearchUsersAsync(SearchQuery);
            SearchResults = results;
            HasSearchResults = results.Count > 0;
            NoSearchResults = results.Count == 0;
        }
        catch (Exception ex)
        {
            HasSearchError = true;
            SearchError = ex.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    // ─── Quitter la recherche ────────────────────────────────────────
    [RelayCommand]
    private void ExitSearchMode()
    {
        IsSearchMode = false;
        SearchQuery = string.Empty;
        SearchResults = new();
        HasSearchResults = false;
        NoSearchResults = false;
        HasSearchError = false;
    }

    // ─── Envoyer une demande d'ami ───────────────────────────────────
    [RelayCommand]
    private async Task SendFriendRequestAsync(UserSearchResult user)
    {
        if (user == null) return;

        var (success, error) = await _friendService.SendFriendRequestAsync(user.UserId);

        if (success)
        {
            // Mettre à jour le statut dans la liste de résultats
            var updated = SearchResults.ToList();
            var idx = updated.FindIndex(u => u.UserId == user.UserId);
            if (idx >= 0)
            {
                updated[idx] = new UserSearchResult
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    RelationStatus = "pending_sent"
                };
                SearchResults = updated;
            }
        }
        else
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
        }
    }

    // ─── Accepter une demande depuis la recherche ────────────────────
    [RelayCommand]
    private async Task AcceptFromSearchAsync(UserSearchResult user)
    {
        if (user?.FriendshipId == null) return;

        var success = await _friendService.AcceptFriendshipAsync(user.FriendshipId);
        if (success)
        {
            var updated = SearchResults.ToList();
            var idx = updated.FindIndex(u => u.UserId == user.UserId);
            if (idx >= 0)
            {
                updated[idx] = new UserSearchResult
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    RelationStatus = "friend",
                    FriendshipId = user.FriendshipId
                };
                SearchResults = updated;
            }
            await LoadFriendsAsync();
        }
    }

    // ─── Invitation ──────────────────────────────────────────────────
    [RelayCommand]
    private async Task InviteFriendAsync()
    {
        try
        {
            var link = await _friendService.GenerateInviteLinkAsync();

            if (string.IsNullOrWhiteSpace(link))
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"),
                    L.T("Friends_InviteLinkFailed"), L.T("Common_OK"));
                return;
            }

            var shareText =
                $"Hey 😄 j'hésite entre 2 styles\n\n" +
                $"Tu peux m'aider à choisir ?\n" +
                $"Ça prend 2 secondes 👇\n" +
                $"{link}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Invitation BeauOuPas",
                Text = shareText
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InviteFriend error: {ex.Message}");
            await Shell.Current.DisplayAlert(L.T("Common_Error"),
                L.T("Friends_ShareFailed"), L.T("Common_OK"));
        }
    }

    // ─── Accepter une demande ────────────────────────────────────────
    [RelayCommand]
    private async Task AcceptFriendAsync(FriendItem friend)
    {
        var success = await _friendService.AcceptFriendshipAsync(friend.FriendshipId);
        if (success)
            await LoadFriendsAsync();
        else
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"), L.T("Friends_AcceptError"), L.T("Common_OK"));
    }

    // ─── Supprimer un ami ────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteFriendAsync(FriendItem friend)
    {
        var label = friend.IsAccepted
            ? L.T("Friends_DeleteConfirm")
            : L.T("Friends_CancelConfirm");

        var confirm = await Shell.Current.DisplayAlert(
            L.T("Friends_DeleteTitle"),
            $"{label} @{friend.Username} ?",
            L.T("Common_Yes"), L.T("Common_Cancel"));

        if (!confirm) return;

        var success = await _friendService.DeleteFriendshipAsync(friend.FriendshipId);
        if (success)
            await LoadFriendsAsync();
        else
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"), L.T("Friends_DeleteError"), L.T("Common_OK"));
    }

    // ─── Ouvrir le profil d'un ami ───────────────────────────────────
    [RelayCommand]
    private async Task OpenFriendProfileAsync(FriendItem friend)
    {
        if (friend == null || !friend.IsAccepted) return;

        await Shell.Current.GoToAsync(
            $"FriendProfileDetail?userId={friend.UserId}&friendshipId={friend.FriendshipId}");
    }
}