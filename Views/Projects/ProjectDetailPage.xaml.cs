using BeauOuPas.ViewModels;

namespace BeauOuPas.Views.Projects;

public partial class ProjectDetailPage : ContentPage
{
    public ProjectDetailPage(ProjectDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            IsVisible = true,
            IsEnabled = true,
            Command = new Command(async () =>
            {
                await Shell.Current.Navigation.PopAsync();
            })
        });
    }
}