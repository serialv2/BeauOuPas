namespace BeauOuPas.Models;

public class UserSearchResult
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string RelationStatus { get; set; } = "none"; // none | pending_sent | pending_received | friend

    public string? FriendshipId { get; set; }

    public string AvatarInitial => Username.Length > 0
        ? Username[0].ToString().ToUpper() : "?";
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    public bool IsFriend => RelationStatus == "friend";
    public bool IsPendingSent => RelationStatus == "pending_sent";
    public bool IsPendingReceived => RelationStatus == "pending_received";
    public bool CanAdd => RelationStatus == "none";

    public string StatusLabel => RelationStatus switch
    {
        "friend" => "✅ Ami",
        "pending_sent" => "⏳ Demande envoyée",
        "pending_received" => "📩 Demande reçue",
        _ => string.Empty
    };
    public Color StatusColor => RelationStatus switch
    {
        "friend" => Color.FromArgb("#4A7A52"),
        "pending_sent" => Color.FromArgb("#C9943E"),
        "pending_received" => Color.FromArgb("#C2754C"),
        _ => Colors.Transparent
    };
}
