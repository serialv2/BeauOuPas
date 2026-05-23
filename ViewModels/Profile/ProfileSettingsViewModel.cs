using BeauOuPas.Localization;
using BeauOuPas.Models;
using BeauOuPas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeauOuPas.ViewModels.Profile;

public partial class ProfileSettingsViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CreditService _creditService;
    private readonly Supabase.Client _supabase;
    private readonly AvatarService _avatarService;
    private readonly AppSettingsService _settingsService;
    private readonly LocationService _locationService;
    private readonly NotificationService _notificationService;

    public ProfileSettingsViewModel(
        AuthService authService,
        CreditService creditService,
        Supabase.Client supabase,
        AvatarService avatarService,
        AppSettingsService settingsService,
        LocationService locationService,
        NotificationService notificationService)
    {
        _authService = authService;
        _creditService = creditService;
        _supabase = supabase;
        _avatarService = avatarService;
        _settingsService = settingsService;
        _locationService = locationService;
        _notificationService = notificationService;
    }

    // ─── Propriétés profil ───────────────────────────────────────────
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _selectedGender = string.Empty;
    [ObservableProperty] private int _credits = 0;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _hasSuccess = false;

    // ─── Date de naissance ───────────────────────────────────────────
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-18);
    public DateTime MaxBirthDate => DateTime.Today.AddYears(-13);
    public DateTime MinBirthDate => DateTime.Today.AddYears(-120);

    // ─── Préférence projets ──────────────────────────────────────────
    [ObservableProperty] private string _projectPreference = "both";
    public Color PrefMaleColor => ProjectPreference == "male"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color PrefFemaleColor => ProjectPreference == "female"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color PrefOtherColor => ProjectPreference == "both"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color PrefMaleTextColor => ProjectPreference == "male"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");
    public Color PrefFemaleTextColor => ProjectPreference == "female"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");
    public Color PrefOtherTextColor => ProjectPreference == "both"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");

    // ─── Géoloc / Notifs ────────────────────────────────────────────
    [ObservableProperty] private bool _locationGranted = false;
    [ObservableProperty] private bool _notifGranted = false;
    public string LocationButtonText => LocationGranted
        ? L.T("Profile_Location_Granted") : L.T("Profile_Location_Request");
    public Color LocationButtonColor => LocationGranted
        ? GetColor("Success") : GetColor("Primary");
    public string NotifButtonText => NotifGranted
        ? L.T("Profile_Notif_Granted") : L.T("Profile_Notif_Request");
    public Color NotifButtonColor => NotifGranted
        ? GetColor("Success") : GetColor("Primary");

    // ─── Propriétés rencontres ───────────────────────────────────────
    [ObservableProperty] private bool _datingEnabled = false;
    [ObservableProperty] private string _datingPreference = "both";
    [ObservableProperty] private bool _datingBoth = true;
    [ObservableProperty] private bool _datingFemale = false;
    [ObservableProperty] private bool _datingMale = false;

    // ─── Couleurs boutons genre rencontre ────────────────────────────
    public Color DatingBothColor => DatingPreference == "both"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color DatingFemaleColor => DatingPreference == "female"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color DatingMaleColor => DatingPreference == "male"
        ? GetColor("SelectionActive") : GetColor("SelectionInactive");
    public Color DatingBothTextColor => DatingPreference == "both"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");
    public Color DatingFemaleTextColor => DatingPreference == "female"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");
    public Color DatingMaleTextColor => DatingPreference == "male"
        ? GetColor("SelectionActiveText") : GetColor("SelectionInactiveText");

    // ─── Propriétés V2 ───────────────────────────────────────────────
    [ObservableProperty] private string? _avatarUrl;
    [ObservableProperty] private string _avatarInitial = string.Empty;
    [ObservableProperty] private bool _hasAvatar = false;
    [ObservableProperty] private bool _isUploadingAvatar = false;
    [ObservableProperty] private string _avatarUploadProgress = string.Empty;
    [ObservableProperty] private bool _feedbackVisible = false;
    [ObservableProperty] private bool _isProfileComplete = true;
    [ObservableProperty] private bool _meetVisible = false;

    public List<string> GenderOptions { get; } = new()
    {
        L.T("Gender_Male"),
        L.T("Gender_Female"),
        L.T("Gender_Other"),
        L.T("Gender_PreferNotToSay")
    };

    // ─── Charger le profil ───────────────────────────────────────────
    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        IsLoading = true;
        try
        {
            var profile = await _authService.GetCurrentProfileAsync();
            if (profile == null) return;

            Username = profile.Username;
            Phone = profile.Phone ?? string.Empty;
            Credits = profile.Credits;
            AvatarUrl = profile.AvatarUrl;
            HasAvatar = !string.IsNullOrEmpty(profile.AvatarUrl);
            AvatarInitial = profile.Username.Length > 0
                ? profile.Username[0].ToString().ToUpper() : "?";

            IsProfileComplete = profile.ProfileCompleted;
            MeetVisible = await _settingsService.IsMeetEnabledAsync();
            FeedbackVisible = await _settingsService.IsFeedbackEnabledAsync();
            DatingEnabled = profile.DatingEnabled;
            DatingPreference = profile.DatingPreference ?? "both";
            ProjectPreference = profile.DatingPreference ?? "both";

            SelectedGender = profile.Gender switch
            {
                "male" => L.T("Gender_Male"),
                "female" => L.T("Gender_Female"),
                "other" => L.T("Gender_Other"),
                "prefer_not_to_say" => L.T("Gender_PreferNotToSay"),
                _ => L.T("Gender_PreferNotToSay")
            };

            Email = _authService.CurrentUser?.Email ?? string.Empty;
            Credits = await _creditService.GetCreditsAsync();

            RefreshDatingColors();

            if (!string.IsNullOrEmpty(profile.BirthDate) &&
                DateTime.TryParse(profile.BirthDate, out var bd))
                BirthDate = bd;

            RefreshPrefColors();
            await RefreshPermissionsAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Rafraîchir les permissions (appelé aussi depuis OnNavigatedTo) ─
    public async Task RefreshPermissionsAsync()
    {
        var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        LocationGranted = locStatus == PermissionStatus.Granted;
        OnPropertyChanged(nameof(LocationButtonText));
        OnPropertyChanged(nameof(LocationButtonColor));

        var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        NotifGranted = notifStatus == PermissionStatus.Granted;
        OnPropertyChanged(nameof(NotifButtonText));
        OnPropertyChanged(nameof(NotifButtonColor));
    }

    // ─── Sauvegarder le profil ───────────────────────────────────────
    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowError("Le pseudo est obligatoire.");
            return;
        }

        IsLoading = true;
        HasError = false;
        HasSuccess = false;

        try
        {
            var userId = _authService.CurrentUser?.Id;
            if (userId == null) return;

            // ⚡ Vérifier que le pseudo n'est pas déjà pris (case-insensitive).
            // On exclut son propre userId pour permettre de garder son pseudo actuel.
            var available = await _authService.IsUsernameAvailableAsync(Username.Trim(), userId);
            if (!available)
            {
                ShowError(L.T("Common_UsernameTaken"));
                IsLoading = false;
                return;
            }

            var genderDb =
                  SelectedGender == L.T("Gender_Male") ? "male"
                : SelectedGender == L.T("Gender_Female") ? "female"
                : SelectedGender == L.T("Gender_Other") ? "other"
                : "prefer_not_to_say";

            await _supabase
                .From<Models.Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Username, Username ?? string.Empty)
                .Set(p => p.Phone, Phone ?? string.Empty)
                .Set(p => p.Gender, genderDb)
                .Set(p => p.BirthDate, BirthDate.ToString("yyyy-MM-dd"))
                .Set(p => p.DatingEnabled, DatingEnabled)
                .Set(p => p.DatingPreference, DatingPreference)
                .Set(p => p.ProfileCompleted, true)
                .Update();

            ShowSuccess("Profil mis à jour avec succès !");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Géolocalisation ─────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestLocationAsync()
    {
        try
        {
            // Si déjà accordée → bouton vert, rien à faire
            if (LocationGranted) return;

            // On tente toujours RequestAsync :
            // - 1ère fois : affiche le dialog natif Android
            // - Permission refusée sans "Ne plus demander" : re-affiche le dialog
            // - Permission refusée avec "Ne plus demander" : ne fait rien silencieusement
            var result = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (result == PermissionStatus.Granted)
            {
                LocationGranted = true;
            }
            else
            {
                // RequestAsync n'a rien affiché (bloqué par Android) → ouvrir les settings
                LocationGranted = false;
                AppInfo.Current.ShowSettingsUI();
            }

            OnPropertyChanged(nameof(LocationButtonText));
            OnPropertyChanged(nameof(LocationButtonColor));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Location] {ex.Message}");
        }
    }

    // ─── Notifications ───────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestNotifAsync()
    {
        try
        {
            // Si déjà accordée → bouton vert, rien à faire
            if (NotifGranted) return;

            var result = await Permissions.RequestAsync<Permissions.PostNotifications>();

            if (result == PermissionStatus.Granted)
            {
                NotifGranted = true;
                await _notificationService.InitializeAsync();
            }
            else
            {
                // RequestAsync n'a rien affiché → ouvrir les settings
                NotifGranted = false;
                AppInfo.Current.ShowSettingsUI();
            }

            OnPropertyChanged(nameof(NotifButtonText));
            OnPropertyChanged(nameof(NotifButtonColor));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notif] {ex.Message}");
        }
    }

    // ─── Préférence rencontre ────────────────────────────────────────
    [RelayCommand]
    private void SelectDatingBoth()
    { DatingPreference = "both"; RefreshDatingColors(); }

    [RelayCommand]
    private void SelectDatingFemale()
    { DatingPreference = "female"; RefreshDatingColors(); }

    [RelayCommand]
    private void SelectDatingMale()
    { DatingPreference = "male"; RefreshDatingColors(); }

    private void RefreshDatingColors()
    {
        OnPropertyChanged(nameof(DatingBothColor));
        OnPropertyChanged(nameof(DatingFemaleColor));
        OnPropertyChanged(nameof(DatingMaleColor));
        OnPropertyChanged(nameof(DatingBothTextColor));
        OnPropertyChanged(nameof(DatingFemaleTextColor));
        OnPropertyChanged(nameof(DatingMaleTextColor));
    }

    // ─── Préférence projets ──────────────────────────────────────────
    [RelayCommand]
    private void SelectPrefMale()
    { ProjectPreference = "male"; RefreshPrefColors(); }

    [RelayCommand]
    private void SelectPrefFemale()
    { ProjectPreference = "female"; RefreshPrefColors(); }

    [RelayCommand]
    private void SelectPrefOther()
    { ProjectPreference = "both"; RefreshPrefColors(); }

    private void RefreshPrefColors()
    {
        OnPropertyChanged(nameof(PrefMaleColor));
        OnPropertyChanged(nameof(PrefFemaleColor));
        OnPropertyChanged(nameof(PrefOtherColor));
        OnPropertyChanged(nameof(PrefMaleTextColor));
        OnPropertyChanged(nameof(PrefFemaleTextColor));
        OnPropertyChanged(nameof(PrefOtherTextColor));
    }

    // ─── Supprimer son compte (VRAIE suppression définitive) ─────────
    // ⚠️ Appel la RPC SQL `delete_my_account()` qui supprime DÉFINITIVEMENT
    // toutes les données de l'utilisateur dans toute la base, puis le
    // compte auth.users lui-même. Conforme RGPD et exigences Google Play.
    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        // Première confirmation
        bool confirm = await Shell.Current.DisplayAlert(
            L.T("DeleteAccount_Confirm1_Title"),
            L.T("DeleteAccount_Confirm1_Msg"),
            L.T("DeleteAccount_Confirm1_Yes"),
            L.T("Common_Cancel"));

        if (!confirm) return;

        // Deuxième confirmation (Google Play exige 2 étapes)
        bool confirm2 = await Shell.Current.DisplayAlert(
            L.T("DeleteAccount_Confirm2_Title"),
            L.T("DeleteAccount_Confirm2_Msg"),
            L.T("DeleteAccount_Confirm2_Yes"),
            L.T("DeleteAccount_Confirm2_No"));

        if (!confirm2) return;

        IsLoading = true;
        try
        {
            // Appel de la VRAIE suppression
            var (success, error) = await _authService.DeleteAccountAsync();

            if (!success)
            {
                await Shell.Current.DisplayAlert(
                    L.T("Common_Error"),
                    L.F("DeleteAccount_Error_Msg", error),
                    L.T("Common_OK"));
                return;
            }

            // Suppression réussie → confirmer à l'utilisateur
            await Shell.Current.DisplayAlert(
                L.T("DeleteAccount_Success_Title"),
                L.T("DeleteAccount_Success_Msg"),
                L.T("Common_OK"));

            // Rediriger vers l'écran d'auth
            if (Shell.Current is AppShell shell)
                shell.GoToAuth();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.F("DeleteAccount_Exception_Msg", ex.Message),
                L.T("Common_OK"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Déconnexion ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            L.T("Dashboard_Logout_Title"),
            L.T("Dashboard_Logout_Msg"),
            L.T("Common_Yes"), L.T("Common_No"));

        if (!confirm) return;

        await _authService.LogoutAsync();

        if (Shell.Current is AppShell shell)
            shell.GoToAuth();
    }

    // ─── Changer l'avatar ────────────────────────────────────────────
    [RelayCommand]
    private async Task ChangeAvatarAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(
                new MediaPickerOptions { Title = L.T("Profile_ChangeAvatar") });
            if (result == null) return;

            IsUploadingAvatar = true;
            AvatarUploadProgress = L.T("Profile_UploadingAvatar");

            await using var stream = await result.OpenReadAsync();
            var (success, url, error) = await _avatarService
                .UploadAvatarAsync(stream, result.FileName);

            if (success && url != null)
            {
                AvatarUrl = url;
                HasAvatar = true;
                AvatarUploadProgress = string.Empty;
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    L.T("Common_Error"), error, L.T("Common_OK"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Avatar] {ex.Message}");
        }
        finally
        {
            IsUploadingAvatar = false;
            AvatarUploadProgress = string.Empty;
        }
    }
    [RelayCommand]
    private async Task JoinSeriesAsync()
    => await Shell.Current.GoToAsync("JoinSeriesPage");
    // ─── Ouvrir feedback ─────────────────────────────────────────────
    [RelayCommand]
    private async Task OpenFeedbackAsync()
    {
        await Shell.Current.GoToAsync("FeedbackPage");
    }

    // ─── Helpers ─────────────────────────────────────────────────────
    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        HasSuccess = false;
    }

    private void ShowSuccess(string message)
    {
        SuccessMessage = message;
        HasSuccess = true;
        HasError = false;
    }

    // ─── Accès aux ressources de couleur (Colors.xaml) ───────────────
    // Permet de référencer les couleurs définies dans Colors.xaml
    // depuis le ViewModel. Si tu changes une couleur dans Colors.xaml,
    // toutes les propriétés calculées de ce ViewModel suivent automatiquement.
    private static Color GetColor(string key)
    {
        if (Application.Current?.Resources?.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }
        // Fallback si la ressource n'existe pas (ne devrait jamais arriver)
        return Colors.Gray;
    }
}
