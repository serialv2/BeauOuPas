using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
public partial class CreatePartyViewModel : ObservableObject
{
    private readonly PartyService _partyService;
    private const string MIX_LABEL = "🎲 Mélange (toutes catégories)";

    public CreatePartyViewModel(PartyService partyService)
    {
        _partyService = partyService;
        _questionCount = 10;
        _questionCountText = "10";
        Categories.Add(MIX_LABEL);
        SelectedCategory = MIX_LABEL;
    }

    [ObservableProperty] private string? _groupId;
    [ObservableProperty] private string? _groupName;

    public bool HasGroup => !string.IsNullOrEmpty(GroupId);
    partial void OnGroupIdChanged(string? value) => OnPropertyChanged(nameof(HasGroup));

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDecrease))]
    [NotifyPropertyChangedFor(nameof(CanIncrease))]
    [NotifyPropertyChangedFor(nameof(QuestionCountLabel))]
    private int _questionCount;

    // ⚡ BUG 4A — Saisie directe du nombre de questions
    [ObservableProperty] private string _questionCountText;

    partial void OnQuestionCountChanged(int value)
    {
        QuestionCountText = value.ToString();
    }

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

    // ⚡ BUG 4A — Valider la saisie directe
    [RelayCommand]
    private void ValidateCount()
    {
        if (int.TryParse(QuestionCountText, out int val))
        {
            val = Math.Clamp(val, MIN_QUESTIONS, MAX_QUESTIONS);
            QuestionCount = val;
            QuestionCountText = val.ToString();
        }
        else
        {
            QuestionCountText = QuestionCount.ToString();
        }
    }

    [ObservableProperty] private bool _showFullLeaderboard = true;

    [RelayCommand]
    private void ToggleShowFullLeaderboard()
    {
        ShowFullLeaderboard = !ShowFullLeaderboard;
    }

    // ⚡ BUG 5 — Selfies
    [ObservableProperty] private bool _selfieEnabled = true;

    [RelayCommand]
    private void ToggleSelfieEnabled()
    {
        SelfieEnabled = !SelfieEnabled;
    }

    public ObservableCollection<string> Categories { get; } = new();
    [ObservableProperty] private string _selectedCategory = MIX_LABEL;

    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isLoadingCategories = false;

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
            Categories.Add(MIX_LABEL);
            foreach (var c in cats)
            {
                if (!string.IsNullOrWhiteSpace(c.Category))
                    Categories.Add(c.Category);
            }
            SelectedCategory = Categories.Contains(previouslySelected)
                ? previouslySelected
                : MIX_LABEL;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateParty] LoadCategories: {ex.Message}");
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        // Valider la saisie avant de créer
        ValidateCount();

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
                showFullLeaderboard: ShowFullLeaderboard,
                selfieEnabled: SelfieEnabled);

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
                await Shell.Current.GoToAsync($"SeriesDetailPage?SeriesId={result.SeriesId}");
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
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    private string MapErrorCodeToMessage(string errorCode)
    {
        var key = errorCode switch
        {
            "unauthenticated" => "CreateParty_Error_Unauthenticated",
            "title_required" => "CreateParty_Error_TitleRequired",
            "title_too_long" => "CreateParty_Error_TitleTooLong",
            "invalid_count" => "CreateParty_Error_InvalidCount",
            "user_not_found" => "CreateParty_Error_UserNotFound",
            "insufficient_credits" => "CreateParty_Error_InsufficientCredits",
            "no_questions_available" => "CreateParty_Error_NoQuestionsAvailable",
            "sql_error" => "CreateParty_Error_SqlError",
            _ => "CreateParty_Error_Unknown"
        };
        var msg = L.T(key);
        if (key == "CreateParty_Error_Unknown" && !string.IsNullOrEmpty(errorCode))
            msg = $"{msg} ({errorCode})";
        return msg;
    }
}