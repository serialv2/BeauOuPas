using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_selfies")]
public class SeriesSelfie : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    /// <summary>
    /// NULL pour les selfies de session quiz/party (pas lié à une question
    /// particulière). Non-NULL pour les selfies du mode vote photo (un
    /// par projet du vote).
    /// </summary>
    [Column("series_project_id")] public string? SeriesProjectId { get; set; }
    /// <summary>
    /// NULL pour les selfies du mode vote photo (pas de notion de question).
    /// Renseigné pour les selfies de session quiz/party : pointe sur la
    /// quiz_questions.id pendant laquelle le selfie a été pris. Permet à
    /// la TV de filtrer pour n'afficher que les selfies de la question
    /// courante pendant sa phase de révélation.
    /// </summary>
    [Column("series_question_id")] public string? SeriesQuestionId { get; set; }
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("photo_url")] public string PhotoUrl { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
