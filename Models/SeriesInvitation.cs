using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_invitations")]
public class SeriesInvitation : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("series_id")]
    public string SeriesId { get; set; } = string.Empty;

    [Column("invited_user_id")]
    public string InvitedUserId { get; set; } = string.Empty;

    [Column("invited_by")]
    public string InvitedBy { get; set; } = string.Empty;

    /// <summary>"pending" | "accepted" | "declined"</summary>
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }
}
