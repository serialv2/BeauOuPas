using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("photo_votes")]
public class PhotoVote : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("rating")]
    public int Rating { get; set; }

    [Column("voter_gender")]
    public string? VoterGender { get; set; }

    [Column("voter_age")]
    public int VoterAge { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    public string RatingLabel => Rating switch
    {
        3 => "J'aime ❤️",
        2 => "Moyen 😐",
        1 => "Pas fan 👎",
        _ => "Inconnu"
    };
}