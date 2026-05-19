namespace BeauOuPas.Views.Shared;

public partial class UpdateRequiredPage : ContentPage
{
    public UpdateRequiredPage()
    {
        InitializeComponent();
        UpdateButton.Clicked += async (s, e) =>
        {
            await Launcher.OpenAsync(
                "https://play.google.com/store/apps/details?id=com.beauoupas.app");
        };
    }
}
