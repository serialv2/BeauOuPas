using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("project_templates")]
public class ProjectTemplate : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("category")] public string Category { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("type")] public string Type { get; set; } = "poll";
    [Column("options")] public string? Options { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
