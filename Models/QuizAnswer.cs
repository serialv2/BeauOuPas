using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

/// <summary>
/// La réponse d'un joueur à une question. UNIQUE (question_id, user_id) en BDD.
/// On insère via la RPC submit_quiz_answer (anti-triche, scoring server-side),
/// pas via INSERT direct.
/// </summary>
[Table("quiz_answers")]
public class QuizAnswer : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("question_id")] public string QuestionId { get; set; } = string.Empty;
    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    [Column("user_id")] public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tableau des UUIDs des options cochées (peut être vide = "aucune case").
    /// Stocké en UUID[] côté Postgres.
    /// </summary>
    [Column("selected_option_ids")] public List<string> SelectedOptionIds { get; set; } = new();

    /// <summary>
    /// Temps de réponse en millisecondes depuis started_at de la question.
    /// Sert au tie-breaker du leaderboard final.
    /// </summary>
    [Column("response_time_ms")] public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Évalué côté serveur dans submit_quiz_answer ("tout ou rien" : toutes
    /// les bonnes cochées ET aucune mauvaise = true).
    /// </summary>
    [Column("is_correct")] public bool IsCorrect { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
