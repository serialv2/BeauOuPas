using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

/// <summary>
/// ⚡ LOT 1 : Modèle léger pour lire la table quiz_questions via PostgREST.
///
/// On ne charge PAS les options ni les réponses ici (juste l'aperçu pour
/// l'affichage en lecture seule dans SeriesDetailPage). Pour l'édition,
/// la page CreateQuizPage charge les options séparément.
///
/// Schéma DB confirmé :
///  - id            uuid PK (gen_random_uuid)
///  - series_id     uuid FK series.id
///  - position      int  (0-indexé, unique par série)
///  - title         text
///  - question_text text
///  - photo_url     text NULLABLE
///  - started_at    timestamptz NULLABLE
///  - created_at    timestamptz default now()
///  - updated_at    timestamptz default now()
/// </summary>
[Table("quiz_questions")]
public class QuizQuestionRow : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("series_id")]
    public string SeriesId { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("photo_url")]
    public string? PhotoUrl { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
