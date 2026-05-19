using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class SeriesResultsPage : ContentPage
{
    public SeriesResultsPage(SeriesResultsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
