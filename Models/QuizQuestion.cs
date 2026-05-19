using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

/// <summary>
/// Une question d'un quizz. L'équivalent de SeriesProject pour le mode quiz.
/// La position est 0-indexed et correspond à current_project_index dans series.
/// started_at est posé par la TV au moment où la question commence (timer démarre).
/// </summary>
[Table("quiz_questions")]
public class QuizQuestion : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    [Column("position")] public int Position { get; set; }

    /// <summary>Petit label affiché au-dessus de la question (ex: "Géographie").</summary>
    [Column("title")] public string? Title { get; set; }

    /// <summary>Énoncé de la question, affiché en grand.</summary>
    [Column("question_text")] public string QuestionText { get; set; } = string.Empty;

    /// <summary>URL d'une image illustrative optionnelle (bucket quiz_photos).</summary>
    [Column("photo_url")] public string? PhotoUrl { get; set; }

    /// <summary>
    /// Posé par la TV quand le timer démarre. Tant que NULL, le joueur attend.
    /// La logique de countdown est calée sur ce timestamp + vote_duration_seconds.
    /// </summary>
    [Column("started_at")] public DateTime? StartedAt { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
