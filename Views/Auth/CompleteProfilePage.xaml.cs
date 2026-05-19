using BeauOuPas.ViewModels.Auth;

namespace BeauOuPas.Views.Auth;

public partial class CompleteProfilePage : ContentPage
{
    private readonly CompleteProfileViewModel _vm;

    public CompleteProfilePage(CompleteProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true; // Bloquer retour arrière
}
