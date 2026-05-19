namespace BeauOuPas.Models;

public class FriendItem
{
    public string FriendshipId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsRequester { get; set; }
    public DateTime CreatedAt { get; set; }

    // ✅ Avatar et statut en ligne
    public string? AvatarUrl { get; set; }
    public DateTime? LastSeen { get; set; }

    public bool IsPending => Status == "pending";
    public bool IsAccepted => Status == "accepted";
    public bool CanAccept => Status == "pending" && !IsRequester;
    public bool IsPendingSent => Status == "pending" && IsRequester;

    // ✅ Initial pour avatar
    public string AvatarInitial => Username.Length > 0
        ? Username[0].ToString().ToUpper() : "?";

    // ✅ Avatar disponible
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    // ✅ Statut en ligne
    public bool IsOnline => LastSeen.HasValue &&
        (DateTime.UtcNow - LastSeen.Value).TotalMinutes < 2;
    public bool IsRecent => LastSeen.HasValue &&
        (DateTime.UtcNow - LastSeen.Value).TotalHours < 1;

    public string OnlineStatusText
    {
        get
        {
            if (IsOnline) return "● En ligne";
            if (IsRecent) return "● Récemment";
            return string.Empty;
        }
    }

    public Color OnlineColor
    {
        get
        {
            if (IsOnline) return Color.FromArgb("#4A7A52");
            if (IsRecent) return Color.FromArgb("#C9943E");
            return Color.FromArgb("#8A6F4A");
        }
    }
}
