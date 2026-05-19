using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

/// <summary>
/// Page de création d'une PARTIE RAPIDE (party game "Most Likely To").
///
/// Le ViewModel (CreatePartyViewModel) est injecté par DI — pense à
/// l'enregistrer dans MauiProgram.cs et la route Shell "CreatePartyPage"
/// (voir README-PARTIE-RAPIDE-LOT5.md).
///
/// Les catégories du picker sont chargées dynamiquement à chaque
/// apparition de la page (la banque party_questions peut évoluer côté
/// admin VB.NET entre deux ouvertures).
/// </summary>
public partial class CreatePartyPage : ContentPage
{
    private readonly CreatePartyViewModel _vm;

    public CreatePartyPage(CreatePartyViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePartyPage] OnAppearing: {ex.Message}");
        }
    }
}
