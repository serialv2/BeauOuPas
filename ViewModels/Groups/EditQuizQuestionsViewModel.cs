using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

/// <summary>
/// 🔧 EDIT QUIZ QUESTIONS PAGE
/// 
/// Page DÉDIÉE à la modification des questions d'un quiz existant.
/// Volontairement séparée de CreateQuizPage pour éviter le bricolage
/// "EditMode" qui rendait l'UI bancale.
/// 
/// Reçoit via Shell :
///  - SeriesId   : id du quiz à modifier
///  - SeriesTitle : titre affiché en sous-header (informatif uniquement)
/// 
/// Workflow :
///  - OnAppearing → LoadQuestionsAsync() charge les questions+options en BDD
///  - L'user peut ajouter/modifier/supprimer/réorganiser
///  - "💾 Mettre à jour" → RPC replace_quiz_questions
///  - Retour SeriesDetailPage
/// 
/// Réutilise EditQuizQuestionViewModel.QuestionSaved (event statique) pour
/// recevoir les modifs depuis EditQuizQuestionPage.
/// </summary>
[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class EditQuizQuestionsViewModel : ObservableObject
{
    private readonly QuizService _quizService;
    private bool _hasLoaded = false;

    public EditQuizQuestionsViewModel(QuizService quizService)
    {
        _quizService = quizService;

        Questions.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasQuestions));
            OnPropertyChanged(nameof(NoQuestions));
            OnPropertyChanged(nameof(QuestionCount));
            OnPropertyChanged(nameof(CanAddQuestion));
            OnPropertyChanged(nameof(ShowSelectButton));
            RefreshQuestionPositions();
        };

        EditQuizQuestionViewModel.QuestionSaved += OnQuestionSaved;
    }

    public void Cleanup()
    {
        EditQuizQuestionViewModel.QuestionSaved -= OnQuestionSaved;
    }

    // ─── Paramètres reçus via Shell ──────────────────────────────
    [ObservableProperty] private string? _seriesId;
    [ObservableProperty] private string? _seriesTitle;

    // ─── Liste des questions ─────────────────────────────────────
    public ObservableCollection<QuizQuestionItem> Questions { get; } = new();

    public bool HasQuestions => Questions.Count > 0;
    public bool NoQuestions => Questions.Count == 0 && !IsLoading;
    public int QuestionCount => Questions.Count;

    // ⚡ QW2 : bouton "✓ Sélectionner" visible si on a des questions ET
    // qu'on n'est pas déjà en mode sélection.
    public bool ShowSelectButton => HasQuestions && !IsSelectionMode;

    private const int MAX_QUESTIONS = 50;
    public bool CanAddQuestion => Questions.Count < MAX_QUESTIONS;

    // ─── État UI ─────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isSaving = false;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(NoQuestions));

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

    /// <summary>
    /// Appelé depuis le code-behind dans OnAppearing pour charger les questions
    /// existantes au moment où la page apparaît (et pas avant).
    /// Idempotent : ne charge qu'une seule fois.
    /// </summary>
    public async Task LoadQuestionsIfNeededAsync()
    {
        if (_hasLoaded) return;
        if (string.IsNullOrEmpty(SeriesId)) return;

        _hasLoaded = true;
        IsLoading = true;
        try
        {
            var existing = await _quizService.LoadQuizQuestionsForEditAsync(SeriesId);

            Questions.Clear();
            foreach (var q in existing)
                Questions.Add(q);

            RefreshQuestionPositions();
            System.Diagnostics.Debug.WriteLine(
                $"[EditQuizQuestions] Chargé {existing.Count} questions de la série {SeriesId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditQuizQuestions] Load: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("EditQuizQuestions_LoadFailed"),
                L.T("Common_OK"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Commandes ───────────────────────────────────────────────

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
            await Shell.Current.GoToAsync("EditQuizQuestionPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditQuizQuestions] AddQuestion: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EditQuestionAsync(QuizQuestionItem? question)
    {
        try
        {
            if (question == null) return;
            // ⚡ QW2 : en mode multi-sélection, le tap toggle au lieu d'éditer
            if (IsSelectionMode)
            {
                ToggleQuestionSelection(question);
                return;
            }
            await Shell.Current.GoToAsync($"EditQuizQuestionPage?QuestionId={question.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditQuizQuestions] EditQuestion: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteQuestionAsync(QuizQuestionItem? question)
    {
        if (question == null) return;

        // ⚡ QW2 : si on est en mode multi-sélection, le tap sur 🗑️ d'un item
        // est bloqué : il faut utiliser le bouton 🗑️ (N) de la barre d'actions.
        if (IsSelectionMode) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("CreateQuiz_DeleteQuestionTitle"),
            string.Format(L.T("CreateQuiz_DeleteQuestionMessage"), question.Position),
            L.T("CreateQuiz_DeleteConfirm"),
            L.T("Common_Cancel"));

        if (!confirm) return;
        Questions.Remove(question);
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ QW2 : MULTI-SÉLECTION GMAIL-STYLE (questions de quiz)
    //
    // Suppression batch : ici c'est local (Remove de la collection),
    // l'enregistrement réel en BDD se fait via le bouton "💾 Mettre à jour"
    // (RPC replace_quiz_questions). Cohérent avec la suppression simple
    // qui était déjà locale.
    // ═════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionHeaderText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedQuestions))]
    [NotifyPropertyChangedFor(nameof(ShowSelectButton))]
    private bool _isSelectionMode = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionHeaderText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedQuestions))]
    private int _selectedQuestionsCount = 0;

    public bool HasSelectedQuestions => SelectedQuestionsCount > 0;

    public string SelectionHeaderText => $"🗑️ ({SelectedQuestionsCount})";

    /// <summary>
    /// ⚡ QW2 (rev) : entre en mode sélection sans cocher d'item.
    /// Appelé par le bouton "✓ Sélectionner" en haut de la liste.
    /// </summary>
    [RelayCommand]
    private void StartQuestionSelection()
    {
        if (!IsSelectionMode) IsSelectionMode = true;
    }

    /// <summary>
    /// Tap court en mode sélection : toggle l'item.
    /// </summary>
    [RelayCommand]
    private void ToggleQuestionSelection(QuizQuestionItem? question)
    {
        if (question == null || !IsSelectionMode) return;
        question.IsSelected = !question.IsSelected;
        SelectedQuestionsCount = Questions.Count(q => q.IsSelected);
        if (SelectedQuestionsCount == 0)
        {
            IsSelectionMode = false;
        }
    }

    /// <summary>
    /// Sort du mode sélection : déselectionne tout.
    /// Aussi exposé via ToggleSelectionMode (utilisé par le Retour Android).
    /// </summary>
    [RelayCommand]
    private void ExitQuestionSelection()
    {
        foreach (var q in Questions)
        {
            if (q.IsSelected) q.IsSelected = false;
        }
        SelectedQuestionsCount = 0;
        IsSelectionMode = false;
    }

    /// <summary>
    /// Alias utilisé par le code-behind sur Retour Android : sort du mode.
    /// </summary>
    [RelayCommand]
    private void ToggleSelectionMode() => ExitQuestionSelection();

    /// <summary>
    /// Suppression batch : 1 confirmation, puis remove de toutes les
    /// questions sélectionnées de la collection (les positions seront
    /// rafraîchies par CollectionChanged → RefreshQuestionPositions).
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedQuestionsAsync()
    {
        var toDelete = Questions.Where(q => q.IsSelected).ToList();
        if (toDelete.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("CreateQuiz_DeleteQuestionTitle"),
            toDelete.Count == 1
                ? string.Format(L.T("CreateQuiz_DeleteQuestionMessage"), toDelete[0].Position)
                : $"Supprimer {toDelete.Count} questions ?",
            L.T("CreateQuiz_DeleteConfirm"),
            L.T("Common_Cancel"));
        if (!confirm) return;

        foreach (var q in toDelete)
        {
            Questions.Remove(q);
        }

        SelectedQuestionsCount = 0;
        IsSelectionMode = false;
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

    /// <summary>
    /// Appelle la RPC replace_quiz_questions et revient à SeriesDetailPage.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(SeriesId))
        {
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                "SeriesId manquant",
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

        // Validation locale
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

        IsSaving = true;
        try
        {
            var (success, errorCode) = await _quizService.ReplaceQuizQuestionsAsync(
                SeriesId, Questions.ToList());

            if (!success)
            {
                var msg = MapErrorCodeToMessage(errorCode);
                await Shell.Current.DisplayAlert(
                    L.T("EditQuizQuestions_SaveFailedTitle"),
                    msg, L.T("Common_OK"));
                return;
            }

            await Shell.Current.DisplayAlert(
                L.T("EditQuizQuestions_SavedTitle"),
                L.T("EditQuizQuestions_SavedMessage"),
                L.T("Common_OK"));

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditQuizQuestions] Save: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L.T("EditQuizQuestions_SaveFailedTitle"),
                ex.Message, L.T("Common_OK"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
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
            "unauthenticated"          => "CreateQuiz_Error_Unauthenticated",
            "questions_invalid"        => "CreateQuiz_Error_QuestionsInvalid",
            "no_questions"             => "CreateQuiz_Error_NoQuestions",
            "too_many_questions"       => "CreateQuiz_Error_TooManyQuestions",
            "not_creator"              => "CreateQuiz_Error_NotCreator",
            "not_a_quiz"               => "CreateQuiz_Error_NotAQuiz",
            "series_not_found"         => "CreateQuiz_Error_SeriesNotFound",
            "series_not_editable"      => "CreateQuiz_Error_SeriesNotEditable",
            "question_title_required"  => "CreateQuiz_Error_QuestionTitleRequired",
            "question_text_required"   => "CreateQuiz_Error_QuestionTextRequired",
            "options_invalid"          => "CreateQuiz_Error_OptionsInvalid",
            "options_count_invalid"    => "CreateQuiz_Error_OptionsCountInvalid",
            "option_text_required"     => "CreateQuiz_Error_OptionTextRequired",
            "option_text_too_long"     => "CreateQuiz_Error_OptionTextTooLong",
            "no_correct_answer"        => "CreateQuiz_Error_NoCorrectAnswer",
            "sql_error"                => "CreateQuiz_Error_SqlError",
            _                          => "CreateQuiz_Error_Unknown"
        };

        var msg = L.T(key);
        if (key == "CreateQuiz_Error_Unknown" && !string.IsNullOrEmpty(errorCode))
            msg = $"{msg} ({errorCode})";
        return msg;
    }
}
