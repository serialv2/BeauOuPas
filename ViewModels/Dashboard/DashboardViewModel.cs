using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly CreditService _creditService;
    private readonly ProjectService _projectService;
    private readonly AppSettingsService _settingsService;

    public DashboardViewModel(
        AuthService authService,
        CreditService creditService,
        ProjectService projectService,
        AppSettingsService settingsService)
    {
        _authService = authService;
        _creditService = creditService;
        _projectService = projectService;
        _settingsService = settingsService;
    }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _avatarUrl = string.Empty;
    [ObservableProperty] private int _totalProjects = 0;
    [ObservableProperty] private int _credits = 0;
    [ObservableProperty] private bool _isLoading = false;

    // ✅ Contrôle visibilité rencontres
    [ObservableProperty] private bool _meetVisible = false;

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        try
        {
            IsLoading = true;
            await _authService.EnsureProfileExistsAsync();

            var profile = await _authService.GetCurrentProfileAsync();
            if (profile != null)
            {
                Username = profile.Username;
                AvatarUrl = profile.AvatarUrl ?? string.Empty;
            }

            Credits = await _creditService.GetCreditsAsync();

            var projects = await _projectService.GetMyProjectsAsync();
            TotalProjects = projects.Count;

            // ✅ Vérifier si les rencontres sont activées globalement
            MeetVisible = await _settingsService.IsMeetEnabledAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erreur chargement profil: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GoToMyProjectsAsync()
        => await Shell.Current.GoToAsync("MyProjectsPage");   // ⚡ FIX : retrait du "//" qui causait l'erreur "Global routes currently cannot be the only page on the stack"

    [RelayCommand]
    private async Task GoToCreditsAsync()
        => await Shell.Current.GoToAsync("CreditsPage");

    [RelayCommand]
    private async Task GoToMatchesAsync()
        => await Shell.Current.GoToAsync("MatchesPage");

    [RelayCommand]
    private async Task GoToSettingsAsync()
        => await Shell.Current.GoToAsync("ProfileSettingsPage");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Déconnexion",
            "Voulez-vous vraiment vous déconnecter ?",
            "Oui", "Non");
        if (!confirm) return;

        await _authService.LogoutAsync();
        if (Shell.Current is AppShell shell)
            shell.GoToAuth();
    }
}