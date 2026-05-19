using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("projects")]
public class Project : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("gender_filter")]
    public string GenderFilter { get; set; } = "both";

    [Column("min_age")]
    public int MinAge { get; set; } = 13;

    [Column("max_age")]
    public int MaxAge { get; set; } = 99;

    [Column("is_open")]
    public bool IsOpen { get; set; } = true;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("moderated_at")]
    public DateTime? ModeratedAt { get; set; }

    [Column("reject_reason")]
    public string? RejectReason { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("moderation_ip")]
    public string? ModerationIp { get; set; }

    [Column("moderation_latitude")]
    public double? ModerationLatitude { get; set; }

    [Column("moderation_longitude")]
    public double? ModerationLongitude { get; set; }

    [Column("moderation_city")]
    public string? ModerationCity { get; set; }

    [Column("moderation_country")]
    public string? ModerationCountry { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("closes_at")]
    public DateTime? ClosesAt { get; set; }

    [Column("is_private")]
    public bool IsPrivate { get; set; } = false;
    [Column("is_series_project")]
    public bool IsSeriesProject { get; set; } = false;
}