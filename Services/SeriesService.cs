using BeauOuPas.Models;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace BeauOuPas.Services;

public class SeriesService
{
    public string? LastError { get; private set; }
    private readonly Supabase.Client _supabase;
    private readonly AppSettingsService _settingsService;
    private readonly CreditService _creditService;
    private readonly LocationService _locationService;  // ⚡ NOUVEAU
    private readonly PhotoUploadService _uploadService; // ⚡ pour upload selfies session quiz/party

    public SeriesService(
        Supabase.Client supabase,
        AppSettingsService settingsService,
        CreditService creditService,
        LocationService locationService,                  // ⚡ NOUVEAU
        PhotoUploadService uploadService)                 // ⚡ pour upload selfies session
    {
        _supabase = supabase;
        _settingsService = settingsService;
        _creditService = creditService;
        _locationService = locationService;             // ⚡ NOUVEAU
        _uploadService = uploadService;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;
    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : RPC qui retourne TOUTE la page de détail d'une série
    // (series + is_creator + is_participant + participants + all_projects)
    // en 1 SEULE requête au lieu de ~3+P+M.
    // Voir get_series_detail_full dans Supabase.
    // ═════════════════════════════════════════════════════════════════
    public async Task<string?> GetSeriesDetailFullJsonAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return null;

            var result = await _supabase.Rpc("get_series_detail_full", new Dictionary<string, object>
            {
                { "p_series_id", seriesId },
                { "p_user_id", userId }
            });

            return result.Content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSeriesDetailFull: {ex.Message}");
            return null;
        }
    }
    public async Task<List<Series>> GetGroupSeriesAsync(string groupId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"GetGroupSeries: groupId={groupId}");
            var result = await _supabase.From<Series>()
                .Where(s => s.GroupId == groupId)
                .Order(s => s.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Get();
            System.Diagnostics.Debug.WriteLine($"GetGroupSeries: count={result.Models.Count}");
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetGroupSeries ERROR: {ex.Message}");
            return new();
        }
    }

    public async Task<Series?> CreateSeriesAsync(
        string? groupId, string title, string? description,
        int maxProjects, int maxPerMember,
        bool projectsHidden,
        bool showStats = true,
        bool isStandalone = false,
        bool membersCanAddProjects = true,
         bool isQuiz = false)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return null;
            var creditResult = await _supabase.Rpc("spend_series_credits",
               new Dictionary<string, object>
               {
                    { "p_user_id", userId },
                    { "p_is_quiz", isQuiz }
               });

            if (creditResult?.Content != null)
            {
                var json = creditResult.Content;
                // La RPC renvoie { "success": bool, "error": "...", "cost": int }
                if (json.Contains("\"success\":false") || json.Contains("\"success\": false"))
                {
                    // Extraire le message d'erreur pour l'afficher à l'utilisateur
                    var match = System.Text.RegularExpressions.Regex.Match(
                        json, "\"error\"\\s*:\\s*\"([^\"]*)\"");
                    var errMsg = match.Success && !string.IsNullOrEmpty(match.Groups[1].Value)
                        ? match.Groups[1].Value
                        : "Crédits insuffisants pour créer cette série.";

                    LastError = errMsg;   // ← propriété à exposer pour l'UI (voir étape 3)
                    System.Diagnostics.Debug.WriteLine($"spend_series_credits refusé: {errMsg}");
                    return null;
                }
            }
            var accessCode = GenerateAccessCode();

            var series = new Series
            {
                GroupId = groupId,
                CreatorId = userId,
                Title = title,
                Description = description,
                MaxProjects = maxProjects,
                MaxPerMember = maxPerMember,
                Status = "preparing",
                IsStandalone = isStandalone,
                AccessCode = accessCode,
                ProjectsHidden = projectsHidden,
                ShowStats = showStats,
                MembersCanAddProjects = membersCanAddProjects,   // ⚡ MAI 2026 : B2
                CreatedAt = DateTime.UtcNow
            };

            var result = await _supabase.From<Series>().Insert(series);
            return result.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateSeries: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Démarre la série/quiz et tente de capturer le lieu (best-effort).
    /// Si la localisation échoue (permission refusée, timeout, etc.),
    /// la série démarre quand même sans lieu. Aucun plantage possible.
    /// </summary>
    public async Task<bool> StartSeriesAsync(string seriesId)
    {
        try
        {
            // ─── 1) Capture du lieu (best-effort, ne fait JAMAIS planter)
            string? city = null;
            string? country = null;
            double? lat = null;
            double? lng = null;

            try
            {
                var loc = await _locationService.GetLocationAsync();
                lat = loc.Lat;
                lng = loc.Lng;
                city = string.IsNullOrWhiteSpace(loc.City) ? null : loc.City;
                country = string.IsNullOrWhiteSpace(loc.Country) ? null : loc.Country;

                System.Diagnostics.Debug.WriteLine(
                    $"[StartSeries] Lieu capturé: {city ?? "?"}, {country ?? "?"} " +
                    $"({lat?.ToString() ?? "null"}, {lng?.ToString() ?? "null"})");
            }
            catch (Exception locEx)
            {
                // Permission refusée, GPS off, géocodage cassé, etc.
                // On démarre la série quand même sans le lieu.
                System.Diagnostics.Debug.WriteLine(
                    $"[StartSeries] Lieu indisponible: {locEx.Message}");
            }

            // ─── 2) Reveal de tous les projets (comportement existant)
            await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId)
                .Set(sp => sp.IsRevealed, true)
                .Update();

            // ─── 3) Update du status + lieu + started_at en une seule requête
            await _supabase.From<Series>()
                .Where(s => s.Id == seriesId)
                .Set(s => s.Status, "active")
                .Set(s => s.CurrentProjectIndex, 0)
                .Set(s => s.StartedAt, DateTime.UtcNow)
                .Set(s => s.PlayCity, city)
                .Set(s => s.PlayCountry, country)
                .Set(s => s.PlayLat, lat)
                .Set(s => s.PlayLng, lng)
                .Update();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartSeries: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Termine la série/quiz et capture l'horodatage de fin.
    /// </summary>
    public async Task<bool> StopSeriesAsync(string seriesId)
    {
        try
        {
            await _supabase.From<Series>()
                .Where(s => s.Id == seriesId)
                .Set(s => s.Status, "finished")
                .Set(s => s.EndedAt, DateTime.UtcNow)   // ⚡ NOUVEAU
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StopSeries: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// ⚡ Repréparer la série : nettoie tout (votes, selfies, participants,
    /// réponses quiz, mode TV) et remet le status à "preparing" pour pouvoir
    /// relancer la même série proprement.
    ///
    /// 🔧 BUG E FIX : la version précédente faisait 6 appels REST séparés,
    /// mais les RLS sur quiz_answers (pas de policy DELETE) bloquaient
    /// silencieusement le nettoyage des réponses. Du coup, après un Repréparer
    /// sur un quiz, HasUserAnsweredAsync retournait true et les joueurs étaient
    /// bloqués sur l'écran "Réponse envoyée" en boucle.
    ///
    /// On passe maintenant par la RPC reset_series_for_replay (SECURITY DEFINER)
    /// qui bypass les RLS, vérifie que le caller est le créateur, et fait tout
    /// le reset dans une seule transaction (atomique). Couvre vote ET quiz.
    /// </summary>
    public async Task<bool> ResetSeriesAsync(string seriesId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId))
            {
                System.Diagnostics.Debug.WriteLine("[ResetSeries] No user, abort");
                return false;
            }

            var res = await _supabase.Rpc("reset_series_for_replay",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId },
                    { "p_user_id",   userId }
                });

            var content = res?.Content;
            if (string.IsNullOrEmpty(content))
            {
                System.Diagnostics.Debug.WriteLine("[ResetSeries] empty RPC response");
                return false;
            }

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;

            if (!success)
            {
                var errorCode = json.Value<string>("error") ?? "unknown_error";
                var errorMsg = json.Value<string>("message") ?? string.Empty;
                System.Diagnostics.Debug.WriteLine(
                    $"[ResetSeries] FAILED: {errorCode} {errorMsg}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[ResetSeries] OK for series {seriesId}");

            // Nettoyage des votes party (table ajoutée après reset_series_for_replay ;
            // non couverte par la RPC principale). Idempotent : no-op pour les quiz.
            try
            {
                await _supabase.Rpc("clear_party_answers",
                    new Dictionary<string, object>
                    {
                        { "p_series_id", seriesId },
                        { "p_user_id",   userId }
                    });
            }
            catch (Exception exParty)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ResetSeries] clear_party_answers (non bloquant): {exParty.Message}");
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ResetSeries] EXCEPTION: {ex.Message}");
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Upload d'un selfie de session (mode quiz / partie rapide). Pas lié
    // à un projet/question particulière : SeriesProjectId reste NULL.
    // Réutilise PhotoUploadService.UploadSessionSelfieFastAsync puis insert
    // une ligne series_selfies. Jamais throw, retourne false si échec.
    // ═════════════════════════════════════════════════════════════════
    public async Task<bool> UploadSessionSelfieAsync(
        string seriesId,
        Stream photoStream,
        string? questionId = null)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var url = await _uploadService.UploadSessionSelfieFastAsync(
                photoStream, seriesId, userId);
            if (string.IsNullOrEmpty(url)) return false;

            var selfie = new SeriesSelfie
            {
                SeriesId = seriesId,
                SeriesProjectId = null,
                SeriesQuestionId = questionId,
                UserId = userId,
                PhotoUrl = url,
                CreatedAt = DateTime.UtcNow
            };
            await _supabase.From<SeriesSelfie>().Insert(selfie);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesService] UploadSessionSelfie: {ex.Message}");
            return false;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Signale à la TV que le joueur a fini de visionner sa pub interstitielle
    // (ou qu'aucune pub ne lui sera affichée). La TV utilise cette info pour
    // démarrer la 1ère question dès que tous les joueurs ont signalé, sans
    // attendre la fin du countdown intro complet. Jamais bloquant côté UI.
    // ═════════════════════════════════════════════════════════════════
    public async Task MarkInterstitialSeenAsync(string seriesId)
    {
        try
        {
            await _supabase.Rpc("mark_interstitial_seen",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId }
                });
            System.Diagnostics.Debug.WriteLine($"[InterstitialSync] mark_interstitial_seen OK ({seriesId})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InterstitialSync] mark_interstitial_seen ERR: {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ Suppression sécurisée d'une série
    //
    // Étapes :
    //   1) Supprimer les fichiers selfies du bucket Supabase Storage
    //      (les photos display/thumbnails sont partagées entre séries
    //       et NE doivent PAS être supprimées)
    //   2) Appeler la RPC delete_series_safe qui :
    //      - vérifie que l'utilisateur est bien le créateur
    //      - supprime la série (cascade FK : votes, projets, selfies BDD,
    //        participants, invitations)
    //
    // Retourne un DeleteSeriesResult avec succès/erreur/message clair.
    // ═════════════════════════════════════════════════════════════════
    public async Task<DeleteSeriesResult> DeleteSeriesAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new DeleteSeriesResult
                {
                    Success = false,
                    ErrorCode = "not_authenticated",
                    Message = "Tu dois être connecté pour supprimer une série."
                };
            }

            // 1) Supprimer les selfies du Storage AVANT la BDD.
            //    Si ça échoue partiellement, ce n'est pas bloquant : on log
            //    et on continue. La BDD reste prioritaire.
            try
            {
                await DeleteSeriesSelfiesFromStorageAsync(seriesId);
            }
            catch (Exception storageEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesService] DeleteSeries Storage warning: {storageEx.Message}");
            }

            // ⚡ LOT 1 : 1bis) Supprimer les photos de questions quiz du Storage.
            //    Récupère les URLs des photos AVANT que les questions soient
            //    cascade-deleted par la RPC. Pas bloquant non plus.
            try
            {
                await DeleteSeriesQuizPhotosFromStorageAsync(seriesId);
            }
            catch (Exception storageEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesService] DeleteSeries QuizPhotos Storage warning: {storageEx.Message}");
            }

            // 2) Appel de la RPC qui fait la suppression BDD avec cascade FK.
            var response = await _supabase.Rpc("delete_series_safe", new Dictionary<string, object>
            {
                { "p_series_id", seriesId },
                { "p_user_id", userId }
            });

            return ParseDeleteSeriesResult(response?.Content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteSeriesAsync: {ex.Message}");
            return new DeleteSeriesResult
            {
                Success = false,
                ErrorCode = "exception",
                Message = $"Erreur technique : {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Liste tous les fichiers dans selfies/{seriesId}/ et les supprime du bucket photos.
    /// Arborescence : selfies/{seriesId}/{seriesProjectId}/{userId}_{ts}.jpg
    /// </summary>
    private async Task DeleteSeriesSelfiesFromStorageAsync(string seriesId)
    {
        var bucket = _supabase.Storage.From("photos");
        var prefix = $"selfies/{seriesId}";

        // 1) Lister les sous-dossiers (un par seriesProjectId)
        var subFolders = await bucket.List(prefix);
        if (subFolders == null || subFolders.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Pas de selfies à supprimer dans '{prefix}'.");
            return;
        }

        // 2) Pour chaque sous-dossier, lister les fichiers et collecter leurs chemins
        var filesToDelete = new List<string>();
        foreach (var sub in subFolders)
        {
            if (string.IsNullOrEmpty(sub.Name)) continue;

            var subPrefix = $"{prefix}/{sub.Name}";
            var files = await bucket.List(subPrefix);
            if (files == null) continue;

            foreach (var f in files)
            {
                if (!string.IsNullOrEmpty(f.Name))
                    filesToDelete.Add($"{subPrefix}/{f.Name}");
            }
        }

        if (filesToDelete.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Aucun fichier selfie trouvé sous '{prefix}'.");
            return;
        }

        // 3) Suppression en lot
        await bucket.Remove(filesToDelete);
        System.Diagnostics.Debug.WriteLine(
            $"[SeriesService] {filesToDelete.Count} selfie(s) supprimé(s) du bucket pour la série {seriesId}.");
    }

    private static DeleteSeriesResult ParseDeleteSeriesResult(string? json)
    {
        var fallback = new DeleteSeriesResult
        {
            Success = false,
            ErrorCode = "parse_error",
            Message = "Réponse serveur invalide."
        };

        if (string.IsNullOrWhiteSpace(json)) return fallback;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new DeleteSeriesResult
            {
                Success = root.TryGetProperty("success", out var s)
                          && s.ValueKind == JsonValueKind.True,
                ErrorCode = root.TryGetProperty("error", out var e)
                            && e.ValueKind == JsonValueKind.String
                            ? e.GetString() : null,
                Message = root.TryGetProperty("message", out var m)
                          && m.ValueKind == JsonValueKind.String
                          ? m.GetString() ?? string.Empty : string.Empty,
                DeletedTitle = root.TryGetProperty("deleted_title", out var t)
                                && t.ValueKind == JsonValueKind.String
                                ? t.GetString() : null,
                DeletedStatus = root.TryGetProperty("deleted_status", out var st)
                                && st.ValueKind == JsonValueKind.String
                                ? st.GetString() : null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseDeleteSeriesResult: {ex.Message}");
            return fallback;
        }
    }
    public async Task<bool> NextProjectAsync(string seriesId, int currentIndex)
    {
        try
        {
            await _supabase.From<Series>()
                .Where(s => s.Id == seriesId)
                .Set(s => s.CurrentProjectIndex, currentIndex + 1)
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NextProject: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Error)> AddProjectToSeriesAsync(
        string seriesId, string projectId,
        bool selfieEnabled = false, string? themeId = null)
    {
        // ⚡ DIAG : tracer chaque étape pour voir où ça rame
        System.Diagnostics.Debug.WriteLine(
            $"[AddProject-DIAG] ENTRÉE: seriesId={seriesId}, projectId={projectId}");
        try
        {
            var userId = CurrentUserId;
            if (userId == null)
            {
                System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] userId NULL → abort");
                return (false, "Non connecté");
            }

            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT GetSeriesAsync");
            var series = await GetSeriesAsync(seriesId);
            System.Diagnostics.Debug.WriteLine(
                $"[AddProject-DIAG] APRÈS GetSeriesAsync: series={(series == null ? "NULL" : "OK")}");
            if (series == null) return (false, "Série introuvable");

            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT myProjects query");
            var myProjects = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId && sp.AddedBy == userId).Get();
            System.Diagnostics.Debug.WriteLine(
                $"[AddProject-DIAG] APRÈS myProjects query: count={myProjects.Models.Count}, max={series.MaxPerMember}");

            if (myProjects.Models.Count >= series.MaxPerMember)
                return (false, $"Limite atteinte ({series.MaxPerMember} projet(s) max par membre)");

            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT allProjects query");
            var allProjects = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId).Get();
            System.Diagnostics.Debug.WriteLine(
                $"[AddProject-DIAG] APRÈS allProjects query: count={allProjects.Models.Count}, max={series.MaxProjects}");

            if (allProjects.Models.Count >= series.MaxProjects)
                return (false, $"La série est complète ({series.MaxProjects} projets max)");

            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT GetCreditsCostSeriesProjectAsync");
            var cost = await _settingsService.GetCreditsCostSeriesProjectAsync();
            System.Diagnostics.Debug.WriteLine($"[AddProject-DIAG] APRÈS cost: cost={cost}");

            if (cost > 0)
            {
                System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT SpendCreditsAsync");
                var (success, error) = await _creditService.SpendCreditsAsync(
                    cost, "series_project", "Ajout d'un projet dans une série");
                System.Diagnostics.Debug.WriteLine(
                    $"[AddProject-DIAG] APRÈS SpendCreditsAsync: success={success}, error={error}");
                if (!success) return (false, error);
            }

            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] AVANT INSERT SeriesProject");
            await _supabase.From<SeriesProject>().Insert(new SeriesProject
            {
                SeriesId = seriesId,
                ProjectId = projectId,
                AddedBy = userId,
                IsRevealed = false,
                Position = allProjects.Models.Count,
                SelfieEnabled = selfieEnabled,
                ThemeId = themeId,
                AddedAt = DateTime.UtcNow
            });
            System.Diagnostics.Debug.WriteLine("[AddProject-DIAG] APRÈS INSERT SeriesProject → OK");

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            // ⚡ DIAG : log enrichi
            System.Diagnostics.Debug.WriteLine(
                $"[AddProject-DIAG] EXCEPTION: type={ex.GetType().Name}, message={ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AddProject-DIAG] INNER: {ex.InnerException.Message}");
            }
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Error)> AddTemplateToSeriesAsync(
        string seriesId, string templateId,
        bool selfieEnabled = false, string? themeId = null)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "Non connecté");

            var series = await GetSeriesAsync(seriesId);
            if (series == null) return (false, "Série introuvable");

            var myProjects = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId && sp.AddedBy == userId).Get();

            if (myProjects.Models.Count >= series.MaxPerMember)
                return (false, $"Limite atteinte ({series.MaxPerMember} projet(s) max par membre)");

            var allProjects = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId).Get();

            if (allProjects.Models.Count >= series.MaxProjects)
                return (false, $"La série est complète ({series.MaxProjects} projets max)");

            var template = await _supabase.From<ProjectTemplate>()
                .Where(t => t.Id == templateId).Single();
            if (template == null) return (false, "Template introuvable");

            var cost = await _settingsService.GetCreditsCostSeriesTemplateAsync();
            if (cost > 0)
            {
                var (success, error) = await _creditService.SpendCreditsAsync(
                    cost, "series_template", "Ajout d'un template dans une série");
                if (!success) return (false, error);
            }

            var project = new Project
            {
                OwnerId = userId,
                Title = template.Title,
                Description = template.Description,
                Type = template.Type,
                Status = "approved",
                IsOpen = true,
                GenderFilter = "both",
                CreatedAt = DateTime.UtcNow
            };

            var projectResult = await _supabase.From<Project>().Insert(project);
            var createdProject = projectResult.Models.FirstOrDefault();
            if (createdProject == null) return (false, "Erreur création projet");

            if (template.Type == "poll" && !string.IsNullOrEmpty(template.Options))
            {
                var options = System.Text.Json.JsonSerializer
                    .Deserialize<List<string>>(template.Options) ?? new();
                for (int i = 0; i < options.Count; i++)
                {
                    await _supabase.From<PollOption>().Insert(new PollOption
                    {
                        ProjectId = createdProject.Id,
                        Text = options[i],
                        Position = i
                    });
                }
            }

            await _supabase.From<SeriesProject>().Insert(new SeriesProject
            {
                SeriesId = seriesId,
                ProjectId = createdProject.Id,
                AddedBy = userId,
                IsRevealed = false,
                Position = allProjects.Models.Count,
                SelfieEnabled = selfieEnabled,
                ThemeId = themeId,
                AddedAt = DateTime.UtcNow
            });

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddTemplateToSeries: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<List<SeriesProject>> GetSeriesProjectsAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            var result = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId)
                .Order(sp => sp.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();

            // Si la série est en mode "projects_hidden = false",
            // on rend tous les projets visibles à tout le monde.
            // Si "projects_hidden = true" (ou null pour les anciennes séries),
            // on applique le filtre original : révélés OU mes propres projets.
            var series = await GetSeriesAsync(seriesId);
            var hidden = series?.ProjectsHidden ?? true; // legacy = caché par sécurité

            if (!hidden)
                return result.Models;

            return result.Models.Where(sp =>
                sp.IsRevealed || sp.AddedBy == userId).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSeriesProjects: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// Retourne TOUS les projets d'une série, sans aucun filtre.
    /// Réservé à des calculs internes (compteurs lobby, etc).
    /// </summary>
    public async Task<List<SeriesProject>> GetAllSeriesProjectsAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId)
                .Order(sp => sp.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAllSeriesProjects: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// Pour le lobby, retourne la liste des participants ayant ajouté
    /// au moins un projet, avec leur nom + compteur.
    /// </summary>
    public async Task<List<SeriesParticipantStat>> GetParticipantsWithStatsAsync(string seriesId)
    {
        try
        {
            // 1) Tous les projets de la série
            var projects = await _supabase.From<SeriesProject>()
                .Where(sp => sp.SeriesId == seriesId)
                .Get();

            // 2) Grouper par AddedBy
            var grouped = projects.Models
                .GroupBy(sp => sp.AddedBy)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToList();

            if (grouped.Count == 0) return new();

            // 3) Récupérer les profils en un seul appel
            var userIds = grouped.Select(g => g.UserId).ToList();
            var profilesResult = await _supabase.From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.In, userIds)
                .Get();

            // 4) Joindre
            var stats = grouped.Select(g =>
            {
                var profile = profilesResult.Models.FirstOrDefault(p => p.Id == g.UserId);
                return new SeriesParticipantStat
                {
                    UserId = g.UserId,
                    Username = profile?.Username ?? "Inconnu",
                    AvatarUrl = profile?.AvatarUrl,
                    ProjectCount = g.Count
                };
            })
            .OrderByDescending(s => s.ProjectCount)
            .ThenBy(s => s.Username)
            .ToList();

            return stats;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetParticipantsWithStats: {ex.Message}");
            return new();
        }
    }

    public async Task<List<ProjectTemplate>> GetTemplatesAsync(string? category = null)
    {
        try
        {
            var result = await _supabase.From<ProjectTemplate>()
                .Where(t => t.IsActive == true).Get();
            var templates = result.Models;
            if (!string.IsNullOrEmpty(category))
                templates = templates.Where(t => t.Category == category).ToList();
            return templates;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetTemplates: {ex.Message}");
            return new();
        }
    }

    public async Task<List<VisualTheme>> GetVisualThemesAsync()
    {
        try
        {
            var result = await _supabase.From<VisualTheme>()
                .Where(t => t.IsActive == true).Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetVisualThemes: {ex.Message}");
            return new();
        }
    }

    public async Task<Series?> GetSeriesAsync(string seriesId)
    {
        try
        {
            return await _supabase.From<Series>()
                .Where(s => s.Id == seriesId).Single();
        }
        catch { return null; }
    }

    public async Task<(bool Success, string Error, Series? Series)> JoinSeriesByCodeAsync(string code)
    {
        try
        {
            var upperCode = code.Trim().ToUpper();

            var parameters = new Dictionary<string, object>
            {
                { "p_code", upperCode }
            };

            try
            {
                await _supabase.Rpc("join_series_by_code", parameters);
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesService] RPC join_series_by_code OK pour code {upperCode}");
            }
            catch (Exception rpcEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesService] RPC join_series_by_code: {rpcEx.Message}");
            }

            var seriesResult = await _supabase.From<Series>()
                .Filter("access_code", Postgrest.Constants.Operator.Equals, upperCode)
                .Single();

            if (seriesResult == null)
                return (false, "Code invalide ou série introuvable", null);

            if (seriesResult.Status == "finished")
                return (false, "Cette série est terminée", null);

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] User a rejoint la série {seriesResult.Id}");

            return (true, string.Empty, seriesResult);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"JoinSeriesByCode: {ex.Message}");
            return (false, "Code invalide", null);
        }
    }

    public async Task<List<Series>> GetMyStandaloneSeriesAsync()
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return new();

            // Appel de la RPC PostgreSQL qui fait le JOIN series + series_participants
            var response = await _supabase.Rpc("get_my_standalone_series", null);

            if (response == null || string.IsNullOrWhiteSpace(response.Content))
                return new();

            // La RPC retourne du JSON, on parse en List<Series>
            var series = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Series>>(response.Content);
            return series ?? new();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMyStandaloneSeries: {ex.Message}");
            return new();
        }
    }

    public async Task<Project?> GetProjectAsync(string projectId)
    {
        try { return await _supabase.From<Project>().Where(p => p.Id == projectId).Single(); }
        catch { return null; }
    }

    public async Task<List<ProjectPhoto>> GetProjectPhotosAsync(string projectId)
    {
        try
        {
            var result = await _supabase.From<ProjectPhoto>()
                .Where(p => p.ProjectId == projectId).Get();
            return result.Models;
        }
        catch { return new(); }
    }

    public async Task<List<PollOption>> GetPollOptionsAsync(string projectId)
    {
        try
        {
            var result = await _supabase.From<PollOption>()
                .Where(o => o.ProjectId == projectId)
                .Order(o => o.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();
            return result.Models;
        }
        catch { return new(); }
    }

    /// <summary>
    /// Supprime un projet de série en nettoyant tout :
    /// - les votes/selfies liés à ce projet dans la série
    /// - le lien series_projects
    /// - le projet lui-même (et toutes ses dépendances : photos, options, votes globaux)
    /// </summary>
    public async Task<(bool Success, string Error)> DeleteSeriesProjectAsync(
        string seriesProjectId, string projectId)
    {
        try
        {
            // 1) Supprimer les votes/selfies liés à CE projet dans la série
            await _supabase.From<SeriesVote>()
                .Where(v => v.SeriesProjectId == seriesProjectId)
                .Delete();
            await _supabase.From<SeriesSelfie>()
                .Where(s => s.SeriesProjectId == seriesProjectId)
                .Delete();

            // 2) Supprimer le lien series_projects
            await _supabase.From<SeriesProject>()
                .Where(sp => sp.Id == seriesProjectId)
                .Delete();

            // 3) Supprimer le projet et toutes ses dépendances
            await _supabase.From<PollOption>()
                .Filter("project_id", Postgrest.Constants.Operator.Equals, projectId)
                .Delete();
            await _supabase.From<ProjectPhoto>()
                .Where(p => p.ProjectId == projectId)
                .Delete();
            await _supabase.From<Project>()
                .Where(p => p.Id == projectId)
                .Delete();

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteSeriesProject: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Helper pour récupérer plusieurs profils en un seul appel.
    /// Utilisé pour afficher les noms d'auteur des projets en mode modération.
    /// </summary>
    public async Task<List<Profile>> GetProfilesByIdsAsync(List<string> userIds)
    {
        try
        {
            if (userIds == null || userIds.Count == 0) return new();
            var result = await _supabase.From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.In, userIds)
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProfilesByIds: {ex.Message}");
            return new();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ⚡ Mode TV — Wrappers des RPC Supabase
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Active le mode TV pour cette série. Retourne le code d'accès à
    /// taper sur la TV.
    /// </summary>
    public async Task<(bool ok, string? accessCode, string? error)> ActivateTvModeAsync(
        string seriesId, bool animatorVotes)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId },
                { "p_animator_votes", animatorVotes }
            };

            var response = await _supabase.Rpc("tv_activate", parameters);

            if (response == null || string.IsNullOrEmpty(response.Content))
            {
                return (false, null, "Réponse vide du serveur");
            }

            var series = await GetSeriesAsync(seriesId);
            return (true, series?.AccessCode, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ActivateTvMode: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// ⚡ Si l'animateur a dit "Je participe" dans la popup d'activation,
    /// l'ajouter immédiatement aux participants pour qu'il soit compté
    /// dans le compteur de la TV. Idempotent.
    /// </summary>
    public async Task<bool> AddAnimatorAsParticipantAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesService] AddAnimatorAsParticipant : pas d'utilisateur connecté");
                return false;
            }

            // Déjà inscrit ? On ne fait rien.
            var existing = await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == userId)
                .Get();

            if (existing.Models.Any())
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesService] Animateur déjà dans la liste des participants");
                return true;
            }

            // Inscription
            var participant = new SeriesParticipant
            {
                SeriesId = seriesId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            await _supabase.From<SeriesParticipant>().Insert(participant);
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Animateur ajouté aux participants de la série {seriesId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] AddAnimatorAsParticipant ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// ⚡ NOUVEAU : retire le current user de la liste des participants.
    /// Appelé quand l'utilisateur confirme "Quitter la série" depuis :
    /// - le bouton Retour de SeriesDetailPage (salle d'attente)
    /// - le bouton Retour de SeriesVotePage (vote en cours)
    /// Idempotent : si l'utilisateur n'est pas dans la liste, ne fait rien.
    /// </summary>
    public async Task<bool> LeaveSeriesAsync(string seriesId)
    {
        try
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesService] LeaveSeriesAsync : pas d'utilisateur connecté");
                return false;
            }

            await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId && p.UserId == userId)
                .Delete();

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] User {userId} a quitté la série {seriesId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesService] LeaveSeries: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Démarre la série en mode TV : passe le status à 'active' et
    /// déclenche le 1er projet (started_at = NOW()).
    /// </summary>
    public async Task<(bool ok, string? error)> StartTvSeriesAsync(string seriesId)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId }
            };

            await _supabase.Rpc("tv_start_series", parameters);
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartTvSeries: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Avance au projet suivant (ou termine la série si on est au dernier).
    /// </summary>
    public async Task<(bool ok, bool finished, string? error)> AdvanceToNextProjectAsync(
        string seriesId)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId }
            };

            var response = await _supabase.Rpc("tv_advance_to_next", parameters);
            var series = await GetSeriesAsync(seriesId);
            var finished = series?.Status == "finished";
            return (true, finished, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AdvanceToNext: {ex.Message}");
            return (false, false, ex.Message);
        }
    }

    /// <summary>
    /// Met en pause / reprend le mode TV.
    /// </summary>
    public async Task<(bool ok, bool nowPaused, string? error)> ToggleTvPauseAsync(string seriesId)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId }
            };

            await _supabase.Rpc("tv_toggle_pause", parameters);
            var series = await GetSeriesAsync(seriesId);
            return (true, series?.TvPaused ?? false, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleTvPause: {ex.Message}");
            return (false, false, ex.Message);
        }
    }

    /// <summary>
    /// Désactive le mode TV. Vide aussi la liste des participants pour
    /// repartir sur une session propre si on relance plus tard.
    /// </summary>
    public async Task<(bool ok, string? error)> DeactivateTvModeAsync(string seriesId)
    {
        try
        {
            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId }
            };

            await _supabase.Rpc("tv_deactivate", parameters);

            // ⚡ Vider la liste des participants : si la série est relancée
            // plus tard, on repart sur une liste propre (pas de fantômes).
            await _supabase.From<SeriesParticipant>()
                .Where(p => p.SeriesId == seriesId)
                .Delete();
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Mode TV désactivé : participants vidés pour série {seriesId}");

            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeactivateTvMode: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// ⚡ NOUVEAU : Désactive automatiquement toutes les sessions TV (séries et
    /// quiz) dont l'utilisateur courant est créateur et qui sont restées
    /// actives. Appelé au démarrage de l'app pour nettoyer les sessions
    /// orphelines (cas où l'app a été tuée sans avoir cliqué sur "Arrêter").
    ///
    /// Logique : si l'utilisateur relance l'app, c'est que la session TV
    /// précédente est forcément terminée — on peut désactiver sans risque.
    ///
    /// Appel "fire-and-forget" : silencieux en cas d'erreur, ne bloque
    /// jamais le démarrage. Aucun message utilisateur.
    /// </summary>
    public async Task DeactivateAllMyActiveTvSessionsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId)) return;

            // Récupérer toutes les séries (incluant quiz, qui sont aussi des
            // 'series' avec is_quiz=true) où je suis créateur ET tv_active=true
            var activeSeries = await _supabase.From<Series>()
                .Where(s => s.CreatorId == userId)
                .Where(s => s.TvActive == true)
                .Get();

            if (activeSeries?.Models == null || activeSeries.Models.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesService] DeactivateAllMyActiveTvSessions: aucune session TV active");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] DeactivateAllMyActiveTvSessions: {activeSeries.Models.Count} session(s) à fermer");

            foreach (var series in activeSeries.Models)
            {
                try
                {
                    await DeactivateTvModeAsync(series.Id);
                }
                catch (Exception inner)
                {
                    // On loggue mais on continue : un échec sur une série ne
                    // doit pas empêcher de fermer les autres.
                    System.Diagnostics.Debug.WriteLine(
                        $"[SeriesService] Échec fermeture TV série {series.Id}: {inner.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Silencieux : appel best-effort au démarrage, ne doit jamais bloquer
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] DeactivateAllMyActiveTvSessions: {ex.Message}");
        }
    }


    // ═════════════════════════════════════════════════════════════════
    // ⚡ LOT 1 : Méthodes liées au mode QUIZ
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⚡ LOT 1 : Liste les questions d'un quiz avec leurs métadonnées
    /// (id, position, title, question_text, photo_url) ordonnées par position.
    /// Utilisé pour afficher la liste en lecture seule dans SeriesDetailPage.
    /// </summary>
    public async Task<List<QuizQuestionRow>> GetQuizQuestionsAsync(string seriesId)
    {
        try
        {
            // Lecture directe de la table quiz_questions via PostgREST.
            // Pas de RPC dédiée — on n'a pas besoin des options ni des réponses
            // dans cette vue (juste la liste read-only).
            var response = await _supabase
                .From<QuizQuestionRow>()
                .Where(q => q.SeriesId == seriesId)
                .Order(q => q.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();

            return response?.Models ?? new List<QuizQuestionRow>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetQuizQuestionsAsync: {ex.Message}");
            return new List<QuizQuestionRow>();
        }
    }

    /// <summary>
    /// ⚡ LOT 1 : Supprime du bucket Storage 'quiz_photos' tous les fichiers
    /// référencés par les questions de cette série.
    ///
    /// Doit être appelé AVANT la RPC delete_series_safe (sinon les FK CASCADE
    /// vont déjà avoir effacé les questions et on n'a plus les URLs).
    ///
    /// Stratégie : on lit les photo_url, on extrait le path relatif au bucket,
    /// on appelle bucket.Remove(paths). Échec silencieux non bloquant.
    /// </summary>
    private async Task DeleteSeriesQuizPhotosFromStorageAsync(string seriesId)
    {
        // 1) Récupérer les URLs des photos des questions de cette série
        var questions = await GetQuizQuestionsAsync(seriesId);
        if (questions == null || questions.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Pas de questions quiz pour la série {seriesId}, rien à nettoyer.");
            return;
        }

        // 2) Extraire les paths à partir des URLs publiques
        //    Format URL : https://xxx.supabase.co/storage/v1/object/public/quiz_photos/{userId}/{guid}.jpg
        //    On veut récupérer "{userId}/{guid}.jpg"
        var pathsToDelete = new List<string>();
        const string marker = "/quiz_photos/";

        foreach (var q in questions)
        {
            if (string.IsNullOrEmpty(q.PhotoUrl)) continue;

            try
            {
                var idx = q.PhotoUrl.IndexOf(marker);
                if (idx < 0) continue;
                var path = q.PhotoUrl.Substring(idx + marker.Length);
                if (!string.IsNullOrEmpty(path))
                    pathsToDelete.Add(path);
            }
            catch
            {
                // Si l'URL est mal formée, on l'ignore silencieusement
            }
        }

        if (pathsToDelete.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesService] Aucune photo quiz à supprimer du bucket pour série {seriesId}.");
            return;
        }

        // 3) Suppression en lot
        await _supabase.Storage.From("quiz_photos").Remove(pathsToDelete);
        System.Diagnostics.Debug.WriteLine(
            $"[SeriesService] {pathsToDelete.Count} photo(s) quiz supprimée(s) du bucket pour série {seriesId}.");
    }

    private static string GenerateAccessCode()
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
// ═════════════════════════════════════════════════════════════════════
// ⚡ Résultat structuré d'une opération de suppression de série.
// Reflète exactement le JSON renvoyé par la RPC delete_series_safe.
// ═════════════════════════════════════════════════════════════════════
public class DeleteSeriesResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DeletedTitle { get; set; }
    public string? DeletedStatus { get; set; }
}