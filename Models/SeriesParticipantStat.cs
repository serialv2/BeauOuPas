namespace BeauOuPas.Models;

/// <summary>
/// Statistiques d'un participant pour la salle d'attente (lobby) d'une série.
/// Pas un modèle Supabase : c'est un DTO calculé côté C# par
/// SeriesService.GetParticipantsWithStatsAsync.
/// </summary>
public class SeriesParticipantStat
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int ProjectCount { get; set; }

    public string ProjectCountLabel => ProjectCount == 1
        ? "1 projet"
        : $"{ProjectCount} projets";
}
