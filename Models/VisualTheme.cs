using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("visual_themes")]
public class VisualTheme : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("category")] public string? Category { get; set; }
    [Column("preview_url")] public string? PreviewUrl { get; set; }
    [Column("css_class")] public string? CssClass { get; set; }
    [Column("is_premium")] public bool IsPremium { get; set; } = false;
    [Column("credit_cost")] public int CreditCost { get; set; } = 0;
    [Column("is_active")] public bool IsActive { get; set; } = true;
}
