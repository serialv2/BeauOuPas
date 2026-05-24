using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_selfies")]
public class SeriesSelfie : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    /// <summary>
    /// NULL pour les selfies de session quiz/party (pas lié à une question
    /// particulière). Non-NULL pour les selfies du mode vote photo (un
    /// par projet du vote).
    /// </summary>
    [Column("series_project_id")] public string? SeriesProjectId { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("photo_url")] public string PhotoUrl { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
