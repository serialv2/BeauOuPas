using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BeauOuPas.ViewModels.Groups;

/// <summary>
/// ViewModel de création d'une PARTIE RAPIDE (party game "Most Likely To").
///
/// Bien plus simple que CreateQuizViewModel : pas de saisie de questions
/// (elles sont tirées au hasard côté serveur dans party_questions). Ne
/// modifie aucun ViewModel/Service existant.
///
/// Flux :
///   - L'utilisateur saisit un titre + (optionnel) une description.
///   - Choisit un nombre de questions (stepper).
///   - Choisit une catégorie dans un picker DYNAMIQUE (chargé via
///     PartyService.GetCategoriesAsync) avec "🎲 Mélange" en tête
///     (= toutes catégories).
///   - La langue des questions = langue du téléphone (détectée via
///     la culture courante), avec fallback 'fr' géré côté SQL.
///   - Bouton "Créer" → PartyService.CreatePartyAsync → RPC
///     create_party_series (création + tirage aléatoire, atomique).
/// </summary>
[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
public partial class CreatePartyViewModel : ObservableObject
{
    private readonly PartyService _partyService;

    // Sentinel "Mélange" : valeur affichée dans le picker pour
    // "toutes catégories". CreatePartyAsync(null) → la RPC pioche partout.
    private const string MIX_LABEL = "🎲 Mélange (toutes catégories)";

    public CreatePartyViewModel(PartyService partyService)
    {
        _partyService = partyService;
        Categories.Add(MIX_LABEL);
        SelectedCategory = MIX_LABEL;
    }

    // ─── Paramètres reçus via Shell ──────────────────────────────
    [ObservableProperty] private string? _groupId;
    [ObservableProperty] private string? _groupName;

    public bool HasGroup => !string.IsNullOrEmpty(GroupId);
    partial void OnGroupIdChanged(string? value) => OnPropertyChanged(nameof(HasGroup));

    // ─── Champs du formulaire ────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    // Nombre de questions (stepper). Bornes alignées sur la RPC (1..50).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDecrease))]
    [NotifyPropertyChangedFor(nameof(CanIncrease))]
    [NotifyPropertyChangedFor(nameof(QuestionCountLabel))]
    private int _questionCount = 10;

    private const int MIN_QUESTIONS = 1;
    private const int MAX_QUESTIONS = 50;

    public bool CanDecrease => QuestionCount > MIN_QUESTIONS;
    public bool CanIncrease => QuestionCount < MAX_QUESTIONS;

    public string QuestionCountLabel =>
        string.Format(L.T("CreateParty_QuestionCountFormat"), QuestionCount);

    [RelayCommand]
    private void DecreaseCount()
    {
        if (QuestionCount > MIN_QUESTIONS) QuestionCount--;
    }

    [RelayCommand]
    private void IncreaseCount()
    {
        if (QuestionCount < MAX_QUESTIONS) QuestionCount++;
    }

    [ObservableProperty] private bool _showFullLeaderboard = true;

    [RelayCommand]
    private void ToggleShowFullLeaderboard()
    {
        ShowFullLeaderboard = !ShowFullLeaderboard;
    }

    // ─── Catégories (picker dynamique, option B) ─────────────────
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty] private string _selectedCategory = MIX_LABEL;

    // ─── État UI ─────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isLoadingCategories = false;

    // Langue du téléphone (ISO 639-1, 2 lettres, minuscules).
    // ⚠️ Voir README : si tu as une API de langue dédiée dans ta couche
    //    Localization (ex. L.CurrentLanguage), remplace ce helper par elle
    //    pour rester cohérent avec le reste de l'app.
    public static string DetectPhoneLang()
    {
        try
        {
            var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (!string.IsNullOrWhiteSpace(two))
                return two.ToLowerInvariant();
        }
        catch { }
        return "fr";
    }

    // ─── Chargement initial des catégories ───────────────────────
    // À appeler depuis l'OnAppearing de la page (voir README).
    public async Task LoadCategoriesAsync()
    {
        if (IsLoadingCategories) return;
        IsLoadingCategories = true;
        try
        {
            var lang = DetectPhoneLang();
            var cats = await _partyService.GetCategoriesAsync(lang);

            var previouslySelected = SelectedCategory;

            Categories.Clear();
            Categories.Add(MIX_LABEL); // "Mélange" toujours en tête
            foreach (var c in cats)
            {
                if (!string.IsNullOrWhiteSpace(c.Category))
                    Categories.Add(c.Category);
            }

            // Restaure la sélection si elle existe encore, sinon "Mélange"
            SelectedCategory = Categories.Contains(previouslySelected)
                ? previouslySelected
                : MIX_LABEL;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreateParty] LoadCategories: {ex.Message}");
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    // ─── Création de la partie ───────────────────────────────────
    [RelayCommand]
    private async Task CreateAsync()
    {
        // Validation locale
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlert(
                L.T("CreateParty_ValidationTitle"),
                L.T("CreateParty_TitleRequired"),
                L.T("Common_OK"));
            return;
        }

        IsLoading = true;
        try
        {
            var effectiveGroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId;
            var lang = DetectPhoneLang();

            // "Mélange" → on passe null au service (→ '__all__' côté RPC)
            string? category =
                (SelectedCategory == MIX_LABEL || string.IsNullOrWhiteSpace(SelectedCategory))
                    ? null
                    : SelectedCategory;

            var result = await _partyService.CreatePartyAsync(
                title: Title.Trim(),
                description: Description?.Trim() ?? string.Empty,
                count: QuestionCount,
                lang: lang,
                category: category,
                groupId: effectiveGroupId,
                showFullLeaderboard: ShowFullLeaderboard);

            if (!result.Success)
            {
                var errorMessage = MapErrorCodeToMessage(result.ErrorCode);
                await Shell.Current.DisplayAlert(
                    L.T("CreateParty_CreationFailedTitle"),
                    errorMessage,
                    L.T("Common_OK"));
                return;
            }

            await Shell.Current.DisplayAlert(
                L.T("CreateParty_CreatedTitle"),
                string.Format(
                    L.T("CreateParty_CreatedMessage"),
                    result.AccessCode ?? "??????",
                    result.QuestionsCount),
                L.T("CreateParty_CreatedOK"));

            if (!string.IsNullOrEmpty(result.SeriesId))
            {
                await Shell.Current.GoToAsync("..");
                await Shell.Current.GoToAsync(
                    $"SeriesDetailPage?SeriesId={result.SeriesId}");
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateParty] Exception: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L.T("CreateParty_CreationFailedTitle"),
                ex.Message,
                L.T("Common_OK"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private string MapErrorCodeToMessage(string errorCode)
    {
        var key = errorCode switch
        {
            "unauthenticated"        => "CreateParty_Error_Unauthenticated",
            "title_required"         => "CreateParty_Error_TitleRequired",
            "title_too_long"         => "CreateParty_Error_TitleTooLong",
            "invalid_count"          => "CreateParty_Error_InvalidCount",
            "user_not_found"         => "CreateParty_Error_UserNotFound",
            "insufficient_credits"   => "CreateParty_Error_InsufficientCredits",
            "no_questions_available" => "CreateParty_Error_NoQuestionsAvailable",
            "sql_error"              => "CreateParty_Error_SqlError",
            _                        => "CreateParty_Error_Unknown"
        };

        var msg = L.T(key);
        if (key == "CreateParty_Error_Unknown" && !string.IsNullOrEmpty(errorCode))
            msg = $"{msg} ({errorCode})";
        return msg;
    }
}
