using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace BeauOuPas.Models;

[Table("app_settings")]
public class AppSetting : BaseModel
{
    [PrimaryKey("key", false)]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}