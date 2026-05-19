using System.Text.RegularExpressions;

namespace BeauOuPas.Views.Shared;

public partial class MaintenancePage : ContentPage
{
    public MaintenancePage(string message)
    {
        InitializeComponent();
        ParseAndDisplayMessage(message);
    }

    private void ParseAndDisplayMessage(string message)
    {
        // Détecter une URL dans le message (http:// ou https://)
        var urlRegex = new Regex(@"https?://[^\s]+", RegexOptions.IgnoreCase);
        var match = urlRegex.Match(message ?? string.Empty);

        if (match.Success)
        {
            var url = match.Value;
            // Afficher le texte sans l'URL
            var textOnly = message!.Replace(url, "").Trim();
            MessageLabel.Text = string.IsNullOrWhiteSpace(textOnly)
                ? "Maintenance en cours..." : textOnly;

            // Afficher le lien séparément
            LinkLabel.Text = url;
            LinkLabel.IsVisible = true;
            LinkTap.Command = new Command(async () =>
            {
                try { await Launcher.Default.OpenAsync(new Uri(url)); }
                catch { }
            });
        }
        else
        {
            MessageLabel.Text = string.IsNullOrWhiteSpace(message)
                ? "Maintenance en cours..." : message;
        }
    }
}
