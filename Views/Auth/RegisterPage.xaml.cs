using BeauOuPas.Helpers;
using BeauOuPas.ViewModels.Auth;

namespace BeauOuPas.Views.Auth;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // ⚡ MAI 2026 : tap sur "CGU" → ouvre terms.html dans in-app browser
    private async void OnTermsTapped(object? sender, TappedEventArgs e)
    {
        await LegalLinks.OpenTermsAsync();
    }

    // ⚡ MAI 2026 : tap sur "Politique de confidentialité" → ouvre privacy.html
    private async void OnPrivacyTapped(object? sender, TappedEventArgs e)
    {
        await LegalLinks.OpenPrivacyAsync();
    }
}
