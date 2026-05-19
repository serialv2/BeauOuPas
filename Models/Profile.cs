using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    [Column("banned")]
    public bool Banned { get; set; }

    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("birth_date")]
    public string? BirthDate { get; set; }

    [Column("gender")]
    public string Gender { get; set; } = string.Empty;

    [Column("credits")]
    public int Credits { get; set; } = 0;

    [Column("language")]
    public string Language { get; set; } = "fr";

    [Column("dating_enabled")]
    public bool DatingEnabled { get; set; } = false;

    [Column("dating_preference")]
    public string DatingPreference { get; set; } = "both";

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_banned")]
    public bool IsBanned { get; set; } = false;

    [Column("banned_at")]
    public DateTime? BannedAt { get; set; }

    [Column("banned_reason")]
    public string? BannedReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ✅ Nouvelles colonnes V2
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("last_seen")]
    public DateTime? LastSeen { get; set; }

    [Column("platform")]
    public string Platform { get; set; } = "android";

    [Column("profile_completed")]
    public bool ProfileCompleted { get; set; } = false;

    // Propriété calculée - pas en DB
    [Newtonsoft.Json.JsonIgnore]
    public int Age
    {
        get
        {
            if (string.IsNullOrEmpty(BirthDate)) return 0;
            if (!DateTime.TryParse(BirthDate, out var birth)) return 0;
            var age = DateTime.Today.Year - birth.Year;
            if (birth.Date > DateTime.Today.AddYears(-age)) age--;
            return age;
        }
    }

    // ✅ Statut en ligne
    [Newtonsoft.Json.JsonIgnore]
    public bool IsOnline => LastSeen.HasValue &&
        (DateTime.UtcNow - LastSeen.Value).TotalMinutes < 2;

    [Newtonsoft.Json.JsonIgnore]
    public bool IsRecent => LastSeen.HasValue &&
        (DateTime.UtcNow - LastSeen.Value).TotalHours < 1;

    [Newtonsoft.Json.JsonIgnore]
    public string OnlineStatus
    {
        get
        {
            if (IsOnline) return "online";
            if (IsRecent) return "recent";
            return "offline";
        }
    }
}