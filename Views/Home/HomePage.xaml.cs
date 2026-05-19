using BeauOuPas.ViewModels.Home;

namespace BeauOuPas.Views.Home;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Recharge à chaque retour sur la page (crédits / nb groupes / projets
        // peuvent avoir changé pendant que l'user a navigué dans l'app).
        await _vm.LoadAsync();
    }
}
