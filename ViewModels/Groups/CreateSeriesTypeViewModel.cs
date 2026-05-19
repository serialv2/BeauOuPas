using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeauOuPas.ViewModels.Groups;

// ⚡ E2d : QueryProperty alignés sur MAJUSCULE pour cohérence avec
// CreateSeriesViewModel et CreateQuizViewModel (sinon GroupId arrivait null).
[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
public partial class CreateSeriesTypeViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;

    public CreateSeriesTypeViewModel(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    // ─── Paramètres reçus via Shell ─────────────────────────────────
    [ObservableProperty] private string? _groupId;
    [ObservableProperty] private string? _groupName;

    // ─── État UI ────────────────────────────────────────────────────
    [ObservableProperty] private bool _isQuizCreationEnabled = true;

    // 🎉 PARTIE RAPIDE — état activé/désactivé.
    // Décision : on réutilise le MÊME flag que le quiz
    // (IsQuizCreationEnabledAsync) pour ne pas avoir à créer une
    // nouvelle méthode dans AppSettingsService et garder un comportement
    // cohérent (si la création de quiz est coupée, la partie l'est aussi).
    // → Pour un flag party DÉDIÉ plus tard : voir README-PARTIE-RAPIDE-LOT5.md
    //   (ajouter IsPartyCreationEnabledAsync dans AppSettingsService et
    //    remplacer la ligne indiquée dans LoadAsync).
    [ObservableProperty] private bool _isPartyCreationEnabled = true;

    [ObservableProperty] private bool _isLoading = true;

    /// <summary>
    /// Vrai si la série sera rattachée à un groupe (vs série standalone).
    /// </summary>
    public bool HasGroup => !string.IsNullOrEmpty(GroupId);

    partial void OnGroupIdChanged(string? value) => OnPropertyChanged(nameof(HasGroup));

    // ─── Init ───────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            IsQuizCreationEnabled = await _settingsService.IsQuizCreationEnabledAsync();

            // 🎉 La partie rapide suit le même interrupteur que le quiz.
            // (Si tu ajoutes IsPartyCreationEnabledAsync à AppSettingsService,
            //  remplace la ligne ci-dessous par :
            //    IsPartyCreationEnabled = await _settingsService.IsPartyCreationEnabledAsync();)
            IsPartyCreationEnabled = IsQuizCreationEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateSeriesType] Load error: {ex.Message}");
            IsQuizCreationEnabled = true;  // En cas d'erreur, on n'empêche pas la création
            IsPartyCreationEnabled = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Commandes ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectVoteAsync()
    {
        // ⚡ E2d : alignement MAJUSCULE (GroupId, GroupName) au lieu de minuscule.
        // Le bug précédent faisait que la série de vote n'était plus rattachée au groupe.
        var route = HasGroup
            ? $"CreateSeriesPage?GroupId={GroupId}&GroupName={Uri.EscapeDataString(GroupName ?? "")}"
            : "CreateSeriesPage";

        await Shell.Current.GoToAsync(route);
    }

    [RelayCommand]
    private async Task SelectQuizAsync()
    {
        if (!IsQuizCreationEnabled)
        {
            await Shell.Current.DisplayAlert(
                L.T("CreateSeriesType_QuizDisabledTitle"),
                L.T("CreateSeriesType_QuizDisabledMessage"),
                L.T("Common_OK"));
            return;
        }

        // ⚡ E2d : alignement MAJUSCULE pour que CreateQuizViewModel reçoive bien le GroupId
        var route = HasGroup
            ? $"CreateQuizPage?GroupId={GroupId}&GroupName={Uri.EscapeDataString(GroupName ?? "")}"
            : "CreateQuizPage";

        await Shell.Current.GoToAsync(route);
    }

    // 🎉 PARTIE RAPIDE — calqué EXACTEMENT sur SelectQuizAsync
    // (même vérif d'activation, même schéma de route GroupId/GroupName).
    [RelayCommand]
    private async Task SelectPartyAsync()
    {
        if (!IsPartyCreationEnabled)
        {
            await Shell.Current.DisplayAlert(
                L.T("CreateSeriesType_QuizDisabledTitle"),
                L.T("CreateSeriesType_QuizDisabledMessage"),
                L.T("Common_OK"));
            return;
        }

        var route = HasGroup
            ? $"CreatePartyPage?GroupId={GroupId}&GroupName={Uri.EscapeDataString(GroupName ?? "")}"
            : "CreatePartyPage";

        await Shell.Current.GoToAsync(route);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
