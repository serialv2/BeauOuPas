using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class CreateSeriesProjectViewModel : ObservableObject
{
    private readonly SeriesService _seriesService;
    private readonly ProjectService _projectService;
    private readonly PhotoUploadService _uploadService;

    public CreateSeriesProjectViewModel(
        SeriesService seriesService,
        ProjectService projectService,
        PhotoUploadService uploadService)
    {
        _seriesService = seriesService;
        _projectService = projectService;
        _uploadService = uploadService;
    }

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;
    [ObservableProperty] private bool _isLoading = false;

    // ─── Type sélectionné ─────────────────────────────────────────
    [ObservableProperty] private string _selectedType = "photo_vote";
    [ObservableProperty] private bool _isPhotoType = true;
    [ObservableProperty] private bool _isDuelType = false;
    [ObservableProperty] private bool _isPollType = false;

    // ─── Champs communs ───────────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    // ─── Selfie automatique (par projet) ──────────────────────────
    [ObservableProperty] private bool _selfieEnabled = true;

    // ─── Photo vote — stockage en bytes ──────────────────────────
    [ObservableProperty] private ImageSource? _photoSource = null;
    private byte[]? _photoBytes;
    private string _photoFileName = string.Empty;

    // ─── Duel — stockage en bytes ─────────────────────────────────
    [ObservableProperty] private ImageSource? _photoLeftSource = null;
    [ObservableProperty] private ImageSource? _photoRightSource = null;
    private byte[]? _photoLeftBytes;
    private byte[]? _photoRightBytes;
    private string _photoLeftFileName = string.Empty;
    private string _photoRightFileName = string.Empty;

    // ─── Sondage ──────────────────────────────────────────────────
    [ObservableProperty] private string _option1 = string.Empty;
    [ObservableProperty] private string _option2 = string.Empty;
    [ObservableProperty] private string _option3 = string.Empty;
    [ObservableProperty] private string _option4 = string.Empty;

    // ─── Sélection type ───────────────────────────────────────────
    [RelayCommand]
    private void SelectType(string type)
    {
        SelectedType = type;
        IsPhotoType = type == "photo_vote";
        IsDuelType = type == "duel";
        IsPollType = type == "poll";
    }

    // ─── Picks photos — lecture en bytes immédiate ────────────────
    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        var result = await MediaPicker.PickPhotoAsync();
        if (result == null) return;
        using var stream = await result.OpenReadAsync();
        _photoBytes = await ReadAllBytesAsync(stream);
        _photoFileName = result.FileName;
        PhotoSource = ImageSource.FromStream(() => new MemoryStream(_photoBytes));
    }

    [RelayCommand]
    private async Task PickPhotoLeftAsync()
    {
        var result = await MediaPicker.PickPhotoAsync();
        if (result == null) return;
        using var stream = await result.OpenReadAsync();
        _photoLeftBytes = await ReadAllBytesAsync(stream);
        _photoLeftFileName = result.FileName;
        PhotoLeftSource = ImageSource.FromStream(() => new MemoryStream(_photoLeftBytes));
    }

    [RelayCommand]
    private async Task PickPhotoRightAsync()
    {
        var result = await MediaPicker.PickPhotoAsync();
        if (result == null) return;
        using var stream = await result.OpenReadAsync();
        _photoRightBytes = await ReadAllBytesAsync(stream);
        _photoRightFileName = result.FileName;
        PhotoRightSource = ImageSource.FromStream(() => new MemoryStream(_photoRightBytes));
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    // ─── Créer ────────────────────────────────────────────────────
    [RelayCommand]
    private async Task CreateAsync()
    {
        // ⚡ DIAG : chaque étape est loggée pour identifier où ça bloque.
        System.Diagnostics.Debug.WriteLine(
            $"[CreateProject-DIAG] ENTRÉE: SeriesId={SeriesId}, Type={SelectedType}, Title={Title}");

        if (string.IsNullOrWhiteSpace(Title))
        {
            System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] Titre vide → abort");
            await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateProject_TitleRequired"), L.T("Common_OK"));
            return;
        }

        IsLoading = true;
        try
        {
            string projectId = string.Empty;

            if (SelectedType == "photo_vote")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] photo_vote: bytes={_photoBytes?.Length ?? 0}");
                if (_photoBytes == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] Pas de photo → abort");
                    await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateProject_AddPhoto"), L.T("Common_OK"));
                    return;
                }
                System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] AVANT CreateSeriesPhotoProjectAsync");
                var (success, error, id) = await _projectService.CreateSeriesPhotoProjectAsync(
                    Title, Description,
                    new MemoryStream(_photoBytes), _photoFileName);
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] APRÈS CreateSeriesPhotoProjectAsync: success={success}, error={error}, id={id}");
                if (!success) { await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK")); return; }
                projectId = id;
            }
            else if (SelectedType == "duel")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] duel: leftBytes={_photoLeftBytes?.Length ?? 0}, rightBytes={_photoRightBytes?.Length ?? 0}");
                if (_photoLeftBytes == null || _photoRightBytes == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] Photos duel manquantes → abort");
                    await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateProject_AddBothPhotos"), L.T("Common_OK"));
                    return;
                }
                System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] AVANT CreateSeriesDuelProjectAsync");
                var (success, error, id) = await _projectService.CreateSeriesDuelProjectAsync(
                    Title, Description,
                    new MemoryStream(_photoLeftBytes), _photoLeftFileName,
                    new MemoryStream(_photoRightBytes), _photoRightFileName);
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] APRÈS CreateSeriesDuelProjectAsync: success={success}, error={error}, id={id}");
                if (!success) { await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK")); return; }
                projectId = id;
            }
            else if (SelectedType == "poll")
            {
                var options = new List<string> { Option1, Option2, Option3, Option4 }
                    .Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] poll: options.Count={options.Count}");
                if (options.Count < 2)
                {
                    System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] < 2 options → abort");
                    await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("CreateProject_Add2Options"), L.T("Common_OK"));
                    return;
                }
                System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] AVANT CreateSeriesPollProjectAsync");
                var (success, error, id) = await _projectService.CreateSeriesPollProjectAsync(
                    Title, Description, options);
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] APRÈS CreateSeriesPollProjectAsync: success={success}, error={error}, id={id}");
                if (!success) { await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK")); return; }
                projectId = id;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[CreateProject-DIAG] AVANT AddProjectToSeriesAsync: projectId={projectId}, selfieEnabled={SelfieEnabled}");
            // Propage le flag SelfieEnabled au service
            var (addSuccess, addError) = await _seriesService.AddProjectToSeriesAsync(
                SeriesId, projectId, selfieEnabled: SelfieEnabled);
            System.Diagnostics.Debug.WriteLine(
                $"[CreateProject-DIAG] APRÈS AddProjectToSeriesAsync: success={addSuccess}, error={addError}");
            if (!addSuccess)
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"), addError, L.T("Common_OK"));
                return;
            }

            System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] SUCCÈS COMPLET → GoToAsync(\"..\")");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // ⚡ DIAG : log enrichi (type + message + stack)
            System.Diagnostics.Debug.WriteLine(
                $"[CreateProject-DIAG] EXCEPTION: type={ex.GetType().Name}, message={ex.Message}");
            System.Diagnostics.Debug.WriteLine(
                $"[CreateProject-DIAG] STACK: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreateProject-DIAG] INNER: {ex.InnerException.Message}");
            }
            await Shell.Current.DisplayAlert(L.T("Common_Error"), ex.Message, L.T("Common_OK"));
        }
        finally
        {
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine("[CreateProject-DIAG] FINALLY: IsLoading=false");
        }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}