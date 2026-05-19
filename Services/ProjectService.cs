using BeauOuPas.Models;
using BeauOuPas.Models.Stats;

namespace BeauOuPas.Services;

public class ProjectService
{
    private readonly Supabase.Client _supabase;
    private readonly PhotoUploadService _uploadService;
    private readonly NetworkService _networkService;
    private readonly LocationService _locationService;

    public ProjectService(
        Supabase.Client supabase,
        PhotoUploadService uploadService,
        NetworkService networkService,
        LocationService locationService)
    {
        _supabase = supabase;
        _uploadService = uploadService;
        _networkService = networkService;
        _locationService = locationService;
    }

    // ─── Créer un projet photo ───────────────────────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreatePhotoProjectAsync(
            string title, string? description, string genderFilter,
            int minAge, int maxAge, Stream photoStream, string fileName,
            DateTime? closesAt = null, bool isPrivate = false)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté", string.Empty);

            var ipAddress = await _networkService.GetPublicIpAsync();
            var (lat, lng, city, country) = await _locationService.GetLocationAsync();
            var (displayUrl, _) = await _uploadService.UploadPhotoAsync(photoStream, fileName);

            var projectId = await _supabase.Rpc<string>("insert_project",
                new Dictionary<string, object>
                {
                    { "p_owner_id",   userId },          { "p_type",        "photo_vote" },
                    { "p_title",      title },            { "p_description", description ?? "" },
                    { "p_gender_filter", genderFilter },  { "p_min_age",     minAge },
                    { "p_max_age",    maxAge },
                    { "p_closes_at",  closesAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "" },
                    { "p_ip_address", ipAddress ?? "" },
                    { "p_latitude",   lat.HasValue ? (object)lat.Value : DBNull.Value },
                    { "p_longitude",  lng.HasValue ? (object)lng.Value : DBNull.Value },
                    { "p_city",       city ?? "" },       { "p_country",     country ?? "" },
                    { "p_is_private", isPrivate }
                });

            if (string.IsNullOrEmpty(projectId))
                return (false, "Erreur création projet", string.Empty);

            await _supabase.Rpc("insert_project_photo",
                new Dictionary<string, object>
                {
                    { "p_project_id", projectId },
                    { "p_url",        displayUrl },
                    { "p_side",       "single" }
                });

            return (true, string.Empty, projectId);
        }
        catch (Exception ex) { return (false, ex.Message, string.Empty); }
    }

    // ─── Créer un projet duel ────────────────────────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreateDuelProjectAsync(
            string title, string? description, string genderFilter,
            int minAge, int maxAge,
            Stream photoLeftStream, string fileNameLeft,
            Stream photoRightStream, string fileNameRight,
            DateTime? closesAt = null, bool isPrivate = false)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté", string.Empty);

            var ipAddress = await _networkService.GetPublicIpAsync();
            var (lat, lng, city, country) = await _locationService.GetLocationAsync();
            var (displayUrlLeft, _)  = await _uploadService.UploadPhotoAsync(photoLeftStream, fileNameLeft);
            var (displayUrlRight, _) = await _uploadService.UploadPhotoAsync(photoRightStream, fileNameRight);

            var projectId = await _supabase.Rpc<string>("insert_project",
                new Dictionary<string, object>
                {
                    { "p_owner_id",   userId },       { "p_type",        "duel" },
                    { "p_title",      title },         { "p_description", description ?? "" },
                    { "p_gender_filter", genderFilter }, { "p_min_age",   minAge },
                    { "p_max_age",    maxAge },
                    { "p_closes_at",  closesAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "" },
                    { "p_ip_address", ipAddress ?? "" },
                    { "p_latitude",   lat.HasValue ? (object)lat.Value : DBNull.Value },
                    { "p_longitude",  lng.HasValue ? (object)lng.Value : DBNull.Value },
                    { "p_city",       city ?? "" },    { "p_country",     country ?? "" },
                    { "p_is_private", isPrivate }
                });

            if (string.IsNullOrEmpty(projectId))
                return (false, "Erreur création projet", string.Empty);

            await _supabase.Rpc("insert_project_photo",
                new Dictionary<string, object>
                { { "p_project_id", projectId }, { "p_url", displayUrlLeft }, { "p_side", "left" } });

            await _supabase.Rpc("insert_project_photo",
                new Dictionary<string, object>
                { { "p_project_id", projectId }, { "p_url", displayUrlRight }, { "p_side", "right" } });

            return (true, string.Empty, projectId);
        }
        catch (Exception ex) { return (false, ex.Message, string.Empty); }
    }

    // ─── Créer un sondage ────────────────────────────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreatePollProjectAsync(
            string title, string? description, string genderFilter,
            int minAge, int maxAge, List<string> options,
            DateTime? closesAt = null, bool isPrivate = false)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté", string.Empty);

            var ipAddress = await _networkService.GetPublicIpAsync();
            var (lat, lng, city, country) = await _locationService.GetLocationAsync();

            var projectId = await _supabase.Rpc<string>("insert_project",
                new Dictionary<string, object>
                {
                    { "p_owner_id",   userId },       { "p_type",        "poll" },
                    { "p_title",      title },         { "p_description", description ?? "" },
                    { "p_gender_filter", genderFilter }, { "p_min_age",   minAge },
                    { "p_max_age",    maxAge },
                    { "p_closes_at",  closesAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "" },
                    { "p_ip_address", ipAddress ?? "" },
                    { "p_latitude",   lat.HasValue ? (object)lat.Value : DBNull.Value },
                    { "p_longitude",  lng.HasValue ? (object)lng.Value : DBNull.Value },
                    { "p_city",       city ?? "" },    { "p_country",     country ?? "" },
                    { "p_is_private", isPrivate }
                });

            if (string.IsNullOrEmpty(projectId))
                return (false, "Erreur création sondage", string.Empty);

            // Insérer les options
            for (int i = 0; i < options.Count; i++)
            {
                await _supabase.From<PollOption>().Insert(new PollOption
                {
                    ProjectId = projectId,
                    Text      = options[i],
                    Position  = i
                });
            }

            return (true, string.Empty, projectId);
        }
        catch (Exception ex) { return (false, ex.Message, string.Empty); }
    }

    // ─── Récupérer un projet par ID ─────────────────────────────────
    public async Task<Project?> GetProjectByIdAsync(string projectId)
    {
        try { return await _supabase.From<Project>().Where(p => p.Id == projectId).Single(); }
        catch { return null; }
    }

    // ─── Créer photo pour série (sans modération) ────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreateSeriesPhotoProjectAsync(string title, string? description, Stream photoStream, string fileName)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[CreatePhoto-DIAG] ENTRÉE: title={title}, fileName={fileName}");
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null)
            {
                System.Diagnostics.Debug.WriteLine("[CreatePhoto-DIAG] userId NULL");
                return (false, "Non connecté", string.Empty);
            }

            System.Diagnostics.Debug.WriteLine("[CreatePhoto-DIAG] AVANT UploadPhotoAsync");
            var (displayUrl, _) = await _uploadService.UploadPhotoAsync(photoStream, fileName);
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePhoto-DIAG] APRÈS UploadPhotoAsync: url={displayUrl?.Substring(0, Math.Min(50, displayUrl?.Length ?? 0))}");

            var project = new Project
            {
                OwnerId = userId, Title = title, Description = description,
                Type = "photo_vote", Status = "approved", IsOpen = true,
                GenderFilter = "both", MinAge = 13, MaxAge = 99,
                CreatedAt = DateTime.UtcNow
            };

            System.Diagnostics.Debug.WriteLine("[CreatePhoto-DIAG] AVANT INSERT Project");
            var result = await _supabase.From<Project>().Insert(project);
            var created = result.Models.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePhoto-DIAG] APRÈS INSERT Project: created={(created == null ? "NULL" : created.Id)}");

            if (created == null) return (false, "Erreur création", string.Empty);

            System.Diagnostics.Debug.WriteLine("[CreatePhoto-DIAG] AVANT RPC insert_project_photo");
            await _supabase.Rpc("insert_project_photo", new Dictionary<string, object>
            { { "p_project_id", created.Id }, { "p_url", displayUrl }, { "p_side", "single" } });
            System.Diagnostics.Debug.WriteLine("[CreatePhoto-DIAG] APRÈS RPC insert_project_photo → OK");

            return (true, string.Empty, created.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePhoto-DIAG] EXCEPTION: type={ex.GetType().Name}, message={ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CreatePhoto-DIAG] INNER: {ex.InnerException.Message}");
            }
            return (false, ex.Message, string.Empty);
        }
    }

    // ─── Créer duel pour série ───────────────────────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreateSeriesDuelProjectAsync(string title, string? description,
            Stream photoLeftStream, string fileNameLeft,
            Stream photoRightStream, string fileNameRight)
    {
        System.Diagnostics.Debug.WriteLine($"[CreateDuel-DIAG] ENTRÉE: title={title}");
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté", string.Empty);

            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] AVANT UploadPhotoAsync left");
            var (urlLeft, _)  = await _uploadService.UploadPhotoAsync(photoLeftStream, fileNameLeft);
            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] AVANT UploadPhotoAsync right");
            var (urlRight, _) = await _uploadService.UploadPhotoAsync(photoRightStream, fileNameRight);
            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] APRÈS uploads OK");

            var project = new Project
            {
                OwnerId = userId, Title = title, Description = description,
                Type = "duel", Status = "approved", IsOpen = true,
                GenderFilter = "both", MinAge = 13, MaxAge = 99,
                CreatedAt = DateTime.UtcNow
            };

            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] AVANT INSERT Project");
            var result = await _supabase.From<Project>().Insert(project);
            var created = result.Models.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine(
                $"[CreateDuel-DIAG] APRÈS INSERT Project: created={(created == null ? "NULL" : created.Id)}");
            if (created == null) return (false, "Erreur création", string.Empty);

            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] AVANT RPCs insert_project_photo");
            await _supabase.Rpc("insert_project_photo", new Dictionary<string, object>
            { { "p_project_id", created.Id }, { "p_url", urlLeft }, { "p_side", "left" } });
            await _supabase.Rpc("insert_project_photo", new Dictionary<string, object>
            { { "p_project_id", created.Id }, { "p_url", urlRight }, { "p_side", "right" } });
            System.Diagnostics.Debug.WriteLine("[CreateDuel-DIAG] APRÈS RPCs → OK");

            return (true, string.Empty, created.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreateDuel-DIAG] EXCEPTION: type={ex.GetType().Name}, message={ex.Message}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[CreateDuel-DIAG] INNER: {ex.InnerException.Message}");
            return (false, ex.Message, string.Empty);
        }
    }

    // ─── Créer sondage pour série ────────────────────────────────────
    public async Task<(bool Success, string Error, string ProjectId)>
        CreateSeriesPollProjectAsync(string title, string? description, List<string> options)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[CreatePoll-DIAG] ENTRÉE: title={title}, options.Count={options.Count}");
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return (false, "Non connecté", string.Empty);
            var project = new Project
            {
                OwnerId = userId, Title = title, Description = description,
                Type = "poll", Status = "approved", IsOpen = true,
                GenderFilter = "both", MinAge = 13, MaxAge = 99,
                CreatedAt = DateTime.UtcNow
            };

            System.Diagnostics.Debug.WriteLine("[CreatePoll-DIAG] AVANT INSERT Project");
            var result = await _supabase.From<Project>().Insert(project);
            var created = result.Models.FirstOrDefault();
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePoll-DIAG] APRÈS INSERT Project: created={(created == null ? "NULL" : created.Id)}");
            if (created == null) return (false, "Erreur création", string.Empty);

            System.Diagnostics.Debug.WriteLine($"[CreatePoll-DIAG] AVANT INSERT {options.Count} options");
            for (int i = 0; i < options.Count; i++)
                await _supabase.From<PollOption>().Insert(new PollOption
                { ProjectId = created.Id, Text = options[i], Position = i });
            System.Diagnostics.Debug.WriteLine("[CreatePoll-DIAG] APRÈS INSERT options → OK");

            return (true, string.Empty, created.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CreatePoll-DIAG] EXCEPTION: type={ex.GetType().Name}, message={ex.Message}");
            if (ex.InnerException != null)
                System.Diagnostics.Debug.WriteLine($"[CreatePoll-DIAG] INNER: {ex.InnerException.Message}");
            return (false, ex.Message, string.Empty);
        }
    }

    // ─── Récupérer mes projets ───────────────────────────────────────
    public async Task<List<Project>> GetMyProjectsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return new List<Project>();
            // ⚡ Exclusion des projets de série : ils sont gérés depuis leur
            // série, pas depuis "Mes projets" (cf. supabase_patch_v4.sql).
            var result = await _supabase.From<Project>()
                .Where(p => p.OwnerId == userId && p.IsSeriesProject == false)
                .Get();
            return result.Models.OrderByDescending(p => p.CreatedAt).ToList();
        }
        catch { return new List<Project>(); }
    }

    // ─── Récupérer les photos d'un projet ────────────────────────────
    public async Task<List<ProjectPhoto>> GetProjectPhotosAsync(string projectId)
    {
        try
        {
            var result = await _supabase.From<ProjectPhoto>().Where(p => p.ProjectId == projectId).Get();
            return result.Models;
        }
        catch { return new List<ProjectPhoto>(); }
    }

    // ─── Récupérer les options d'un sondage ──────────────────────────
    public async Task<List<PollOption>> GetPollOptionsAsync(string projectId)
    {
        try
        {
            var result = await _supabase
                .From<PollOption>()
                .Filter("project_id", Postgrest.Constants.Operator.Equals, projectId)
                .Get();
            return result.Models.OrderBy(o => o.Position).ToList();
        }
        catch { return new List<PollOption>(); }
    }

    // ─── Fermer un projet ────────────────────────────────────────────
    public async Task<bool> CloseProjectAsync(string projectId)
    {
        try
        {
            await _supabase.From<Project>().Where(p => p.Id == projectId).Set(p => p.IsOpen, false).Update();
            return true;
        }
        catch { return false; }
    }

    // ─── Supprimer un projet ─────────────────────────────────────────
    public async Task<bool> DeleteProjectAsync(string projectId)
    {
        try
        {
            await _supabase.From<PhotoVote>().Where(v => v.ProjectId == projectId).Delete();
            await _supabase.From<DuelVote>().Where(v => v.ProjectId == projectId).Delete();
            await _supabase.From<PollVote>().Where(v => v.ProjectId == projectId).Delete();
            await _supabase.From<PollOption>().Filter("project_id", Postgrest.Constants.Operator.Equals, projectId).Delete();
            await _supabase.From<ProjectPhoto>().Where(p => p.ProjectId == projectId).Delete();
            await _supabase.From<Project>().Where(p => p.Id == projectId).Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteProject error: {ex.Message}");
            return false;
        }
    }

    // ─── ⚡ NOUVEAU : Modifier titre + description d'un projet ────────
    public async Task<(bool Success, string Error)> UpdateProjectMetadataAsync(
        string projectId, string title, string? description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title))
                return (false, "Le titre est obligatoire.");

            await _supabase.From<Project>()
                .Where(p => p.Id == projectId)
                .Set(p => p.Title, title.Trim())
                .Set(p => p.Description, description?.Trim())
                .Update();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateProjectMetadata: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── ⚡ NOUVEAU : Remplacer une photo d'un projet ─────────────────
    /// <summary>
    /// Supprime l'ancienne photo pour le 'side' donné et insère la nouvelle.
    /// 'side' est l'un des suivants : "single", "left", "right".
    /// </summary>
    public async Task<(bool Success, string Error)> UpdateProjectPhotoAsync(
        string projectId, string side, Stream newPhotoStream, string fileName)
    {
        try
        {
            // 1) Upload de la nouvelle photo
            var (newUrl, _) = await _uploadService.UploadPhotoAsync(newPhotoStream, fileName);
            if (string.IsNullOrEmpty(newUrl))
                return (false, "Échec de l'upload de la photo.");

            // 2) Supprimer l'ancienne photo de ce side (en DB ; l'image dans
            //    Storage est laissée orpheline - on pourra faire un nettoyage
            //    périodique côté Supabase si besoin)
            await _supabase.From<ProjectPhoto>()
                .Filter("project_id", Postgrest.Constants.Operator.Equals, projectId)
                .Filter("side", Postgrest.Constants.Operator.Equals, side)
                .Delete();

            // 3) Insérer la nouvelle photo via le RPC existant
            await _supabase.Rpc("insert_project_photo", new Dictionary<string, object>
            {
                { "p_project_id", projectId },
                { "p_url", newUrl },
                { "p_side", side }
            });

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateProjectPhoto: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── Stats photo ─────────────────────────────────────────────────
    public async Task<PhotoVoteStats?> GetPhotoStatsAsync(string projectId)
    {
        try
        {
            var votes = await _supabase.From<PhotoVote>().Where(v => v.ProjectId == projectId).Get();
            var list = votes.Models;
            if (!list.Any()) return null;

            return new PhotoVoteStats
            {
                ProjectId    = projectId,
                TotalVotes   = list.Count,
                ScoreMoyen   = list.Average(v => v.Rating),
                TotalJaime   = list.Count(v => v.Rating == 3),
                TotalMoyen   = list.Count(v => v.Rating == 2),
                TotalPasFan  = list.Count(v => v.Rating == 1),
                VotesHommes  = list.Count(v => v.VoterGender == "male"),
                VotesFemmes  = list.Count(v => v.VoterGender == "female"),
                VotesAutres  = list.Count(v => v.VoterGender == "other" || v.VoterGender == "prefer_not_to_say"),
                Age13_17     = list.Count(v => v.VoterAge >= 13 && v.VoterAge <= 17),
                Age18_24     = list.Count(v => v.VoterAge >= 18 && v.VoterAge <= 24),
                Age25_34     = list.Count(v => v.VoterAge >= 25 && v.VoterAge <= 34),
                Age35_49     = list.Count(v => v.VoterAge >= 35 && v.VoterAge <= 49),
                Age50_64     = list.Count(v => v.VoterAge >= 50 && v.VoterAge <= 64),
                Age65Plus    = list.Count(v => v.VoterAge >= 65)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPhotoStats error: {ex.Message}");
            return null;
        }
    }

    // ─── Stats duel ──────────────────────────────────────────────────
    public async Task<DuelVoteStats?> GetDuelStatsAsync(string projectId)
    {
        try
        {
            var votes = await _supabase.From<DuelVote>().Where(v => v.ProjectId == projectId).Get();
            var list = votes.Models;
            if (!list.Any()) return null;

            return new DuelVoteStats
            {
                ProjectId   = projectId,
                TotalVotes  = list.Count,
                VotesLeft   = list.Count(v => v.ChosenSide == "left"),
                VotesRight  = list.Count(v => v.ChosenSide == "right"),
                VotesHommes = list.Count(v => v.VoterGender == "male"),
                VotesFemmes = list.Count(v => v.VoterGender == "female"),
                Age13_17    = list.Count(v => v.VoterAge >= 13 && v.VoterAge <= 17),
                Age18_24    = list.Count(v => v.VoterAge >= 18 && v.VoterAge <= 24),
                Age25_34    = list.Count(v => v.VoterAge >= 25 && v.VoterAge <= 34),
                Age35_49    = list.Count(v => v.VoterAge >= 35 && v.VoterAge <= 49),
                Age50_64    = list.Count(v => v.VoterAge >= 50 && v.VoterAge <= 64),
                Age65Plus   = list.Count(v => v.VoterAge >= 65)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDuelStats error: {ex.Message}");
            return null;
        }
    }

    // ─── Stats sondage ───────────────────────────────────────────────
    public async Task<List<(PollOption Option, int Count, double Percentage)>>
        GetPollStatsAsync(string projectId)
    {
        try
        {
            var options = await GetPollOptionsAsync(projectId);
            var votes = await _supabase
                .From<PollVote>()
                .Filter("project_id", Postgrest.Constants.Operator.Equals, projectId)
                .Get();

            var totalVotes = votes.Models.Count;
            return options.Select(o =>
            {
                var count = votes.Models.Count(v => v.OptionId == o.Id);
                var pct   = totalVotes > 0 ? Math.Round((double)count / totalVotes * 100, 1) : 0;
                return (o, count, pct);
            }).ToList();
        }
        catch { return new List<(PollOption, int, double)>(); }
    }

    // ─── Charger photos pour plusieurs projets ───────────────────────
    public async Task<List<ProjectPhoto>> GetProjectPhotosForIdsAsync(List<string> projectIds)
    {
        try
        {
            if (projectIds == null || projectIds.Count == 0) return new List<ProjectPhoto>();
            var result = await _supabase.From<ProjectPhoto>()
                .Filter("project_id", Postgrest.Constants.Operator.In, projectIds).Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProjectPhotosForIds error: {ex.Message}");
            return new List<ProjectPhoto>();
        }
    }

    // ─── Charger plusieurs profils ───────────────────────────────────
    public async Task<List<BeauOuPas.Models.Profile>> GetProfilesForIdsAsync(List<string> userIds)
    {
        try
        {
            if (userIds == null || userIds.Count == 0) return new List<BeauOuPas.Models.Profile>();
            var result = await _supabase.From<BeauOuPas.Models.Profile>()
                .Filter("id", Postgrest.Constants.Operator.In, userIds).Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProfilesForIds error: {ex.Message}");
            return new List<BeauOuPas.Models.Profile>();
        }
    }
    // ─── ⚡ NOUVEAU : Récupérer mes projets + stats résumées en 1 seul appel ──
    // Remplace la chaîne lente : GetMyProjectsAsync + N×GetProjectPhotosAsync
    //                          + N×GetXxxStatsAsync (= jusqu'à 41 requêtes)
    // par UNE SEULE RPC SQL qui ramène tout en JSON.
    // Voir SQL : get_my_projects_with_stats(p_user_id uuid)
    //
    // ⚠️ Cette méthode ne ramène que ce qui est affiché dans la LISTE
    // "Mes projets". Pour les stats détaillées (par genre/âge dans la
    // popup détail), continue d'utiliser GetPhotoStatsAsync, GetDuelStatsAsync,
    // GetPollStatsAsync — elles sont toujours nécessaires.
    public async Task<string> GetMyProjectsWithStatsJsonAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId)) return "[]";

            var parameters = new Dictionary<string, object>
            {
                { "p_user_id", userId }
            };

            var response = await _supabase.Rpc("get_my_projects_with_stats", parameters);

            // Le contenu est un JSON brut (jsonb retourné par la RPC).
            // On le retourne tel quel — le ViewModel le parsera.
            return response?.Content ?? "[]";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMyProjectsWithStats: {ex.Message}");
            return "[]";
        }
    }
}
