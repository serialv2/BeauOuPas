using CommunityToolkit.Maui.Views;

namespace BeauOuPas.Views.Auth;

public partial class BanPopup : Popup
{
    public BanPopup(string reason, string banDate)
    {
        InitializeComponent();
        BanReasonLabel.Text = reason;
        BanDateLabel.Text = banDate;
        CloseButton.Clicked += (s, e) => Close();
    }
}