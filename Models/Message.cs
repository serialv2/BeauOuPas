using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BeauOuPas.Models;

[Table("messages")]
public class Message : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("chat_id")]
    public string ChatId { get; set; } = string.Empty;

    [Column("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("is_reported")]
    public bool IsReported { get; set; } = false;

    [Column("reported_by")]
    public string? ReportedBy { get; set; }

    [Column("reported_at")]
    public DateTime? ReportedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // ─── Propriétés calculées - pas en DB ────────────────────────────
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsMyMessage { get; set; } = false;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeLabel =>
        CreatedAt.ToString("HH:mm");
}