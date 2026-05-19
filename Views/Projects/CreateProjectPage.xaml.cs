using BeauOuPas.ViewModels.Projects;

namespace BeauOuPas.Views.Projects;

public partial class CreateProjectPage : ContentPage
{
    public CreateProjectPage(CreateProjectViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}