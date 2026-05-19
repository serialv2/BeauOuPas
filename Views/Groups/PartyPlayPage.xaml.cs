using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

/// <summary>
/// Code-behind de l'écran de vote joueur "Partie Rapide".
///
/// Calqué sur QuizPlayPage : le ViewModel est injecté via DI, InitAsync()
/// est déclenché dans OnAppearing(), CleanupAsync() dans OnDisappearing().
/// Le ViewModel reçoit SeriesId / SeriesTitle via [QueryProperty] (paramètres
/// de navigation Shell), donc OnAppearing arrive après que les query params
/// soient appliqués.
/// </summary>
public partial class PartyPlayPage : ContentPage
{
    private readonly PartyPlayViewModel _vm;

    public PartyPlayPage(PartyPlayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _vm.CleanupAsync();
    }
}
