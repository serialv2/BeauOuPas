using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("groups")]
public class Group : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("avatar_url")] public string? AvatarUrl { get; set; }
    [Column("owner_id")] public string OwnerId { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
