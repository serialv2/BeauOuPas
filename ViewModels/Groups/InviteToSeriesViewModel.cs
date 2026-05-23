using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.Localization;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class InviteToSeriesViewModel : ObservableObject
{
    private readonly SeriesInvitationService _invitationService;

    public InviteToSeriesViewModel(SeriesInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isSearching = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<UserInviteItem> Results { get; } = new();

    private CancellationTokenSource? _searchCts;

    partial void OnSearchQueryChanged(string value)
    {
        // Debounce 350ms : on annule la recherche précédente si l'utilisateur tape
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;
                await MainThread.InvokeOnMainThreadAsync(() => SearchAsync(value));
            }
            catch (TaskCanceledException) { /* normal */ }
        });
    }

    private async Task SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            Results.Clear();
            StatusMessage = string.Empty;
            return;
        }

        IsSearching = true;
        try
        {
            var users = await _invitationService.SearchUsersToInviteAsync(SeriesId, query.Trim());

            Results.Clear();
            foreach (var u in users)
            {
                Results.Add(new UserInviteItem
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    AvatarUrl = u.AvatarUrl,
                    IsFriend = u.IsFriend
                });
            }

            StatusMessage = Results.Count == 0
                ? "Aucun utilisateur trouvé (ou tous déjà invités)"
                : string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InviteSearch: {ex.Message}");
            StatusMessage = "Erreur de recherche";
        }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private async Task InviteAsync(UserInviteItem item)
    {
        if (item == null || item.IsInviting || item.IsInvited) return;

        item.IsInviting = true;
        try
        {
            var (success, error) = await _invitationService.InviteUserAsync(
                SeriesId, item.UserId);

            if (success)
            {
                item.IsInvited = true;
                item.IsInviting = false;
            }
            else
            {
                item.IsInviting = false;
                await Shell.Current.DisplayAlert(L.T("InviteSeries_CannotInvite"), error, L.T("Common_OK"));
            }
        }
        catch (Exception ex)
        {
            item.IsInviting = false;
            System.Diagnostics.Debug.WriteLine($"Invite: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}

/// <summary>État UI d'un utilisateur dans la liste de recherche.</summary>
public partial class UserInviteItem : ObservableObject
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsFriend { get; set; }

    public string AvatarInitial => Username.Length > 0
        ? Username[0].ToString().ToUpper() : "?";
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInvite))]
    private bool _isInviting = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInvite))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private bool _isInvited = false;

    public bool CanInvite => !IsInviting && !IsInvited;
    public string StatusLabel => IsInvited ? "✅ Invité" : "Inviter";
}