using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class EditQuizQuestionsPage : ContentPage
{
    private EditQuizQuestionsViewModel? _vm;

    public EditQuizQuestionsPage(EditQuizQuestionsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EditQuizQuestionsViewModel vm)
        {
            await vm.LoadQuestionsIfNeededAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Pas de Cleanup ici car on peut revenir sur la page après l'édition
        // d'une question (navigation .. depuis EditQuizQuestionPage). Le ViewModel
        // doit rester abonné à QuestionSaved.
        //
        // Cleanup() sera appelé quand l'utilisateur fait "Annuler" ou
        // "Mettre à jour" (les commandes naviguent ".." depuis cette page).
    }

    // ─────────────────────────────────────────────────────────────────
    // ⚡ QW2 : tap sur une carte question
    // Mode normal → ouvre l'éditeur de question (EditQuestionCommand)
    // Mode select → toggle la coche
    // ─────────────────────────────────────────────────────────────────
    private void OnQuestionItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable) return;
        if (bindable.BindingContext is not QuizQuestionItem item) return;
        if (_vm == null) return;

        if (_vm.IsSelectionMode)
        {
            if (_vm.ToggleQuestionSelectionCommand.CanExecute(item))
                _vm.ToggleQuestionSelectionCommand.Execute(item);
        }
        else
        {
            if (_vm.EditQuestionCommand.CanExecute(item))
                _vm.EditQuestionCommand.Execute(item);
        }
    }

    // ⚡ QW2 : Retour Android sort du mode sélection plutôt que de la page
    protected override bool OnBackButtonPressed()
    {
        if (_vm != null && _vm.IsSelectionMode)
        {
            _vm.ToggleSelectionModeCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
