using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

/// <summary>
/// Une option de réponse pour une QuizQuestion. Position 0-3 = lettre A/B/C/D.
/// is_correct N'EST PAS exposé au client en phase question (ne pas spoiler).
/// La RPC get_quiz_question_for_tv n'inclut pas is_correct dans son retour.
/// is_correct n'est exposé qu'en phase reveal via get_quiz_question_results.
/// </summary>
[Table("quiz_question_options")]
public class QuizQuestionOption : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("question_id")] public string QuestionId { get; set; } = string.Empty;

    /// <summary>0..3 = A/B/C/D.</summary>
    [Column("position")] public int Position { get; set; }

    [Column("option_text")] public string OptionText { get; set; } = string.Empty;

    /// <summary>
    /// True si cette option est une bonne réponse. Plusieurs options peuvent l'être
    /// (cas multi-correct, scoring "tout ou rien").
    /// ⚠️ Cette colonne est sensible : ne JAMAIS la lire directement côté client en
    /// phase question. Utiliser get_quiz_question_for_tv qui ne la renvoie pas.
    /// </summary>
    [Column("is_correct")] public bool IsCorrect { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
