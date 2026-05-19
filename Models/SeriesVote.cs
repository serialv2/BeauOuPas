using Postgrest.Attributes;
using Postgrest.Models;

namespace BeauOuPas.Models;

[Table("series_votes")]
public class SeriesVote : BaseModel
{
    // Id : laisser null pour les inserts, Supabase génère le UUID via DEFAULT gen_random_uuid()
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("series_id")] public string SeriesId { get; set; } = string.Empty;
    [Column("series_project_id")] public string SeriesProjectId { get; set; } = string.Empty;
    [Column("voter_id")] public string VoterId { get; set; } = string.Empty;
    [Column("vote_value")] public string VoteValue { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}