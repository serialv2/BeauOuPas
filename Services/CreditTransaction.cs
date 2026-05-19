
using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

namespace BeauOuPas.Models;

[Table("credit_transactions")]
public class CreditTransaction : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("amount")]
    public int Amount { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // ─── Propriétés calculées - pas en DB ────────────────────────────
    [JsonIgnore]
    public bool IsGain => Amount > 0;

    [JsonIgnore]
    public string AmountLabel => IsGain
        ? $"+{Amount} crédit{(Amount > 1 ? "s" : "")}"
        : $"{Amount} crédit{(Math.Abs(Amount) > 1 ? "s" : "")}";

    // ⚡ FIX: ajout des cas manquants. Avant, "series_project" tombait dans le
    // default `_ => Type` et s'affichait brut dans la page Mes Crédits.
    // Note: les libellés sont aujourd'hui hardcodés en français (cohérent avec
    // l'existant); une refonte i18n via converter pourra venir plus tard.
    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        "welcome" => "🎁 Bienvenue",
        "vote" => "🗳️ Vote",
        "ad_banner" => "📢 Pub bannière",
        "ad_reward" => "🎬 Pub rewarded",
        "project_submit" => "📷 Projet soumis",
        "project_boost" => "🚀 Boost projet",
        "meet" => "💘 Rencontre",
        "rewind" => "↩️ Rewind",
        "series_project" => "🎬 Projet en série",
        "friend_invite" => "👥 Ami invité",
        _ => Type
    };
}
