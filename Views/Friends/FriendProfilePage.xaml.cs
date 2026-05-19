using BeauOuPas.ViewModels.Friends;

namespace BeauOuPas.Views.Friends;

public partial class FriendProfilePage : ContentPage
{
    private readonly FriendProfileViewModel _vm;
    private bool _loaded = false;

    public FriendProfilePage(FriendProfileViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;

        _vm.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(FriendProfileViewModel.FriendUserId)
                && !string.IsNullOrEmpty(_vm.FriendUserId)
                && !_loaded)
            {
                _loaded = true;
                await _vm.LoadProfileAsync();
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(_vm.FriendUserId) && !_loaded)
        {
            _loaded = true;
            await _vm.LoadProfileAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Permet de recharger les projets quand on revient de ProjectDetailPage
        _loaded = false;
    }
}