using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("poll_votes")]
public class PollVote : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [Column("voter_gender")]
    public string? VoterGender { get; set; }

    [Column("voter_age")]
    public int? VoterAge { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
