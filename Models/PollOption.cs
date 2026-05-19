using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("poll_options")]
public class PollOption : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("text")]
    public string Text { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Propriétés UI (non DB)
    [Newtonsoft.Json.JsonIgnore]
    public int VoteCount { get; set; } = 0;

    [Newtonsoft.Json.JsonIgnore]
    public double Percentage { get; set; } = 0;

    [Newtonsoft.Json.JsonIgnore]
    public bool IsSelected { get; set; } = false;
}
