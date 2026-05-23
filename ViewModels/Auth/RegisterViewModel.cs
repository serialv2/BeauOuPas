using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Auth;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _authService;

    public RegisterViewModel(AuthService authService) { _authService = authService; }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private DateTime _birthDate = DateTime.Today.AddYears(-18);
    [ObservableProperty] private string _selectedGender = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private bool _acceptedTerms = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;
    [ObservableProperty] private bool _isLoading = false;

    public DateTime MaxBirthDate => DateTime.Today.AddYears(-18);

    public List<string> GenderOptions => new()
    {
        L.T("Register_GenderMale"),
        L.T("Register_GenderFemale"),
        L.T("Register_GenderOther"),
        L.T("Register_GenderNone")
    };

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Username)) { ShowError("Le pseudo est obligatoire."); return; }
        if (string.IsNullOrWhiteSpace(Email)) { ShowError("L'email est obligatoire."); return; }
        if (string.IsNullOrWhiteSpace(Password)) { ShowError("Le mot de passe est obligatoire."); return; }
        if (Password != ConfirmPassword) { ShowError("Les mots de passe ne correspondent pas."); return; }
        if (Password.Length < 6) { ShowError("Le mot de passe doit contenir au moins 6 caractères."); return; }
        if (string.IsNullOrWhiteSpace(SelectedGender)) { ShowError("Veuillez sélectionner votre genre."); return; }

        var age = DateTime.Today.Year - BirthDate.Year;
        if (BirthDate.Date > DateTime.Today.AddYears(-age)) age--;
        if (age < 18) { ShowError(L.T("Register_ErrorAge")); return; }

        if (!AcceptedTerms) { ShowError(L.T("Register_ErrorTerms")); return; }

        IsLoading = true; HasError = false;

        var genderDb = SelectedGender switch
        {
            var s when s == L.T("Register_GenderMale") => "male",
            var s when s == L.T("Register_GenderFemale") => "female",
            var s when s == L.T("Register_GenderOther") => "other",
            _ => "prefer_not_to_say"
        };

        var (success, error) = await _authService.RegisterAsync(
            Email, Password, Username, DateOnly.FromDateTime(BirthDate),
            genderDb, string.IsNullOrWhiteSpace(Phone) ? null : Phone);

        IsLoading = false;

        if (!success)
        {
            // Cas email déjà utilisé — proposer les options
            if (error.Contains("existe déjà") || error.Contains("already registered"))
            {
                var choice = await Shell.Current.DisplayActionSheet(
                    "Email déjà utilisé",
                    L.T("Common_Cancel"),
                    null,
                    "Se connecter",
                    "Renvoyer l'email de confirmation");

                if (choice == "Se connecter")
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else if (choice == "Renvoyer l'email de confirmation")
                {
                    IsLoading = true;
                    var (resendOk, resendError) = await _authService.ResendConfirmationAsync(Email);
                    IsLoading = false;

                    if (resendOk)
                        await Shell.Current.DisplayAlert(
                            "Email envoyé",
                            "Un nouvel email de confirmation a été envoyé. Vérifiez votre boîte mail.",
                            L.T("Common_OK"));
                    else
                        ShowError(resendError);
                }
                return;
            }

            ShowError(error);
            return;
        }

        await Shell.Current.DisplayAlert(
            L.T("Register_SuccessTitle"),
            L.T("Register_SuccessMessage"),
            L.T("Common_OK"));
        await Shell.Current.GoToAsync("//LoginPage");
    }

    [RelayCommand]
    private async Task RegisterWithGoogleAsync()
    {
        IsLoading = true; HasError = false;
        var (success, error) = await _authService.LoginWithGoogleAsync();
        IsLoading = false;
        if (!success) ShowError(error);
    }

    [RelayCommand]
    private async Task RegisterWithAppleAsync()
    {
        IsLoading = true; HasError = false;
        var (success, error) = await _authService.LoginWithAppleAsync();
        IsLoading = false;
        if (!success) ShowError(error);
    }

    [RelayCommand]
    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//LoginPage");

    [RelayCommand]
    private async Task OpenTermsAsync()
    {
        try
        {
            await Browser.OpenAsync(
                "https://serialv2.github.io/beauoupas-web/terms.html",
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] OpenTerms: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenPrivacyAsync()
    {
        try
        {
            await Browser.OpenAsync(
                "https://serialv2.github.io/beauoupas-web/privacy.html",
                BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] OpenPrivacy: {ex.Message}");
        }
    }

    private void ShowError(string message) { ErrorMessage = message; HasError = true; }
}