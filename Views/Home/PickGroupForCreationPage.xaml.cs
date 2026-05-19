using BeauOuPas.ViewModels.Home;

namespace BeauOuPas.Views.Home;

public partial class PickGroupForCreationPage : ContentPage
{
    private readonly PickGroupForCreationViewModel _vm;

    public PickGroupForCreationPage(PickGroupForCreationViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
