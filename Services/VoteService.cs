using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class VoteService
{
    private readonly Supabase.Client _supabase;
    private readonly FriendService _friendService;

    public VoteService(Supabase.Client supabase, FriendService friendService)
    {
        _supabase = supabase;
        _friendService = friendService;
    }

    public async Task<(bool HasVoted, int? Rating)> HasVotedPhotoAsync(string projectId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, null);

            var result = await _supabase
                .From<PhotoVote>()
                .Where(v => v.UserId == userId && v.ProjectId == projectId)
                .Single();

            return result != null ? (true, result.Rating) : (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    public async Task<(bool Success, string Error)> VotePhotoAsync(
        string projectId,
        int rating,
        Profile voterProfile)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté");

            var vote = new PhotoVote
            {
                UserId = userId,
                ProjectId = projectId,
                Rating = rating,
                VoterGender = voterProfile.Gender,
                VoterAge = (short)voterProfile.Age,
                CreatedAt = DateTime.UtcNow
            };

            await _supabase.From<PhotoVote>().Insert(vote);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
                return (false, "Vous avez déjà voté sur ce projet.");

            return (false, ex.Message);
        }
    }

    public async Task<(bool HasVoted, string? Side)> HasVotedDuelAsync(string projectId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, null);

            var result = await _supabase
                .From<DuelVote>()
                .Where(v => v.UserId == userId && v.ProjectId == projectId)
                .Single();

            return result != null ? (true, result.ChosenSide) : (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    public async Task<(bool Success, string Error)> VoteDuelAsync(
        string projectId,
        string side,
        Profile voterProfile)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté");

            var vote = new DuelVote
            {
                UserId = userId,
                ProjectId = projectId,
                ChosenSide = side,
                VoterGender = voterProfile.Gender,
                VoterAge = (short)voterProfile.Age,
                CreatedAt = DateTime.UtcNow
            };

            await _supabase.From<DuelVote>().Insert(vote);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
                return (false, "Vous avez déjà voté sur ce projet.");

            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ProjectId)> RewindLastPhotoVoteAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, null);

            var result = await _supabase
                .From<PhotoVote>()
                .Where(v => v.UserId == userId)
                .Order(v => v.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();

            if (result?.Models == null || result.Models.Count == 0)
                return (false, null);

            var lastVote = result.Models[0];
            var projectId = lastVote.ProjectId;

            await _supabase
                .From<PhotoVote>()
                .Where(v => v.UserId == userId && v.ProjectId == projectId)
                .Delete();

            return (true, projectId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RewindLastPhotoVote error: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<(bool Success, string? ProjectId)> RewindLastDuelVoteAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, null);

            var result = await _supabase
                .From<DuelVote>()
                .Where(v => v.UserId == userId)
                .Order(v => v.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();

            if (result?.Models == null || result.Models.Count == 0)
                return (false, null);

            var lastVote = result.Models[0];
            var projectId = lastVote.ProjectId;

            await _supabase
                .From<DuelVote>()
                .Where(v => v.UserId == userId && v.ProjectId == projectId)
                .Delete();

            return (true, projectId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RewindLastDuelVote error: {ex.Message}");
            return (false, null);
        }
    }

    // ─── Chargement initial des IDs déjà votés (sessions passées) ────
    // Appelé UNE SEULE FOIS dans LoadFeedAsync, avant tout chargement.
    // Résultat injecté dans _shownInSession → plus de race condition.
    public async Task<HashSet<string>> GetAllVotedProjectIdsAsync()
    {
        var result = new HashSet<string>();
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return result;

            var photoTask = _supabase
                .From<PhotoVote>()
                .Where(v => v.UserId == userId)
                .Get();

            var duelTask = _supabase
                .From<DuelVote>()
                .Where(v => v.UserId == userId)
                .Get();

            // Identique au site web : on exclut aussi les sondages deja votes
            var pollTask = _supabase
                .From<PollVote>()
                .Where(v => v.UserId == userId)
                .Get();

            await Task.WhenAll(photoTask, duelTask, pollTask);

            foreach (var v in photoTask.Result.Models)
                if (!string.IsNullOrWhiteSpace(v.ProjectId))
                    result.Add(v.ProjectId);

            foreach (var v in duelTask.Result.Models)
                if (!string.IsNullOrWhiteSpace(v.ProjectId))
                    result.Add(v.ProjectId);

            foreach (var v in pollTask.Result.Models)
                if (!string.IsNullOrWhiteSpace(v.ProjectId))
                    result.Add(v.ProjectId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAllVotedProjectIds error: {ex.Message}");
        }
        return result;
    }

    public async Task<List<Project>> GetFeedProjectsPageAsync(
        Profile currentUser,
        int pageSize = 10,
        int offset = 0,
        string? seed = null)
    {
        try
        {
            var currentUserId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(currentUserId))
                return new List<Project>();

            seed ??= DateTime.UtcNow.Ticks.ToString();

            var parameters = new Dictionary<string, object>
            {
                { "p_user_id", currentUserId },
                { "p_limit", pageSize },
                { "p_offset", offset },
                { "p_seed", seed }
            };

            var result = await _supabase.Rpc<List<Project>>(
                "get_feed_projects",
                parameters);

            return result ?? new List<Project>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFeedProjectsPageAsync error: {ex.Message}");
            return new List<Project>();
        }
    }

    public async Task<List<Project>> GetUnvotedProjectsAsync(
        Profile currentUser,
        int pageSize = 10,
        List<string>? excludeIds = null)
    {
        try
        {
            var currentUserId = _supabase.Auth.CurrentUser?.Id ?? string.Empty;

            // excludeIds contient :
            // - les IDs déjà votés toutes sessions (chargés une seule fois au
            //   démarrage via GetAllVotedProjectIdsAsync dans LoadFeedAsync)
            // - les projets déjà montrés dans cette session (_shownInSession)
            // Aucune requête base ici → zéro race condition.
            var excludedIds = new HashSet<string>(excludeIds ?? new List<string>());

            var fetchLimit = Math.Max(pageSize * 5, 30);

            var projectsResult = await _supabase
                .From<Project>()
                .Where(p => p.Status == "approved" && p.IsOpen == true)
                .Order("created_at", Postgrest.Constants.Ordering.Descending)
                .Limit(fetchLimit)
                .Get();

            var filtered = projectsResult.Models
                .Where(p => p.Status == "approved" && p.IsOpen)
                .Where(p => p.OwnerId != currentUserId)
                .Where(p => !excludedIds.Contains(p.Id))
                .ToList();

            if (currentUser.Gender == "male" || currentUser.Gender == "female")
            {
                filtered = filtered
                    .Where(p => p.GenderFilter == "both" || p.GenderFilter == currentUser.Gender)
                    .ToList();
            }

            if (currentUser.Age > 0)
            {
                filtered = filtered
                    .Where(p => currentUser.Age >= p.MinAge && currentUser.Age <= p.MaxAge)
                    .ToList();
            }

            var publicProjects = filtered
                .Where(p => !p.IsPrivate)
                .ToList();

            var privateProjects = filtered
                .Where(p => p.IsPrivate)
                .ToList();

            if (privateProjects.Count > 0)
            {
                var friends = await _friendService.GetFriendsAsync();

                var friendIds = friends
                    .Where(f => f.IsAccepted)
                    .Select(f => f.UserId)
                    .ToHashSet();

                var allowedPrivateProjects = privateProjects
                    .Where(p => friendIds.Contains(p.OwnerId))
                    .ToList();

                filtered = publicProjects
                    .Concat(allowedPrivateProjects)
                    .ToList();
            }
            else
            {
                filtered = publicProjects;
            }

            System.Diagnostics.Debug.WriteLine(
                $"Feed projets reçus={projectsResult.Models.Count}, visibles={filtered.Count}, retournés={Math.Min(pageSize, filtered.Count)}");

            return filtered
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Feed error: {ex.Message}");
            return new List<Project>();
        }
    }
}