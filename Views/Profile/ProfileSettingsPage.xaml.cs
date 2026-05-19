using BeauOuPas.Helpers;
using BeauOuPas.ViewModels.Profile;

namespace BeauOuPas.Views.Profile;

public partial class ProfileSettingsPage : ContentPage
{
    private readonly ProfileSettingsViewModel _vm;

    public ProfileSettingsPage(ProfileSettingsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadProfileAsync();
    }

    // ─── Rafraîchir les permissions au retour des paramètres Android ──
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _vm.RefreshPermissionsAsync();
    }

    // ✅ Bloquer retour arrière si profil pas encore complet
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsProfileComplete) return false;
        return true;
    }

    // ⚡ MAI 2026 : Liens vers les pages légales hébergées (Lot 2/3 publication)

    private async void OnPrivacyTapped(object? sender, TappedEventArgs e)
    {
        await LegalLinks.OpenPrivacyAsync();
    }

    private async void OnTermsTapped(object? sender, TappedEventArgs e)
    {
        await LegalLinks.OpenTermsAsync();
    }

    private async void OnLegalTapped(object? sender, TappedEventArgs e)
    {
        await LegalLinks.OpenLegalAsync();
    }
}
