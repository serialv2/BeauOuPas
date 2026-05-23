using BeauOuPas.Localization;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using static System.Net.Mime.MediaTypeNames;

namespace BeauOuPas.ViewModels.Projects;

public partial class CreateProjectViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly ImageService _imageService;
    private readonly AuthService _authService;
    private readonly CropService _cropService;
    private readonly CreditService _creditService;

    public CreateProjectViewModel(
        ProjectService projectService,
        ImageService imageService,
        AuthService authService,
        CropService cropService,
        CreditService creditService)
    {
        _projectService = projectService;
        _imageService = imageService;
        _authService = authService;
        _cropService = cropService;
        _creditService = creditService;

        // Initialiser 2 options vides pour le sondage
        PollOptions.Add(string.Empty);
        PollOptions.Add(string.Empty);
    }

    // ─── Étape courante ──────────────────────────────────────────────
    [ObservableProperty] private int _currentStep = 1;

    // ─── Étape 1 : Type ──────────────────────────────────────────────
    [ObservableProperty] private bool _isPhotoVote = false;
    [ObservableProperty] private bool _isDuel = false;
    [ObservableProperty] private bool _isPoll = false;

    // ─── Étape 2 : Ciblage ───────────────────────────────────────────
    [ObservableProperty] private string _selectedGender = "both";
    [ObservableProperty] private bool _genderBoth = true;
    [ObservableProperty] private bool _genderFemale = false;
    [ObservableProperty] private bool _genderMale = false;
    [ObservableProperty] private int _minAge = 18;
    [ObservableProperty] private int _maxAge = 99;
    [ObservableProperty] private bool _hasDuration = false;
    [ObservableProperty] private int _durationDays = 7;
    [ObservableProperty] private bool _isPrivate = false;

    // ─── Étape 3 : Contenu ───────────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    // Photos
    [ObservableProperty] private ImageSource? _photoPreview;
    private Stream? _photoStream;
    private string _photoFileName = string.Empty;

    [ObservableProperty] private ImageSource? _photoLeftPreview;
    [ObservableProperty] private ImageSource? _photoRightPreview;
    private Stream? _photoLeftStream;
    private Stream? _photoRightStream;
    private string _photoLeftFileName = string.Empty;
    private string _photoRightFileName = string.Empty;

    // ─── Sondage ─────────────────────────────────────────────────────
    public ObservableCollection<string> PollOptions { get; } = new();

    [ObservableProperty] private string _pollOption1 = string.Empty;
    [ObservableProperty] private string _pollOption2 = string.Empty;
    [ObservableProperty] private string _pollOption3 = string.Empty;
    [ObservableProperty] private string _pollOption4 = string.Empty;
    [ObservableProperty] private string _pollOption5 = string.Empty;
    [ObservableProperty] private string _pollOption6 = string.Empty;

    [ObservableProperty] private bool _showOption3 = false;
    [ObservableProperty] private bool _showOption4 = false;
    [ObservableProperty] private bool _showOption5 = false;
    [ObservableProperty] private bool _showOption6 = false;

    public bool CanAddOption => !ShowOption6;
    public bool CanRemoveOption => ShowOption3;

    [RelayCommand]
    private void AddPollOption()
    {
        if (!ShowOption3) { ShowOption3 = true; }
        else if (!ShowOption4) { ShowOption4 = true; }
        else if (!ShowOption5) { ShowOption5 = true; }
        else if (!ShowOption6) { ShowOption6 = true; }
        OnPropertyChanged(nameof(CanAddOption));
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    [RelayCommand]
    private void RemovePollOption()
    {
        if (ShowOption6) { ShowOption6 = false; PollOption6 = string.Empty; }
        else if (ShowOption5) { ShowOption5 = false; PollOption5 = string.Empty; }
        else if (ShowOption4) { ShowOption4 = false; PollOption4 = string.Empty; }
        else if (ShowOption3) { ShowOption3 = false; PollOption3 = string.Empty; }
        OnPropertyChanged(nameof(CanAddOption));
        OnPropertyChanged(nameof(CanRemoveOption));
    }

    private List<string> GetPollOptions()
    {
        var opts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PollOption1)) opts.Add(PollOption1.Trim());
        if (!string.IsNullOrWhiteSpace(PollOption2)) opts.Add(PollOption2.Trim());
        if (ShowOption3 && !string.IsNullOrWhiteSpace(PollOption3)) opts.Add(PollOption3.Trim());
        if (ShowOption4 && !string.IsNullOrWhiteSpace(PollOption4)) opts.Add(PollOption4.Trim());
        if (ShowOption5 && !string.IsNullOrWhiteSpace(PollOption5)) opts.Add(PollOption5.Trim());
        if (ShowOption6 && !string.IsNullOrWhiteSpace(PollOption6)) opts.Add(PollOption6.Trim());
        return opts;
    }

    // ─── État ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private string _uploadProgress = string.Empty;

    // ─── Visibilité étapes ───────────────────────────────────────────
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    // ─── Couleurs boutons genre ──────────────────────────────────────
    public Color BothColor => SelectedGender == "both"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color FemaleColor => SelectedGender == "female"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color MaleColor => SelectedGender == "male"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color BothTextColor => SelectedGender == "both"
        ? Colors.White : Color.FromArgb("#3D2817");
    public Color FemaleTextColor => SelectedGender == "female"
        ? Colors.White : Color.FromArgb("#3D2817");
    public Color MaleTextColor => SelectedGender == "male"
        ? Colors.White : Color.FromArgb("#3D2817");

    // ─── Étape 1 ─────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectPhotoVote()
    { IsPhotoVote = true; IsDuel = false; IsPoll = false; NavigateToStep(2); }

    [RelayCommand]
    private void SelectDuel()
    { IsDuel = true; IsPhotoVote = false; IsPoll = false; NavigateToStep(2); }

    [RelayCommand]
    private void SelectPoll()
    { IsPoll = true; IsPhotoVote = false; IsDuel = false; NavigateToStep(2); }

    // ─── Genre ───────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectGenderBoth() { SelectedGender = "both"; RefreshGenderColors(); }
    [RelayCommand]
    private void SelectGenderFemale() { SelectedGender = "female"; RefreshGenderColors(); }
    [RelayCommand]
    private void SelectGenderMale() { SelectedGender = "male"; RefreshGenderColors(); }

    private void RefreshGenderColors()
    {
        OnPropertyChanged(nameof(BothColor)); OnPropertyChanged(nameof(FemaleColor));
        OnPropertyChanged(nameof(MaleColor)); OnPropertyChanged(nameof(BothTextColor));
        OnPropertyChanged(nameof(FemaleTextColor)); OnPropertyChanged(nameof(MaleTextColor));
    }

    partial void OnMinAgeChanged(int value) { if (value > MaxAge) MinAge = value - 1; }
    partial void OnMaxAgeChanged(int value) { if (value < MinAge) MaxAge = value + 1; }
    // ─── Navigation ──────────────────────────────────────────────────
    [RelayCommand]
    private void GoToStep3() => NavigateToStep(3);

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 1) NavigateToStep(CurrentStep - 1);
        else Shell.Current.GoToAsync("..");
    }

    private void NavigateToStep(int step)
    {
        CurrentStep = step;
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
    }

    // ─── Pick photos ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(
                new MediaPickerOptions { Title = L.T("Create_YourPhoto") });
            if (result == null) return;
            if (!_imageService.IsValidImage(result.FileName, new FileInfo(result.FullPath).Length))
            { ShowError(L.T("Create_PhotoInvalid")); return; }
            var croppedStream = await _cropService.CropFreeAsync(result.FullPath);
            if (croppedStream == null) return;
            _photoStream = croppedStream;
            _photoFileName = result.FileName;
            PhotoPreview = ImageSource.FromFile(result.FullPath);
            HasError = false;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    [RelayCommand]
    private async Task PickPhotoLeftAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Photo A" });
            if (result == null) return;
            if (!_imageService.IsValidImage(result.FileName, new FileInfo(result.FullPath).Length))
            { ShowError(L.T("Create_PhotoInvalid")); return; }
            var croppedStream = await _cropService.CropFreeAsync(result.FullPath);
            if (croppedStream == null) return;
            _photoLeftStream = croppedStream;
            _photoLeftFileName = result.FileName;
            PhotoLeftPreview = ImageSource.FromFile(result.FullPath);
            HasError = false;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    [RelayCommand]
    private async Task PickPhotoRightAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Photo B" });
            if (result == null) return;
            if (!_imageService.IsValidImage(result.FileName, new FileInfo(result.FullPath).Length))
            { ShowError(L.T("Create_PhotoInvalid")); return; }
            var croppedStream = await _cropService.CropFreeAsync(result.FullPath);
            if (croppedStream == null) return;
            _photoRightStream = croppedStream;
            _photoRightFileName = result.FileName;
            PhotoRightPreview = ImageSource.FromFile(result.FullPath);
            HasError = false;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ─── Soumettre ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task SubmitProjectAsync()
    {
        HasError = false;

        if (string.IsNullOrWhiteSpace(Title))
        { ShowError(L.T("Create_ErrorTitle")); return; }

        if (IsPhotoVote && _photoStream == null)
        { ShowError(L.T("Create_ErrorPhoto")); return; }

        if (IsDuel && (_photoLeftStream == null || _photoRightStream == null))
        { ShowError(L.T("Create_ErrorTwoPhotos")); return; }

        if (IsPoll)
        {
            var opts = GetPollOptions();
            if (opts.Count < 2)
            { ShowError("Ajoute au moins 2 options au sondage."); return; }
        }

        IsLoading = true;

        try
        {
            UploadProgress = L.T("Create_CheckingCredits");
            var (creditOk, creditError) = await _creditService.SpendForProjectAsync();
            if (!creditOk) { ShowError(creditError); return; }

            var closesAt = _hasDuration
                ? DateTime.UtcNow.AddDays(_durationDays)
                : (DateTime?)null;

            bool success;
            string error;

            if (IsPhotoVote)
            {
                UploadProgress = L.T("Create_Compressing");
                (success, error, _) = await _projectService.CreatePhotoProjectAsync(
                    Title, Description, SelectedGender, MinAge, MaxAge,
                    _photoStream!, _photoFileName, closesAt, IsPrivate);
            }
            else if (IsDuel)
            {
                UploadProgress = L.T("Create_CompressingAll");
                (success, error, _) = await _projectService.CreateDuelProjectAsync(
                    Title, Description, SelectedGender, MinAge, MaxAge,
                    _photoLeftStream!, _photoLeftFileName,
                    _photoRightStream!, _photoRightFileName, closesAt, IsPrivate);
            }
            else // Poll
            {
                UploadProgress = "Création du sondage...";
                var pollOpts = GetPollOptions();
                (success, error, _) = await _projectService.CreatePollProjectAsync(
                    Title, Description, SelectedGender, MinAge, MaxAge,
                    pollOpts, closesAt, IsPrivate);
            }

            if (success) await OnProjectCreated();
            else
            {
                await _creditService.AddCreditsAsync(0, "refund", L.T("Create_Refund"));
                ShowError(error);
            }
        }
        catch (Exception ex) { ShowError($"Erreur : {ex.Message}"); }
        finally { IsLoading = false; UploadProgress = string.Empty; }
    }

    private async Task OnProjectCreated()
    {
        await Shell.Current.DisplayAlert(
            L.T("Create_SuccessTitle"), L.T("Create_SuccessMessage"), "OK");
        ResetForm();
        await Shell.Current.GoToAsync("..");
    }

    private void ResetForm()
    {
        CurrentStep = 1;
        Title = string.Empty; Description = string.Empty;
        PhotoPreview = null; PhotoLeftPreview = null; PhotoRightPreview = null;
        _photoStream?.Dispose(); _photoStream = null;
        _photoLeftStream?.Dispose(); _photoLeftStream = null;
        _photoRightStream?.Dispose(); _photoRightStream = null;
        IsPhotoVote = false; IsDuel = false; IsPoll = false;
        SelectedGender = "both"; IsPrivate = false;
        MinAge = 18; MaxAge = 99;
        PollOption1 = PollOption2 = PollOption3 = PollOption4 = PollOption5 = PollOption6 = string.Empty;
        ShowOption3 = ShowOption4 = ShowOption5 = ShowOption6 = false;
        NavigateToStep(1);
        RefreshGenderColors();
    }

    private void ShowError(string message) { ErrorMessage = message; HasError = true; }

    private async Task<int> GetProjectCostAsync() { return 0; }
}
