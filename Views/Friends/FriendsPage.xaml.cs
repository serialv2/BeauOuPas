using BeauOuPas.ViewModels.Friends;

namespace BeauOuPas.Views.Friends;

public partial class FriendsPage : ContentPage
{
    private readonly FriendsViewModel _vm;

    public FriendsPage(FriendsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadFriendsAsync();
    }
}
