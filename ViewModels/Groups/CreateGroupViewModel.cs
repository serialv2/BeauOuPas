using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Groups;

// ⚡ MODIFIÉ : ajout d'un QueryProperty "ChainCreate".
// Quand l'utilisateur arrive depuis CreateContextPage (option "Nouveau groupe"),
// ce flag est mis à true. Une fois le groupe créé, on enchaîne automatiquement
// sur CreateSeriesTypePage avec le GroupId tout neuf — au lieu de revenir
// simplement en arrière.
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

    /// <summary>
    /// Si vrai, après création du groupe on enchaîne sur CreateSeriesTypePage
    /// au lieu de revenir en arrière. Reçu via QueryProperty depuis
    /// CreateContextPage.
    /// </summary>
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

        // Forcer le rafraîchissement de la liste
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
            await Shell.Current.DisplayAlert("Erreur", "Le nom du groupe est obligatoire.", "OK");
            return;
        }

        IsLoading = true;
        try
        {
            var group = await _groupService.CreateGroupAsync(Name.Trim(), Description.Trim());
            if (group == null)
            {
                await Shell.Current.DisplayAlert("Erreur", "Impossible de créer le groupe.", "OK");
                return;
            }

            // Ajouter les amis sélectionnés
            var selected = Friends.Where(f => f.IsSelected).ToList();
            foreach (var friend in selected)
                await _groupService.AddMemberAsync(group.Id, friend.UserId);

            await Shell.Current.DisplayAlert("✅ Groupe créé !",
                $"Le groupe \"{group.Name}\" a été créé avec {selected.Count + 1} membre(s).", "Super !");

            // ⚡ NOUVEAU : si on est dans le flow "créer groupe puis créer série",
            // on enchaîne sur CreateSeriesTypePage avec le GroupId tout neuf.
            // Sinon comportement classique : retour en arrière.
            if (ChainCreate)
            {
                // On retire d'abord la page courante (CreateGroupPage) pour
                // que le retour arrière depuis CreateSeriesTypePage ne ramène
                // pas l'utilisateur sur le formulaire de création de groupe.
                // On revient à HomePage puis on pousse CreateSeriesTypePage
                // avec le GroupId tout neuf.
                await Shell.Current.GoToAsync("..");
                await Task.Delay(50); // laisse le temps à la nav de se stabiliser
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
            await Shell.Current.DisplayAlert("Erreur", ex.Message, "OK");
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
