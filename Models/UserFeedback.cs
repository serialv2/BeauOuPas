using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("user_feedback")]
public class UserFeedback : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("rating")]
    public int Rating { get; set; } = 5;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("platform")]
    public string Platform { get; set; } = "android";

    [Column("app_version")]
    public string AppVersion { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
