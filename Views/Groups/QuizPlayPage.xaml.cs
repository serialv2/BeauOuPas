using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class QuizPlayPage : ContentPage
{
    private readonly QuizPlayViewModel _vm;

    // Pour gérer la confirmation de sortie en plein quiz
    private bool _confirmingExit = false;
    private bool _exitConfirmed = false;

    public QuizPlayPage(QuizPlayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (string.IsNullOrEmpty(_vm.SeriesId)) return;
        await _vm.InitAsync();
    }

    /// <summary>
    /// Interception du bouton Retour Android : on demande confirmation
    /// avant de quitter (sauf si la série est terminée ou en erreur).
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsFinish || _vm.IsError || _exitConfirmed)
        {
            return base.OnBackButtonPressed();
        }
        if (_confirmingExit) return true;
        _ = HandleExitConfirmationAsync();
        return true;
    }

    private async Task HandleExitConfirmationAsync()
    {
        _confirmingExit = true;
        try
        {
            bool confirm = await DisplayAlert(
                "Quitter le quiz ?",
                "Si tu quittes maintenant, tu sortiras de la session en cours. " +
                "Tu pourras le rejoindre depuis la page de la série.",
                "Quitter", "Continuer à jouer");

            if (confirm)
            {
                _exitConfirmed = true;
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlayPage] HandleExit: {ex.Message}");
        }
        finally
        {
            _confirmingExit = false;
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _vm.CleanupAsync();
        _vm.Dispose();
    }
}
