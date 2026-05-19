using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

/// <summary>
/// Modèle Postgrest pour la table public.party_answers (mode Partie Rapide).
///
/// Conventions projet : [Table], [Column], [PrimaryKey("id", false)],
/// [JsonIgnore] pour les champs non envoyés à l'insert.
///
/// Table créée par le BLOC A (déjà appliqué). Rappel structure :
///   id, series_id, question_id, voter_id, target_user_id, created_at
///   + UNIQUE(series_id, question_id, voter_id)
/// </summary>
[Table("party_answers")]
public class PartyAnswer : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("series_id")]
    public string SeriesId { get; set; } = string.Empty;

    [Column("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [Column("voter_id")]
    public string VoterId { get; set; } = string.Empty;

    [Column("target_user_id")]
    public string TargetUserId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
