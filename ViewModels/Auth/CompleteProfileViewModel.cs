using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Auth;

public partial class CompleteProfileViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly LocationService _locationService;
    private readonly NotificationService _notificationService;
    private readonly Supabase.Client _supabase;

    public CompleteProfileViewModel(
        AuthService authService,
        LocationService locationService,
        NotificationService notificationService,
        Supabase.Client supabase)
    {
        _authService = authService;
        _locationService = locationService;
        _notificationService = notificationService;
        _supabase = supabase;

        // Valeur par défaut date naissance
        BirthDate = DateTime.Today.AddYears(-18);
    }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private DateTime _birthDate;
    [ObservableProperty] private string _selectedGender = "Préfère ne pas préciser";
    [ObservableProperty] private string _projectPreference = "both";
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private bool _hasAgeError = false;
    [ObservableProperty] private string _ageError = string.Empty;
    [ObservableProperty] private bool _locationGranted = false;
    [ObservableProperty] private bool _notifGranted = false;

    public DateTime MaxBirthDate => DateTime.Today.AddYears(-13);
    public DateTime MinBirthDate => DateTime.Today.AddYears(-120);

    public List<string> GenderOptions { get; } = new()
    {
        "Homme", "Femme", "Autre", "Préfère ne pas préciser"
    };

    // ─── Couleurs préférences projets ────────────────────────────────
    public Color PrefMaleColor => ProjectPreference == "male"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color PrefFemaleColor => ProjectPreference == "female"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color PrefOtherColor => ProjectPreference == "both"
        ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color PrefMaleTextColor => ProjectPreference == "male"
        ? Colors.White : Color.FromArgb("#3D2817");
    public Color PrefFemaleTextColor => ProjectPreference == "female"
        ? Colors.White : Color.FromArgb("#3D2817");
    public Color PrefOtherTextColor => ProjectPreference == "both"
        ? Colors.White : Color.FromArgb("#3D2817");

    // ─── Boutons géoloc / notifs ─────────────────────────────────────
    public string LocationButtonText => LocationGranted
        ? "✅ Géolocalisation autorisée"
        : "📍 Autoriser la géolocalisation";
    public Color LocationButtonColor => LocationGranted
        ? Color.FromArgb("#4A7A52") : Color.FromArgb("#C2754C");

    public string NotifButtonText => NotifGranted
        ? "✅ Notifications autorisées"
        : "🔔 Autoriser les notifications";
    public Color NotifButtonColor => NotifGranted
        ? Color.FromArgb("#4A7A52") : Color.FromArgb("#C2754C");

    // ─── Commandes préférences ────────────────────────────────────────
    [RelayCommand] private void SelectPrefMale()
    { ProjectPreference = "male"; RefreshPrefColors(); }
    [RelayCommand] private void SelectPrefFemale()
    { ProjectPreference = "female"; RefreshPrefColors(); }
    [RelayCommand] private void SelectPrefOther()
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

    // ─── Géolocalisation ──────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestLocationAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            LocationGranted = status == PermissionStatus.Granted;
            OnPropertyChanged(nameof(LocationButtonText));
            OnPropertyChanged(nameof(LocationButtonColor));
        }
        catch { LocationGranted = false; }
    }

    // ─── Notifications ────────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestNotifAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            NotifGranted = status == PermissionStatus.Granted;
            if (NotifGranted)
                await _notificationService.InitializeAsync();
            OnPropertyChanged(nameof(NotifButtonText));
            OnPropertyChanged(nameof(NotifButtonColor));
        }
        catch { NotifGranted = false; }
    }

    // ─── Sauvegarder le profil ────────────────────────────────────────
    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        HasError = false;
        HasAgeError = false;

        // Validation username
        if (string.IsNullOrWhiteSpace(Username) || Username.Length < 3)
        {
            HasError = true;
            ErrorMessage = L.T("CompleteProfile_UsernameRequired");
            return;
        }

        // Validation âge minimum 13 ans
        var age = DateTime.Today.Year - BirthDate.Year;
        if (DateTime.Today < BirthDate.AddYears(age)) age--;
        if (age < 13)
        {
            HasAgeError = true;
            AgeError = L.T("CompleteProfile_AgeError");
            return;
        }

        IsLoading = true;
        try
        {
            var userId = _authService.CurrentUser?.Id;
            if (userId == null) return;

            // ⚡ Vérifier que le pseudo n'est pas déjà pris (case-insensitive).
            // On exclut son propre userId pour permettre de garder le pseudo actuel.
            var available = await _authService.IsUsernameAvailableAsync(Username.Trim(), userId);
            if (!available)
            {
                HasError = true;
                ErrorMessage = L.T("Common_UsernameTaken");
                IsLoading = false;
                return;
            }

            var genderDb = SelectedGender switch
            {
                "Homme" => "male",
                "Femme" => "female",
                "Autre" => "other",
                _ => "prefer_not_to_say"
            };

            await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.Username, Username.Trim())
                .Set(p => p.BirthDate, BirthDate.ToString("yyyy-MM-dd"))
                .Set(p => p.Gender, genderDb)
                .Set(p => p.DatingPreference, ProjectPreference)
                .Set(p => p.ProfileCompleted, true)
                .Update();

            // Naviguer vers l'app principale
            if (Shell.Current is AppShell shell)
                shell.GoToMainApp();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally { IsLoading = false; }
    }
}
