using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class JoinSeriesPage : ContentPage
{
    public JoinSeriesPage(JoinSeriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
