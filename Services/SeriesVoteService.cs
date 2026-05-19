using BeauOuPas.Models;
using Postgrest;

namespace BeauOuPas.Services;

public class SeriesVoteService
{
    private readonly Supabase.Client _supabase;
    private readonly PhotoUploadService _uploadService;

    public SeriesVoteService(Supabase.Client supabase, PhotoUploadService uploadService)
    {
        _supabase = supabase;
        _uploadService = uploadService;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ─── Rejoindre une série ──────────────────────────────────────
    public async Task<bool> JoinSeriesAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var existing = await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == userId)
                .Get();
            if (existing.Models.Any()) return true;

            var sp = new SeriesParticipant
            {
                SeriesId = seriesId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            await _supabase.From<SeriesParticipant>().Insert(sp);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VoteService] JoinSeries: {ex.Message}");
            return false;
        }
    }
    // ─── Quitter une série (DELETE de series_participants) ──────────
    /// <summary>
    /// ⚡ NOUVEAU : retire le current user de la liste des participants.
    /// Appelé quand l'utilisateur confirme "Quitter la série" depuis le bouton Retour.
    /// Idempotent : si l'utilisateur n'est pas dans la liste, ne fait rien.
    /// </summary>
    public async Task<bool> LeaveSeriesAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == userId)
                .Delete();

            System.Diagnostics.Debug.WriteLine(
                $"[VoteService] User {userId} a quitté la série {seriesId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VoteService] LeaveSeries: {ex.Message}");
            return false;
        }
    }
    public async Task<int> GetParticipantCountAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId).Get();
            return result.Models.Count;
        }
        catch { return 0; }
    }

    public async Task<int> GetVoteCountAsync(string seriesProjectId)
    {
        try
        {
            var result = await _supabase.From<SeriesVote>()
                .Where(v => v.SeriesProjectId == seriesProjectId).Get();
            return result.Models.Count;
        }
        catch { return 0; }
    }

    public async Task<bool> HasUserVotedAsync(string seriesProjectId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var existing = await _supabase.From<SeriesVote>()
                .Where(v => v.SeriesProjectId == seriesProjectId && v.VoterId == userId)
                .Get();
            return existing.Models.Any();
        }
        catch { return false; }
    }

    // ─── Voter ────────────────────────────────────────────────────
    // Retourne (Success, ErrorMessage) pour permettre à l'UI d'afficher l'erreur si fail.
    public async Task<(bool Success, string Error)> VoteAsync(
        string seriesId, string seriesProjectId, string voteValue)
    {
        var userId = CurrentUserId;
        if (userId == null)
            return (false, "Utilisateur non authentifié");

        System.Diagnostics.Debug.WriteLine(
            $"[VoteService] Vote attempt: seriesId={seriesId}, sp={seriesProjectId}, value={voteValue}, user={userId}");

        // ⚠️ NE PAS poser Id = string.Empty : Supabase doit générer le UUID lui-même.
        // Le BaseModel sans Id explicite laisse PostgREST utiliser le default DB.
        var vote = new SeriesVote
        {
            SeriesId = seriesId,
            SeriesProjectId = seriesProjectId,
            VoterId = userId,
            VoteValue = voteValue,
            CreatedAt = DateTime.UtcNow
        };

        // Tentative 1 : upsert atomique (évite doublon si index unique présent)
        try
        {
            var result = await _supabase.From<SeriesVote>()
                .Upsert(vote, new QueryOptions { OnConflict = "series_project_id,voter_id" });

            System.Diagnostics.Debug.WriteLine(
                $"[VoteService] Upsert OK, returned {result.Models.Count} model(s)");
            return (true, string.Empty);
        }
        catch (Exception exUpsert)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[VoteService] Upsert failed: {exUpsert.GetType().Name}: {exUpsert.Message}");

            // Tentative 2 : check-then-insert
            try
            {
                var existing = await _supabase.From<SeriesVote>()
                    .Where(v => v.SeriesProjectId == seriesProjectId && v.VoterId == userId)
                    .Get();

                if (existing.Models.Any())
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[VoteService] Vote already exists, treating as success");
                    return (true, string.Empty);
                }

                var inserted = await _supabase.From<SeriesVote>().Insert(vote);
                System.Diagnostics.Debug.WriteLine(
                    $"[VoteService] Insert fallback OK, {inserted.Models.Count} model(s)");
                return (true, string.Empty);
            }
            catch (Exception exInsert)
            {
                var msg = $"{exInsert.GetType().Name}: {exInsert.Message}";
                System.Diagnostics.Debug.WriteLine($"[VoteService] Insert FAILED: {msg}");
                return (false, msg);
            }
        }
    }

    // ─── Upload selfie (rapide, un seul fichier compressé) ────────
    // ─── Upload selfie (rapide, un seul fichier compressé) ────────
    // ─── Upload selfie (rapide, un seul fichier compressé) ────────
    public async Task<bool> UploadSelfieAsync(
        string seriesId, string seriesProjectId, Stream photoStream, string fileName)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var url = await _uploadService.UploadSelfieFastAsync(
                photoStream, seriesId, seriesProjectId, userId);
            if (string.IsNullOrEmpty(url)) return false;

            var selfie = new SeriesSelfie
            {
                SeriesId = seriesId,
                SeriesProjectId = seriesProjectId,
                UserId = userId,
                PhotoUrl = url,
                CreatedAt = DateTime.UtcNow
            };
            await _supabase.From<SeriesSelfie>().Insert(selfie);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VoteService] UploadSelfie: {ex.Message}");
            return false;
        }
    }
    // ─── Résultats d'un projet ────────────────────────────────────
    public async Task<SeriesProjectResults> GetProjectResultsAsync(
        string seriesProjectId, string projectType)
    {
        try
        {
            var votes = await _supabase.From<SeriesVote>()
                .Where(v => v.SeriesProjectId == seriesProjectId).Get();

            var selfies = await _supabase.From<SeriesSelfie>()
                .Where(s => s.SeriesProjectId == seriesProjectId).Get();

            var results = new SeriesProjectResults
            {
                TotalVotes = votes.Models.Count,
                SelfieUrls = selfies.Models.Select(s => s.PhotoUrl).ToList()
            };

            System.Diagnostics.Debug.WriteLine(
                $"[VoteService] GetResults sp={seriesProjectId}: " +
                $"{votes.Models.Count} votes, values=[{string.Join(",", votes.Models.Select(v => v.VoteValue))}]");

            if (projectType == "photo_vote")
            {
                results.Likes = votes.Models.Count(v => v.VoteValue == "like");
                results.Mehs = votes.Models.Count(v => v.VoteValue == "meh");
                results.Dislikes = votes.Models.Count(v => v.VoteValue == "dislike");
            }
            else if (projectType == "duel")
            {
                results.VotesA = votes.Models.Count(v => v.VoteValue == "A");
                results.VotesB = votes.Models.Count(v => v.VoteValue == "B");
            }
            else if (projectType == "poll")
            {
                results.PollResults = votes.Models
                    .GroupBy(v => v.VoteValue)
                    .ToDictionary(g => g.Key, g => g.Count());
            }

            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VoteService] GetResults: {ex.Message}");
            return new();
        }
    }

    public async Task<List<SeriesVote>> GetAllVotesAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<SeriesVote>()
                .Where(v => v.SeriesId == seriesId).Get();
            return result.Models;
        }
        catch { return new(); }
    }

    public async Task<List<SeriesSelfie>> GetAllSelfiesAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<SeriesSelfie>()
                .Where(s => s.SeriesId == seriesId).Get();
            return result.Models;
        }
        catch { return new(); }
    }
}

public class SeriesProjectResults
{
    public int TotalVotes { get; set; }
    public int Likes { get; set; }
    public int Mehs { get; set; }
    public int Dislikes { get; set; }
    public int VotesA { get; set; }
    public int VotesB { get; set; }
    public Dictionary<string, int> PollResults { get; set; } = new();
    public List<string> SelfieUrls { get; set; } = new();
}