using BeauOuPas.ViewModels.Dating;

namespace BeauOuPas.Views.Dating;

public partial class MatchesPage : ContentPage
{
    public MatchesPage(MatchesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MatchesViewModel vm)
            await vm.LoadMatchesAsync();
    }
}