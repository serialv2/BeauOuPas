namespace BeauOuPas.Models;

public class FriendProjectItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool AlreadyVoted { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool HasPhoto => !string.IsNullOrEmpty(ThumbnailUrl);
    public string TypeEmoji => Type == "duel" ? "⚖️" : "📷";
}
