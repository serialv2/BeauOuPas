using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Home;

/// <summary>
/// Écran de choix d'un groupe existant pour y créer une série/quiz.
/// L'utilisateur arrive ici depuis CreateContextPage (3e option).
/// Une fois un groupe sélectionné, on enchaîne sur CreateSeriesTypePage
/// avec le GroupId/GroupName en paramètres (les params attendus par
/// CreateSeriesTypeViewModel via [QueryProperty]).
/// </summary>
public partial class PickGroupForCreationViewModel : ObservableObject
{
    private readonly GroupService _groupService;

    public PickGroupForCreationViewModel(GroupService groupService)
    {
        _groupService = groupService;
    }

    [ObservableProperty] private List<Group> _groups = new();
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasNoGroups = false;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Groups = await _groupService.GetMyGroupsAsync();
            HasNoGroups = Groups.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PickGroup] Load: {ex.Message}");
            Groups = new();
            HasNoGroups = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PickGroupAsync(Group group)
    {
        if (group == null) return;

        // On enchaîne sur l'écran de choix Vote/Quiz, avec le contexte de groupe.
        // Les noms de paramètres respectent les [QueryProperty] de
        // CreateSeriesTypeViewModel : "GroupId" et "GroupName".
        await Shell.Current.GoToAsync("CreateSeriesTypePage",
            new Dictionary<string, object>
            {
                { "GroupId", group.Id },
                { "GroupName", group.Name }
            });
    }
}
