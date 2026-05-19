using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("group_messages")]
public class GroupMessage : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("group_id")] public string GroupId { get; set; } = string.Empty;
    [Column("sender_id")] public string SenderId { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    // ⚡ FIX: propriété calculée côté client uniquement.
    // Postgrest C# utilise Newtonsoft.Json en interne, donc il faut
    // [Newtonsoft.Json.JsonIgnore] et NON [System.Text.Json.Serialization.JsonIgnore].
    // Sans ça → PGRST204 "Could not find the 'IsMyMessage' column..."
    [Newtonsoft.Json.JsonIgnore]
    public bool IsMyMessage { get; set; }
}