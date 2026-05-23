using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
[QueryProperty(nameof(EditMode), "EditMode")]
public partial class CreateQuizViewModel : ObservableObject
{
    private readonly SeriesService _seriesService;
    private readonly QuizService _quizService;

    public CreateQuizViewModel(SeriesService seriesService, QuizService quizService)
    {
        _seriesService = seriesService;
        _quizService = quizService;

        Questions.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasQuestions));
            OnPropertyChanged(nameof(NoQuestions));
            OnPropertyChanged(nameof(QuestionCount));
            OnPropertyChanged(nameof(CanAddQuestion));
            RefreshQuestionPositions();
        };

        EditQuizQuestionViewModel.QuestionSaved += OnQuestionSaved;
    }

    public void Cleanup()
    {
        EditQuizQuestionViewModel.QuestionSaved -= OnQuestionSaved;
    }

    // ─── Paramètres reçus via Shell ──────────────────────────────
    [ObservableProperty] private string? _groupId;
    [ObservableProperty] private string? _groupName;

    [ObservableProperty] private string? _seriesId;
    [ObservableProperty] private string? _seriesTitle;
    [ObservableProperty] private string? _editMode;

    public bool IsEditMode => string.Equals(EditMode, "true", StringComparison.OrdinalIgnoreCase);
    public bool IsCreateMode => !IsEditMode;

    partial void OnEditModeChanged(string? value)
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(SaveButtonLabel));
        OnPropertyChanged(nameof(HeaderLabel));
    }

    partial void OnSeriesIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && IsEditMode)
            _ = LoadExistingQuizAsync();
    }

    public string SaveButtonLabel => IsEditMode
        ? L.T("CreateQuiz_UpdateButton")
        : L.T("CreateQuiz_CreateButton");

    public string HeaderLabel => IsEditMode
        ? L.T("CreateQuiz_EditHeader")
        : L.T("CreateQuiz_Header");

    // ─── Champs du formulaire ────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StyleKahootSelected))]
    [NotifyPropertyChangedFor(nameof(StyleMillionaireSelected))]
    [NotifyPropertyChangedFor(nameof(StyleBurgerSelected))]
    [NotifyPropertyChangedFor(nameof(StyleWeakestSelected))]
    [NotifyPropertyChangedFor(nameof(StyleKahootBg))]
    [NotifyPropertyChangedFor(nameof(StyleMillionaireBg))]
    [NotifyPropertyChangedFor(nameof(StyleBurgerBg))]
    [NotifyPropertyChangedFor(nameof(StyleWeakestBg))]
    [NotifyPropertyChangedFor(nameof(StyleKahootBorder))]
    [NotifyPropertyChangedFor(nameof(StyleMillionaireBorder))]
    [NotifyPropertyChangedFor(nameof(StyleBurgerBorder))]
    [NotifyPropertyChangedFor(nameof(StyleWeakestBorder))]
    private string _quizStyle = "kahoot";

    public bool StyleKahootSelected => QuizStyle == "kahoot";
    public bool StyleMillionaireSelected => QuizStyle == "millionaire";
    public bool StyleBurgerSelected => QuizStyle == "burger";
    public bool StyleWeakestSelected => QuizStyle == "weakest";

    public Color StyleKahootBg => StyleKahootSelected ? Color.FromArgb("#E5DCC9") : Colors.White;
    public Color StyleMillionaireBg => StyleMillionaireSelected ? Color.FromArgb("#E5DCC9") : Colors.White;
    public Color StyleBurgerBg => StyleBurgerSelected ? Color.FromArgb("#E5DCC9") : Colors.White;
    public Color StyleWeakestBg => StyleWeakestSelected ? Color.FromArgb("#E5DCC9") : Colors.White;

    public Color StyleKahootBorder => StyleKahootSelected ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color StyleMillionaireBorder => StyleMillionaireSelected ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color StyleBurgerBorder => StyleBurgerSelected ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color StyleWeakestBorder => StyleWeakestSelected ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");

    [RelayCommand]
    private void SelectStyle(string? style)
    {
        if (string.IsNullOrEmpty(style)) return;
        if (style is "kahoot" or "millionaire" or "burger" or "weakest")
            QuizStyle = style;
    }

    [ObservableProperty] private bool _showFullLeaderboard = true;

    // ⚡ BUG 5 — Selfie réactivé sur la page de création de quiz
    [ObservableProperty] private bool _selfieEnabled = true;

    [RelayCommand]
    private void ToggleSelfieEnabled()
    {
        SelfieEnabled = !SelfieEnabled;
    }

    // ─── Liste des questions ─────────────────────────────────────
    public ObservableCollection<QuizQuestionItem> Questions { get; } = new();

    public bool HasQuestions => Questions.Count > 0;
    public bool NoQuestions => Questions.Count == 0;
    public int QuestionCount => Questions.Count;

    private const int MAX_QUESTIONS = 50;
    public bool CanAddQuestion => Questions.Count < MAX_QUESTIONS;

    // ─── État UI ─────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;

    public bool HasGroup => !string.IsNullOrEmpty(GroupId);
    partial void OnGroupIdChanged(string? value) => OnPropertyChanged(nameof(HasGroup));

    // ─── Helpers ─────────────────────────────────────────────────

    private void RefreshQuestionPositions()
    {
        for (int i = 0; i < Questions.Count; i++)
        {
            var q = Questions[i];
            q.Position = i + 1;
            q.PositionLabel = string.Format(L.T("CreateQuiz_QuestionPositionFormat"), i + 1);
            q.CanMoveUp = i > 0;
            q.CanMoveDown = i < Questions.Count - 1;
        }
    }

    private void OnQuestionSaved(object? sender, QuizQuestionResult result)
    {
        if (!string.IsNullOrEmpty(result.QuestionId))
        {
            var existing = Questions.FirstOrDefault(q => q.Id == result.QuestionId);
            if (existing != null)
            {
                existing.Title = result.Title;
                existing.QuestionText = result.QuestionText;
                existing.PhotoUrl = result.PhotoUrl;
                existing.Options = result.Options;
                existing.OptionsCount = result.Options.Count;
                existing.HasCorrectAnswer = result.Options.Any(o => o.IsCorrect);
                return;
            }
        }

        var newQuestion = new QuizQuestionItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = result.Title,
            QuestionText = result.QuestionText,
            PhotoUrl = result.PhotoUrl,
            Options = result.Options,
            OptionsCount = result.Options.Count,
            HasCorrectAnswer = result.Options.Any(o => o.IsCorrect)
        };
        Questions.Add(newQuestion);
    }

    private async Task LoadExistingQuizAsync()
    {
        if (string.IsNullOrEmpty(SeriesId)) return;

        IsLoading = true;
        try
        {
            if (!string.IsNullOrEmpty(SeriesTitle))
                Title = SeriesTitle;

            var existing = await _quizService.LoadQuizQuestionsForEditAsync(SeriesId);

            Questions.Clear();
            foreach (var q in existing)
                Questions.Add(q);

            RefreshQuestionPositions();
            System.Diagnostics.Debug.WriteLine(
                $"[CreateQuiz] EditMode: chargé {existing.Count} questions de la série {SeriesId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateQuiz] LoadExistingQuiz: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Commandes ───────────────────────────────────────────────

    [RelayCommand]
    private void ToggleShowFullLeaderboard()
    {
        ShowFullLeaderboard = !ShowFullLeaderboard;
    }

    [RelayCommand]
    private async Task AddQuestionAsync()
    {
        try
        {
            if (!CanAddQuestion)
            {
                await Shell.Current.DisplayAlert(
                    L.T("CreateQuiz_TooManyQuestionsTitle"),
                    string.Format(L.T("CreateQuiz_TooManyQuestionsMessage"), MAX_QUESTIONS),
                    L.T("Common_OK"));
                return;
            }

            System.Diagnostics.Debug.WriteLine("[CreateQuiz] AddQuestion: navigation");
            await Shell.Current.GoToAsync("EditQuizQuestionPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreateQuiz] AddQuestion FAILED: {ex.GetType().Name}: {ex.Message}");
            try
            {
                await Shell.Current.DisplayAlert(
                    L.T("CreateQuiz_ValidationTitle"),
                    L.F("CreateQuiz_NavImpossible", ex.Message),
                    L.T("Common_OK"));
            }
            catch { }
        }
    }

    [RelayCommand]
    private async Task EditQuestionAsync(QuizQuestionItem? question)
    {
        try
        {
            if (question == null) return;
            await Shell.Current.GoToAsync($"EditQuizQuestionPage?QuestionId={question.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreateQuiz] EditQuestion FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteQuestionAsync(QuizQuestionItem? question)
    {
        if (question == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("CreateQuiz_DeleteQuestionTitle"),
            string.Format(L.T("CreateQuiz_DeleteQuestionMessage"), question.Position),
            L.T("CreateQuiz_DeleteConfirm"),
            L.T("Common_Cancel"));

        if (!confirm) return;
        Questions.Remove(question);
    }

    [RelayCommand]
    private void MoveQuestionUp(QuizQuestionItem? question)
    {
        if (question == null) return;
        var index = Questions.IndexOf(question);
        if (index <= 0) return;
        Questions.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveQuestionDown(QuizQuestionItem? question)
    {
        if (question == null) return;
        var index = Questions.IndexOf(question);
        if (index < 0 || index >= Questions.Count - 1) return;
        Questions.Move(index, index + 1);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsCreateMode && string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlert(
                L.T("CreateQuiz_ValidationTitle"),
                L.T("CreateQuiz_TitleRequired"),
                L.T("Common_OK"));
            return;
        }

        if (Questions.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                L.T("CreateQuiz_ValidationTitle"),
                L.T("CreateQuiz_NoQuestions"),
                L.T("Common_OK"));
            return;
        }

        for (int i = 0; i < Questions.Count; i++)
        {
            var q = Questions[i];
            if (string.IsNullOrWhiteSpace(q.Title) ||
                string.IsNullOrWhiteSpace(q.QuestionText) ||
                q.Options == null || q.Options.Count < 2 || q.Options.Count > 4 ||
                !q.Options.Any(o => o.IsCorrect) ||
                q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
            {
                await Shell.Current.DisplayAlert(
                    L.T("CreateQuiz_ValidationTitle"),
                    string.Format(L.T("CreateQuiz_QuestionIncomplete"), i + 1),
                    L.T("Common_OK"));
                return;
            }
        }

        IsLoading = true;
        try
        {
            if (IsEditMode)
            {
                if (string.IsNullOrEmpty(SeriesId))
                {
                    await Shell.Current.DisplayAlert(
                        L.T("CreateQuiz_CreationFailedTitle"),
                        L.T("CreateQuiz_SeriesIdMissing"),
                        L.T("Common_OK"));
                    return;
                }

                var (success, errorCode) = await _quizService.ReplaceQuizQuestionsAsync(
                    SeriesId, Questions.ToList());

                if (!success)
                {
                    var msg = MapErrorCodeToMessage(errorCode);
                    await Shell.Current.DisplayAlert(
                        L.T("CreateQuiz_CreationFailedTitle"),
                        msg, L.T("Common_OK"));
                    return;
                }

                await Shell.Current.DisplayAlert(
                    L.T("CreateQuiz_UpdatedTitle"),
                    L.T("CreateQuiz_UpdatedMessage"),
                    L.T("Common_OK"));

                await Shell.Current.GoToAsync("..");
                return;
            }

            // ─── Mode création ──────────────────────────────────────
            var effectiveGroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId;

            var result = await _quizService.CreateQuizAsync(
                groupId: effectiveGroupId,
                title: Title.Trim(),
                description: Description?.Trim() ?? string.Empty,
                quizStyle: QuizStyle,
                showFullLeaderboard: ShowFullLeaderboard,
                questions: Questions.ToList(),
                selfieEnabled: SelfieEnabled);  // ⚡ BUG 5

            if (!result.Success)
            {
                var errorMessage = MapErrorCodeToMessage(result.ErrorCode);
                await Shell.Current.DisplayAlert(
                    L.T("CreateQuiz_CreationFailedTitle"),
                    errorMessage,
                    L.T("Common_OK"));
                return;
            }

            await Shell.Current.DisplayAlert(
                L.T("CreateQuiz_CreatedTitle"),
                string.Format(L.T("CreateQuiz_CreatedMessage"), result.AccessCode ?? "??????"),
                L.T("CreateQuiz_CreatedOK"));

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
            System.Diagnostics.Debug.WriteLine($"[CreateQuiz] Exception: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L.T("CreateQuiz_CreationFailedTitle"),
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
        if (Questions.Count > 0 && !IsEditMode)
        {
            bool confirm = await Shell.Current.DisplayAlert(
                L.T("CreateQuiz_CancelConfirmTitle"),
                L.T("CreateQuiz_CancelConfirmMessage"),
                L.T("CreateQuiz_CancelConfirm"),
                L.T("CreateQuiz_KeepEditing"));

            if (!confirm) return;
        }

        await Shell.Current.GoToAsync("..");
    }

    public QuizQuestionItem? GetQuestionById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Questions.FirstOrDefault(q => q.Id == id);
    }

    private string MapErrorCodeToMessage(string errorCode)
    {
        var key = errorCode switch
        {
            "unauthenticated" => "CreateQuiz_Error_Unauthenticated",
            "title_required" => "CreateQuiz_Error_TitleRequired",
            "title_too_long" => "CreateQuiz_Error_TitleTooLong",
            "invalid_quiz_style" => "CreateQuiz_Error_InvalidQuizStyle",
            "questions_invalid" => "CreateQuiz_Error_QuestionsInvalid",
            "no_questions" => "CreateQuiz_Error_NoQuestions",
            "too_many_questions" => "CreateQuiz_Error_TooManyQuestions",
            "not_group_member" => "CreateQuiz_Error_NotGroupMember",
            "not_creator" => "CreateQuiz_Error_NotCreator",
            "not_a_quiz" => "CreateQuiz_Error_NotAQuiz",
            "series_not_found" => "CreateQuiz_Error_SeriesNotFound",
            "series_not_editable" => "CreateQuiz_Error_SeriesNotEditable",
            "question_title_required" => "CreateQuiz_Error_QuestionTitleRequired",
            "question_text_required" => "CreateQuiz_Error_QuestionTextRequired",
            "options_invalid" => "CreateQuiz_Error_OptionsInvalid",
            "options_count_invalid" => "CreateQuiz_Error_OptionsCountInvalid",
            "option_text_required" => "CreateQuiz_Error_OptionTextRequired",
            "option_text_too_long" => "CreateQuiz_Error_OptionTextTooLong",
            "no_correct_answer" => "CreateQuiz_Error_NoCorrectAnswer",
            "insufficient_credits" => "CreateQuiz_Error_InsufficientCredits",
            "user_not_found" => "CreateQuiz_Error_UserNotFound",
            "sql_error" => "CreateQuiz_Error_SqlError",
            _ => "CreateQuiz_Error_Unknown"
        };

        var msg = L.T(key);
        if (key == "CreateQuiz_Error_Unknown" && !string.IsNullOrEmpty(errorCode))
            msg = $"{msg} ({errorCode})";
        return msg;
    }
}

public partial class QuizQuestionItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    [ObservableProperty] private int _position = 1;
    [ObservableProperty] private string _positionLabel = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string? _photoUrl;
    [ObservableProperty] private int _optionsCount = 0;
    [ObservableProperty] private bool _hasCorrectAnswer = false;
    [ObservableProperty] private bool _canMoveUp = false;
    [ObservableProperty] private bool _canMoveDown = false;
    [ObservableProperty] private bool _isSelected = false;
    public List<QuizQuestionOptionItem> Options { get; set; } = new();
    public bool HasPhoto => !string.IsNullOrEmpty(PhotoUrl);
    partial void OnPhotoUrlChanged(string? value) => OnPropertyChanged(nameof(HasPhoto));
}

public partial class QuizQuestionOptionItem : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _isCorrect = false;
}