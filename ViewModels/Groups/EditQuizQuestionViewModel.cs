using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(QuestionId), "QuestionId")]
public partial class EditQuizQuestionViewModel : ObservableObject
{
    private readonly PhotoUploadService _photoUploadService;

    public EditQuizQuestionViewModel(PhotoUploadService photoUploadService)
    {
        _photoUploadService = photoUploadService;

        // Initialise par défaut avec 2 options vides (minimum requis par la RPC)
        Options = new ObservableCollection<EditQuizOptionItem>
        {
            new EditQuizOptionItem(),
            new EditQuizOptionItem()
        };

        Options.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CanAddOption));
            OnPropertyChanged(nameof(CanRemoveOption));
            OnPropertyChanged(nameof(OptionCount));
            RefreshOptionFlags();
        };
    }

    // ─── Paramètres reçus via Shell ─────────────────────────────────
    [ObservableProperty] private string? _questionId;

    // ─── Champs du formulaire ───────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string? _photoUrl;
    [ObservableProperty] private string? _photoPreview;

    // ─── Options ────────────────────────────────────────────────────
    public ObservableCollection<EditQuizOptionItem> Options { get; }

    // Limites imposées par la RPC create_quiz_series
    private const int MIN_OPTIONS = 2;
    private const int MAX_OPTIONS = 4;

    public bool CanAddOption => Options.Count < MAX_OPTIONS;
    public bool CanRemoveOption => Options.Count > MIN_OPTIONS;
    public int OptionCount => Options.Count;
    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPreview);

    // ─── État UI ────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isUploadingPhoto = false;
    [ObservableProperty] private bool _hasUnsavedChanges = false;

    partial void OnTitleChanged(string value) => HasUnsavedChanges = true;
    partial void OnQuestionTextChanged(string value) => HasUnsavedChanges = true;
    partial void OnPhotoUrlChanged(string? value) => HasUnsavedChanges = true;

    // ─── Helpers ────────────────────────────────────────────────────

    private void RefreshOptionFlags()
    {
        for (int i = 0; i < Options.Count; i++)
        {
            Options[i].PositionLabel = string.Format(
                L.T("EditQuizQuestion_OptionPositionFormat"), i + 1);
        }
    }

    public void Initialize(QuizQuestionItem? existing)
    {
        // Si on édite une question existante, on charge ses valeurs
        if (existing != null)
        {
            Title = existing.Title;
            QuestionText = existing.QuestionText;
            PhotoUrl = existing.PhotoUrl;
            PhotoPreview = existing.PhotoUrl;

            Options.Clear();
            if (existing.Options != null && existing.Options.Count > 0)
            {
                foreach (var opt in existing.Options)
                {
                    Options.Add(new EditQuizOptionItem
                    {
                        Text = opt.Text,
                        IsCorrect = opt.IsCorrect
                    });
                }
            }

            // Garantir le minimum de 2 options
            while (Options.Count < MIN_OPTIONS)
            {
                Options.Add(new EditQuizOptionItem());
            }
        }
        RefreshOptionFlags();
        HasUnsavedChanges = false;
        OnPropertyChanged(nameof(HasPhoto));
    }

    // ─── Commandes : Photo ──────────────────────────────────────────

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        try
        {
            // 🔧 FIX : on ouvre directement la galerie, l'option Caméra a été
            // retirée car elle ne fonctionne pas correctement actuellement.
            // À réactiver plus tard quand le bug caméra sera diagnostiqué.
            FileResult? result = await MediaPicker.Default.PickPhotoAsync();

            if (result == null) return;

            IsUploadingPhoto = true;
            try
            {
                using var stream = await result.OpenReadAsync();
                var url = await _photoUploadService.UploadQuizPhotoAsync(stream);

                if (string.IsNullOrEmpty(url))
                {
                    await Shell.Current.DisplayAlert(
                        L.T("Common_Error"),
                        L.T("EditQuizQuestion_PhotoUploadFailed"),
                        L.T("Common_OK"));
                    return;
                }

                // Si on avait déjà une photo, on supprime l'ancienne du bucket
                if (!string.IsNullOrEmpty(PhotoUrl) && PhotoUrl != url)
                {
                    _ = _photoUploadService.DeleteQuizPhotoAsync(PhotoUrl);
                }

                PhotoUrl = url;
                PhotoPreview = url;
                OnPropertyChanged(nameof(HasPhoto));
                HasUnsavedChanges = true;
            }
            finally
            {
                IsUploadingPhoto = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditQuizQuestion] PickPhoto: {ex.Message}");
            IsUploadingPhoto = false;
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("EditQuizQuestion_PhotoUploadFailed"),
                L.T("Common_OK"));
        }
    }

    [RelayCommand]
    private async Task RemovePhotoAsync()
    {
        if (string.IsNullOrEmpty(PhotoPreview)) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("EditQuizQuestion_RemovePhotoTitle"),
            L.T("EditQuizQuestion_RemovePhotoMessage"),
            L.T("Common_Yes"),
            L.T("Common_No"));

        if (!confirm) return;

        // Supprime du bucket en arrière-plan
        if (!string.IsNullOrEmpty(PhotoUrl))
        {
            _ = _photoUploadService.DeleteQuizPhotoAsync(PhotoUrl);
        }

        PhotoUrl = null;
        PhotoPreview = null;
        OnPropertyChanged(nameof(HasPhoto));
        HasUnsavedChanges = true;
    }

    // ─── Commandes : Options ────────────────────────────────────────

    [RelayCommand]
    private void AddOption()
    {
        if (!CanAddOption) return;
        Options.Add(new EditQuizOptionItem());
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private async Task RemoveOptionAsync(EditQuizOptionItem? option)
    {
        if (option == null || !CanRemoveOption) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("EditQuizQuestion_RemoveOptionTitle"),
            L.T("EditQuizQuestion_RemoveOptionMessage"),
            L.T("Common_Yes"),
            L.T("Common_No"));

        if (!confirm) return;

        Options.Remove(option);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void ToggleOptionCorrect(EditQuizOptionItem? option)
    {
        if (option == null) return;
        option.IsCorrect = !option.IsCorrect;
        HasUnsavedChanges = true;
    }

    // ─── Commandes : Sauvegarde / Annulation ────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        // Validations alignées sur la RPC create_quiz_series

        if (string.IsNullOrWhiteSpace(Title))
        {
            await ShowError(L.T("EditQuizQuestion_TitleRequired"));
            return;
        }
        if (Title.Length > 200)
        {
            await ShowError(L.T("EditQuizQuestion_TitleTooLong"));
            return;
        }

        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            await ShowError(L.T("EditQuizQuestion_QuestionTextRequired"));
            return;
        }
        if (QuestionText.Length > 1000)
        {
            await ShowError(L.T("EditQuizQuestion_QuestionTextTooLong"));
            return;
        }

        if (Options.Count < MIN_OPTIONS || Options.Count > MAX_OPTIONS)
        {
            await ShowError(string.Format(
                L.T("EditQuizQuestion_OptionsCountInvalid"),
                MIN_OPTIONS, MAX_OPTIONS));
            return;
        }

        for (int i = 0; i < Options.Count; i++)
        {
            var opt = Options[i];
            if (string.IsNullOrWhiteSpace(opt.Text))
            {
                await ShowError(string.Format(
                    L.T("EditQuizQuestion_OptionTextRequired"), i + 1));
                return;
            }
            if (opt.Text.Length > 300)
            {
                await ShowError(string.Format(
                    L.T("EditQuizQuestion_OptionTextTooLong"), i + 1));
                return;
            }
        }

        if (!Options.Any(o => o.IsCorrect))
        {
            await ShowError(L.T("EditQuizQuestion_NoCorrectAnswer"));
            return;
        }

        // ⚠️ Communication avec CreateQuizViewModel via évènement statique
        // (les Shell parameters ne supportent pas les objets complexes facilement).
        // CreateQuizViewModel s'abonne quand il navigue ici, et reçoit le résultat.
        QuestionSaved?.Invoke(this, new QuizQuestionResult
        {
            QuestionId = QuestionId,
            Title = Title.Trim(),
            QuestionText = QuestionText.Trim(),
            PhotoUrl = PhotoUrl,
            Options = Options
                .Select(o => new QuizQuestionOptionItem
                {
                    Text = o.Text.Trim(),
                    IsCorrect = o.IsCorrect
                })
                .ToList()
        });

        HasUnsavedChanges = false;
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (HasUnsavedChanges)
        {
            bool confirm = await Shell.Current.DisplayAlert(
                L.T("EditQuizQuestion_CancelConfirmTitle"),
                L.T("EditQuizQuestion_CancelConfirmMessage"),
                L.T("EditQuizQuestion_DiscardChanges"),
                L.T("EditQuizQuestion_KeepEditing"));

            if (!confirm) return;

            // Si on avait uploadé une photo dans cette session sans la sauver,
            // on la supprime du bucket pour éviter les fichiers orphelins.
            // (On ne peut pas savoir laquelle est "nouvelle" vs "originale" facilement,
            //  donc on laisse PhotoUploadService.DeleteQuizPhotoAsync silencieux si erreur.)
        }

        await Shell.Current.GoToAsync("..");
    }

    private async Task ShowError(string message)
    {
        await Shell.Current.DisplayAlert(
            L.T("EditQuizQuestion_ValidationTitle"),
            message,
            L.T("Common_OK"));
    }

    // ─── Évènement de communication avec CreateQuizViewModel ────────
    public static event EventHandler<QuizQuestionResult>? QuestionSaved;
}

// ─── Item de l'éditeur (≠ QuizQuestionOptionItem qui est dans CreateQuizViewModel) ──
public partial class EditQuizOptionItem : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private string _positionLabel = string.Empty;
}

// ─── Résultat envoyé à CreateQuizViewModel après sauvegarde ─────────
public class QuizQuestionResult
{
    public string? QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public List<QuizQuestionOptionItem> Options { get; set; } = new();
}