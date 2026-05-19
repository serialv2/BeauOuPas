using BeauOuPas.ViewModels.Dashboard;
using CommunityToolkit.Mvvm.Input;

namespace BeauOuPas.Views.Dashboard;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DashboardViewModel vm)
            await vm.LoadProfileAsync();
    }
    [RelayCommand]
    private async Task GoToMatchesAsync()
    {
        if (BindingContext is DashboardViewModel vm)
            await vm.GoToMatchesCommand.ExecuteAsync(null);
    }
}