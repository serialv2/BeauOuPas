using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class CreateSeriesProjectPage : ContentPage
{
    public CreateSeriesProjectPage(CreateSeriesProjectViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
