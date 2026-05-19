using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Home;

/// <summary>
/// Écran "Où créer ?" — proposé après un tap sur la carte violette
/// "Créer un quiz ou une partie" de la HomePage. L'utilisateur choisit
/// le contexte de création :
///   1. Sans groupe → CreateSeriesTypePage avec GroupId=null
///   2. Nouveau groupe → CreateGroupPage en mode "chain" puis enchaîne
///      sur CreateSeriesTypePage avec le GroupId tout neuf
///   3. Dans un groupe existant → PickGroupForCreationPage puis enchaîne
///      sur CreateSeriesTypePage avec le GroupId choisi
///
/// La 3e option est cachée si l'utilisateur n'a aucun groupe.
/// </summary>
public partial class CreateContextViewModel : ObservableObject
{
    private readonly GroupService _groupService;

    public CreateContextViewModel(GroupService groupService)
    {
        _groupService = groupService;
    }

    /// <summary>Vrai si l'utilisateur a au moins un groupe.</summary>
    [ObservableProperty] private bool _hasGroups = false;

    [ObservableProperty] private bool _isLoading = true;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var groups = await _groupService.GetMyGroupsAsync();
            HasGroups = groups.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateContext] Load: {ex.Message}");
            HasGroups = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Choix 1 : sans groupe. On va directement à CreateSeriesTypePage
    /// sans GroupId — cela créera une série "standalone" (rattachée à
    /// l'utilisateur, pas à un groupe).
    /// </summary>
    [RelayCommand]
    private async Task ChooseStandaloneAsync()
    {
        await Shell.Current.GoToAsync("CreateSeriesTypePage");
    }

    /// <summary>
    /// Choix 2 : nouveau groupe. On ouvre CreateGroupPage avec le flag
    /// ChainCreate=true. Une fois le groupe créé, le ViewModel enchaîne
    /// automatiquement sur CreateSeriesTypePage avec le nouveau GroupId.
    /// </summary>
    [RelayCommand]
    private async Task ChooseNewGroupAsync()
    {
        await Shell.Current.GoToAsync("CreateGroupPage?ChainCreate=true");
    }

    /// <summary>
    /// Choix 3 : groupe existant. On ouvre l'écran de sélection.
    /// </summary>
    [RelayCommand]
    private async Task ChooseExistingGroupAsync()
    {
        await Shell.Current.GoToAsync("PickGroupForCreationPage");
    }
}
