using BeauOuPas.ViewModels.Credits;

namespace BeauOuPas.Views.Credits;

public partial class CreditsPage : ContentPage
{
    public CreditsPage(CreditsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CreditsViewModel vm)
            await vm.LoadAsync();
    }
}