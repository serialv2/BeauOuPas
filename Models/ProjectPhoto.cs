using Postgrest.Attributes;
using Postgrest.Models;
namespace BeauOuPas.Models;

[Table("project_photos")]
public class ProjectPhoto : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("url")]
    public string Url { get; set; } = string.Empty;

    [Column("side")]
    public string Side { get; set; } = "single"; // "single","left","right"

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }
}
