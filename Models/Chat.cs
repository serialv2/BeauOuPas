using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("chats")]
public class Chat : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user1_id")]
    public string User1Id { get; set; } = string.Empty;

    [Column("user2_id")]
    public string User2Id { get; set; } = string.Empty;

    [Column("is_blocked")]
    public bool IsBlocked { get; set; } = false;

    [Column("blocked_by")]
    public string? BlockedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
