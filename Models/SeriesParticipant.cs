using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_participants")]
public class SeriesParticipant : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("joined_at")] public DateTime JoinedAt { get; set; }
}
