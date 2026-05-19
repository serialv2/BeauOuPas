using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("friendships")]
public class Friendship : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("requester_id")]
    public string RequesterId { get; set; } = string.Empty;

    [Column("receiver_id")]
    public string ReceiverId { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("friend_invites")]
public class FriendInvite : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("inviter_id")]
    public string InviterId { get; set; } = string.Empty;

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_by")]
    public string? UsedBy { get; set; }
}
