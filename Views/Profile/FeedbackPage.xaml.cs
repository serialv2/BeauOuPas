using BeauOuPas.ViewModels.Profile;

namespace BeauOuPas.Views.Profile;

public partial class FeedbackPage : ContentPage
{
    public FeedbackPage(FeedbackViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
