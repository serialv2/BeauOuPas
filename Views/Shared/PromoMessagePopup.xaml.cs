using CommunityToolkit.Maui.Views;

namespace BeauOuPas.Views.Shared;

public partial class PromoMessagePopup : Popup
{
    public PromoMessagePopup(string title, string message)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        CloseButton.Clicked += (s, e) => Close();
    }
}