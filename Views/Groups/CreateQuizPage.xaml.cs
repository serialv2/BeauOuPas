using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class CreateQuizPage : ContentPage
{
    private readonly CreateQuizViewModel _vm;

    public CreateQuizPage(CreateQuizViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // ⚡ E2c : désabonner l'évènement QuestionSaved pour éviter les fuites mémoire
        // ⚠️ Ne pas désabonner dans OnDisappearing si la page peut réapparaître
        //    (cas où on revient depuis EditQuizQuestionPage). On ne désabonne donc QUE
        //    quand on quitte définitivement la pile (vers GroupDetailPage).
        //
        // Solution : on regarde si on est toujours dans la pile Shell ou pas.
        if (Shell.Current?.Navigation?.NavigationStack != null
            && !Shell.Current.Navigation.NavigationStack.Contains(this))
        {
            _vm.Cleanup();
        }
    }
}
