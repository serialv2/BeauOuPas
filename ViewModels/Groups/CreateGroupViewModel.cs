using BeauOuPas.Localization;
using BeauOuPas.Models;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static Android.Util.EventLogTags;
using static Java.Util.Jar.Attributes;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(ChainCreate), "ChainCreate")]
public partial class CreateGroupViewModel : ObservableObject
{
    private readonly GroupService _groupService;
    private readonly FriendService _friendService;

    public CreateGroupViewModel(GroupService groupService, FriendService friendService)
    {
        _groupService = groupService;
        _friendService = friendService;
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isLoadingFriends = false;
    [ObservableProperty] private List<FriendSelectItem> _friends = new();
    [ObservableProperty] private bool _hasFriends = false;
    [ObservableProperty] private bool _chainCreate = false;

    public int SelectedCount => Friends.Count(f => f.IsSelected);

    [RelayCommand]
    public async Task LoadFriendsAsync()
    {
        IsLoadingFriends = true;
        try
        {
            var friendItems = await _friendService.GetFriendsAsync();
            var accepted = friendItems.Where(f => f.IsAccepted).ToList();

            Friends = accepted.Select(f => new FriendSelectItem
            {
                UserId = f.UserId,
                Username = f.Username,
                AvatarUrl = f.AvatarUrl,
                IsSelected = false
            }).ToList();

            HasFriends = Friends.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadFriends: {ex.Message}");
        }
        finally { IsLoadingFriends = false; }
    }

    [RelayCommand]
    private void ToggleFriend(FriendSelectItem friend)
    {
        var idx = Friends.FindIndex(f => f.UserId == friend.UserId);
        if (idx < 0) return;

        Friends[idx].IsSelected = !Friends[idx].IsSelected;

        var updated = Friends.ToList();
        Friends = null!;
        Friends = updated;

        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateGroup_NameRequired"), L.T("Common_OK"));
            return;
        }

        IsLoading = true;
        try
        {
            var group = await _groupService.CreateGroupAsync(Name.Trim(), Description.Trim());
            if (group == null)
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateGroup_CreateFailed"), L.T("Common_OK"));
                return;
            }

            var selected = Friends.Where(f => f.IsSelected).ToList();
            foreach (var friend in selected)
                await _groupService.AddMemberAsync(group.Id, friend.UserId);

            await Shell.Current.DisplayAlert(L.T("CreateGroup_Created_Title"),
                L.F("CreateGroup_Created_Msg", group.Name, selected.Count + 1), L.T("CreateGroup_Created_OK"));

            if (ChainCreate)
            {
                await Shell.Current.GoToAsync("..");
                await Task.Delay(50);
                await Shell.Current.GoToAsync("CreateSeriesTypePage",
                    new Dictionary<string, object>
                    {
                        { "GroupId", group.Id },
                        { "GroupName", group.Name }
                    });
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroup: {ex.Message}");
            await Shell.Current.DisplayAlert(L.T("Common_Error"), ex.Message, L.T("Common_OK"));
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}

public class FriendSelectItem
{
    public string FirstLetter => Username.Length > 0 ? Username[0].ToString() : "?";
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsSelected { get; set; }
    public string CheckIcon => IsSelected ? "✓" : string.Empty;
    public Color BorderColor => IsSelected
        ? Color.FromArgb("#C2754C")
        : Color.FromArgb("#E5DCC9");
    public Color CheckBgColor => IsSelected
        ? Color.FromArgb("#C2754C")
        : Color.FromArgb("#E5DCC9");
}