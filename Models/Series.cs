using Postgrest.Attributes;
using Postgrest.Models;
using Newtonsoft.Json;
namespace BeauOuPas.Models;

[Table("series")]
public class Series : BaseModel
{
    [PrimaryKey("id")] public string Id { get; set; } = string.Empty;
    [Column("group_id")] public string? GroupId { get; set; }
    [Column("creator_id")] public string CreatorId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("max_projects")] public int MaxProjects { get; set; } = 10;
    [Column("max_per_member")] public int MaxPerMember { get; set; } = 2;
    [Column("status")] public string Status { get; set; } = "preparing";
    [Column("current_project_index")] public int CurrentProjectIndex { get; set; } = 0;
    [Column("tv_session_id")] public string? TvSessionId { get; set; }
    [Column("is_standalone")] public bool IsStandalone { get; set; } = false;
    [Column("access_code")] public string? AccessCode { get; set; }
    [Column("max_participants")] public int? MaxParticipants { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// ⚡ NOUVEAU : true = projets cachés (effet surprise) jusqu'au démarrage.
    /// false = projets visibles par tous les membres dans la salle d'attente.
    /// null = série créée avant l'introduction de la fonctionnalité (legacy)
    /// → on traite comme "cachés" par sécurité.
    /// </summary>
    [Column("projects_hidden")] public bool? ProjectsHidden { get; set; }

    // ─────────────────────────────────────────────────────────────
    // ⚡ Lot Mode TV
    // ─────────────────────────────────────────────────────────────

    /// <summary>True quand l'animateur a activé le mode TV.</summary>
    [Column("tv_active")] public bool TvActive { get; set; } = false;

    /// <summary>True si l'animateur a mis la session en pause (overlay sur la TV).</summary>
    [Column("tv_paused")] public bool TvPaused { get; set; } = false;

    /// <summary>Timestamp de démarrage du mode TV (pour stats).</summary>
    [Column("tv_started_at")] public DateTime? TvStartedAt { get; set; }

    /// <summary>
    /// True : l'animateur participe au vote (apparaît dans le compteur "X / Y ont voté").
    /// False : il est juste animateur, ne vote pas.
    /// </summary>
    [Column("animator_votes")] public bool AnimatorVotes { get; set; } = true;

    /// <summary>
    /// ⚡ NOUVEAU : true = la TV affiche la phase "Statistiques détaillées" (par genre / âge)
    /// après chaque projet pendant la phase reveal.
    /// false = la TV saute cette phase et passe directement au projet suivant.
    /// Default TRUE en BDD → comportement actuel préservé pour toutes les séries existantes.
    /// </summary>
    [Column("show_stats")] public bool ShowStats { get; set; } = true;

    /// <summary>
    /// ⚡ MAI 2026 — Si true, les participants invités peuvent ajouter
    /// leurs propres projets à la série. Si false, seul le créateur peut.
    /// Toujours true historiquement (les séries pré-existantes restent
    /// en mode collaboratif via DEFAULT en base).
    /// </summary>
    [Column("members_can_add_projects")] public bool MembersCanAddProjects { get; set; } = true;

    /// <summary>
    /// ⚡ NOUVEAU : durée d'une phase de vote / question en secondes (5..60).
    /// Côté quiz, sert aussi de timer par question.
    /// Default 20 en BDD pour conserver le comportement actuel.
    /// </summary>
    [Column("vote_duration_seconds")] public int VoteDurationSeconds { get; set; } = 20;

    // ─────────────────────────────────────────────────────────────
    // ⚡ Lot Mode QUIZZ
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// True = série quizz (questions/réponses, scoring), False/null = série de votes classique.
    /// Détermine quelle page TV charger (tv-quiz.html vs tv-display.html) et quelle page Maui
    /// (QuizPlayPage vs SeriesVotePage) le joueur reçoit lors du Join.
    /// </summary>
    [Column("is_quiz")] public bool IsQuiz { get; set; } = false;

    /// <summary>
    /// Style visuel TV pour le quiz : "kahoot" | "millionaire" | "burger" | "weakest".
    /// NULL pour les séries de vote classiques. Défini par l'animateur à la création.
    /// </summary>
    [Column("quiz_style")] public string? QuizStyle { get; set; }

    /// <summary>
    /// True = la TV affiche le classement complet en fin de quiz (après le podium top 3).
    /// False = on s'arrête au podium. Default TRUE.
    /// </summary>
    [Column("show_full_leaderboard")] public bool ShowFullLeaderboard { get; set; } = true;

    /// <summary>
    /// Durée en secondes de la phase intro avant la 1ère question (5..60).
    /// Default 20 en BDD.
    /// </summary>
    [Column("intro_duration_seconds")] public int IntroDurationSeconds { get; set; } = 20;

    // ─────────────────────────────────────────────────────────────
    // ⚡ Lieu et horodatage de mise en play / fin
    // Tout est NULLABLE : aucun plantage possible si pas dispo.
    // ─────────────────────────────────────────────────────────────

    /// <summary>Ville où la série/quiz a été démarrée (NULL si permission refusée).</summary>
    [Column("play_city")] public string? PlayCity { get; set; }

    /// <summary>Pays où la série/quiz a été démarrée.</summary>
    [Column("play_country")] public string? PlayCountry { get; set; }

    /// <summary>Latitude au moment du play (NULL si pas de GPS).</summary>
    [Column("play_lat")] public double? PlayLat { get; set; }

    /// <summary>Longitude au moment du play.</summary>
    [Column("play_lng")] public double? PlayLng { get; set; }

    /// <summary>Horodatage de la mise en play.</summary>
    [Column("started_at")] public DateTime? StartedAt { get; set; }

    /// <summary>
    /// ⚡ NOUVEAU : horodatage de fin de série (StopSeriesAsync).
    /// NULL tant que la série n'est pas terminée. Sert à calculer la durée du jeu.
    /// </summary>
    [Column("ended_at")] public DateTime? EndedAt { get; set; }
}
