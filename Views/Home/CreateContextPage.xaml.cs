using BeauOuPas.ViewModels.Home;

namespace BeauOuPas.Views.Home;

public partial class CreateContextPage : ContentPage
{
    private readonly CreateContextViewModel _vm;

    public CreateContextPage(CreateContextViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Recharge à chaque apparition pour bien gérer la visibilité de la
        // 3e carte (l'utilisateur peut avoir créé un groupe entre-temps).
        await _vm.LoadAsync();
    }
}
