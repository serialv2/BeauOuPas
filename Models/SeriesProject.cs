using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_projects")]
public class SeriesProject : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    [Column("project_id")] public string ProjectId { get; set; } = string.Empty;
    [Column("added_by")] public string AddedBy { get; set; } = string.Empty;
    [Column("is_revealed")] public bool IsRevealed { get; set; } = false;
    [Column("position")] public int Position { get; set; } = 0;
    [Column("selfie_enabled")] public bool SelfieEnabled { get; set; } = true;
    [Column("theme_id")] public string? ThemeId { get; set; }
    [Column("added_at")] public DateTime AddedAt { get; set; }
    [Column("started_at")] public DateTime? StartedAt { get; set; }
}
