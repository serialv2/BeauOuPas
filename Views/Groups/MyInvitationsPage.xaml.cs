using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class MyInvitationsPage : ContentPage
{
    private readonly MyInvitationsViewModel _vm;

    public MyInvitationsPage(MyInvitationsViewModel vm)
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
