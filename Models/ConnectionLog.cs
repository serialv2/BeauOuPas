using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("connection_logs")]
public class ConnectionLog : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("platform")]
    public string Platform { get; set; } = "android";

    [Column("connected_at")]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}
