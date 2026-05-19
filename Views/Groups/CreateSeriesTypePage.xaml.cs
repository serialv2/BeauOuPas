using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class CreateSeriesTypePage : ContentPage
{
    private readonly CreateSeriesTypeViewModel _vm;

    public CreateSeriesTypePage(CreateSeriesTypeViewModel vm)
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