using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class EditQuizQuestionPage : ContentPage
{
    private readonly EditQuizQuestionViewModel _vm;

    public EditQuizQuestionPage(EditQuizQuestionViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // ⚡ FIX : si on est en mode édition (QuestionId fourni via Shell parameters),
        // on récupère le CreateQuizViewModel ATTACHÉ À LA PAGE PARENTE dans la pile
        // de navigation Shell — pas une instance fraîche du DI container, qui aurait
        // une liste de questions vide.
        QuizQuestionItem? existing = null;

        if (!string.IsNullOrEmpty(_vm.QuestionId))
        {
            var createVm = FindParentCreateQuizViewModel();
            existing = createVm?.GetQuestionById(_vm.QuestionId);

            // Trace utile si jamais le VM parent n'est pas trouvé
            if (createVm == null)
                System.Diagnostics.Debug.WriteLine(
                    "[EditQuizQuestion] Parent CreateQuizViewModel introuvable dans la pile de navigation");
            else if (existing == null)
                System.Diagnostics.Debug.WriteLine(
                    $"[EditQuizQuestion] Question {_vm.QuestionId} non trouvée dans le VM parent");
        }

        _vm.Initialize(existing);
    }

    /// <summary>
    /// Cherche dans la pile de navigation Shell la page qui héberge le
    /// CreateQuizViewModel parent, et retourne son BindingContext.
    ///
    /// On parcourt la NavigationStack du Shell (et celle des sections si nécessaire)
    /// car le DI ne nous donnera pas la même instance de CreateQuizViewModel
    /// que celle qui est attachée à CreateQuizPage (Transient).
    /// </summary>
    private CreateQuizViewModel? FindParentCreateQuizViewModel()
    {
        try
        {
            var navigation = Shell.Current?.Navigation;
            if (navigation == null) return null;

            // Pile principale
            foreach (var page in navigation.NavigationStack)
            {
                if (page?.BindingContext is CreateQuizViewModel vm)
                    return vm;
            }

            // Pile modale (au cas où)
            foreach (var page in navigation.ModalStack)
            {
                if (page?.BindingContext is CreateQuizViewModel vm)
                    return vm;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EditQuizQuestion] FindParentCreateQuizViewModel: {ex.Message}");
        }

        return null;
    }
}
