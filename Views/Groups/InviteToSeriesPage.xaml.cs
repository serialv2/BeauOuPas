using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class InviteToSeriesPage : ContentPage
{
    public InviteToSeriesPage(InviteToSeriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
