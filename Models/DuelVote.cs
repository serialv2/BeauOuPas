using Postgrest.Attributes;
using Postgrest.Models;
namespace BeauOuPas.Models;

[Table("duel_votes")]
public class DuelVote : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("chosen_side")]
    public string ChosenSide { get; set; } = string.Empty; // "left" ou "right"

    [Column("voter_gender")]
    public string? VoterGender { get; set; }

    [Column("voter_age")]
    public int VoterAge { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
