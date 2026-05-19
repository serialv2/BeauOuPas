using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Views.Auth;
using BeauOuPas.Localization;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas.ViewModels.Auth;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;
    private readonly CreditService _creditService;

    public LoginViewModel(AuthService authService, NotificationService notificationService, CreditService creditService)
    {
        _authService = authService;
        _notificationService = notificationService;
        _creditService = creditService;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasError = false;

    // ⚡ NOUVEAU : pilote l'overlay plein écran lors des auth OAuth
    // (Google, Apple). Reste affiché pendant tout le flow OAuth :
    // clic → WebView Google → page blanche Supabase → retour app.
    [ObservableProperty] private bool _isAuthenticating = false;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError(L.T("Login_ErrorFields"));
            return;
        }
        IsLoading = true;
        HasError = false;
        var result = await _authService.LoginAsync(Email, Password);
        IsLoading = false;
        await HandleLoginResultAsync(result);
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        IsAuthenticating = true;
        HasError = false;
        try
        {
            var (success, error) = await _authService.LoginWithGoogleAsync();
            if (!success) ShowError(error);
        }
        finally
        {
            IsAuthenticating = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithAppleAsync()
    {
        IsAuthenticating = true;
        HasError = false;
        try
        {
            var (success, error) = await _authService.LoginWithAppleAsync();
            if (!success) ShowError(error);
        }
        finally
        {
            IsAuthenticating = false;
        }
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        var emailToReset = Email.Trim();
        if (string.IsNullOrWhiteSpace(emailToReset))
        {
            emailToReset = await Shell.Current.DisplayPromptAsync(
                L.T("Login_ForgotTitle"),
                L.T("Login_ForgotPrompt"),
                L.T("Login_ForgotSend"),
                L.T("Common_Cancel"),
                "votre@email.com",
                keyboard: Keyboard.Email);
            if (string.IsNullOrWhiteSpace(emailToReset)) return;
            Email = emailToReset;
        }
        IsLoading = true; HasError = false;
        var (success, error) = await _authService.ResetPasswordAsync(emailToReset);
        IsLoading = false;
        if (success)
            await Shell.Current.DisplayAlert(L.T("Login_ForgotSentTitle"),
                $"{L.T("Login_ForgotSentMessage")} ({emailToReset})", L.T("Common_OK"));
        else
            ShowError(error);
    }

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync("RegisterPage");

    public async Task HandleLoginResultAsync(AuthService.LoginResult result)
    {
        if (result.IsBanned)
        {
            var banDate = result.BannedAt.HasValue ? result.BannedAt.Value.ToString("dd/MM/yyyy") : "?";
            var reason = string.IsNullOrWhiteSpace(result.BannedReason) ? "Aucune raison." : result.BannedReason;
            var popup = new BanPopup(reason, banDate);
            await Microsoft.Maui.Controls.Application.Current!.MainPage!.ShowPopupAsync(popup);
            return;
        }
        if (!result.Success) { ShowError(result.ErrorMessage); return; }

        await _creditService.GrantWelcomeCreditsIfNeededAsync();

        if (Shell.Current is AppShell appShell) appShell.GoToMainApp();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await _notificationService.InitializeAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"FCM error: {ex.Message}"); }
        });

        await Task.Delay(1000);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Microsoft.Maui.Controls.Application.Current is App app)
                await app.ShowPromoMessageIfNeededAsync();
        });
    }

    private void ShowError(string message) { ErrorMessage = message; HasError = true; }
}
