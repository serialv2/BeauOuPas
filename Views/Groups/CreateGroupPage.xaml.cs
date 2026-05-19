using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class CreateGroupPage : ContentPage
{
    private readonly CreateGroupViewModel _vm;

    public CreateGroupPage(CreateGroupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadFriendsCommand.Execute(null);
    }
}
