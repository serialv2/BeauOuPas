using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("meet_requests")]
public class MeetRequest : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("from_user_id")]
    public string FromUserId { get; set; } = string.Empty;

    [Column("to_user_id")]
    public string ToUserId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("is_match")]
    public bool IsMatch { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}