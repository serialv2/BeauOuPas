using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("reports")]
public class Report : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("reporter_id")]
    public string ReporterId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
