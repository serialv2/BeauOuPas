using BeauOuPas.Models;
using Newtonsoft.Json.Linq;

namespace BeauOuPas.Services;

/// <summary>
/// Service côté joueur/animateur pour le mode PARTIE RAPIDE (party game
/// type "Most Likely To").
///
/// Calqué sur QuizService (mêmes conventions, mêmes patterns Supabase),
/// mais 100% indépendant : ce fichier ne modifie PAS QuizService et ne
/// casse rien de l'existant. Il réutilise l'infra commune (series,
/// series_participants, quiz_questions) et les tables/RPC party_*.
///
/// Patterns clés :
/// - JoinSeriesAsync                  : ajoute le user à series_participants (idempotent)
/// - LeaveSeriesAsync                 : retire le user (au "Quitter")
/// - GetParticipantCountAsync         : compte les joueurs connectés
/// - GetQuestionsAsync                : liste les questions de la partie (quiz_questions réutilisée)
/// - GetCurrentQuestionForPlayerAsync : RPC get_party_question_for_tv (question + joueurs = options)
/// - HasUserVotedAsync                : true si le user a déjà voté à la question
/// - SubmitVoteAsync                  : RPC submit_party_vote (auto-vote AUTORISÉ)
/// - GetQuestionResultsAsync          : RPC get_party_question_results (détail d'une question)
/// - GetFinalResultsAsync             : RPC get_party_final_results (top3 + leaderboard)
/// - GetRecapAsync                    : RPC get_party_recap (récap final question par question)
/// - GetCategoriesAsync               : RPC get_party_categories (picker dynamique)
/// - CreatePartyAsync                 : RPC create_party_series (création + tirage aléatoire)
/// </summary>
public class PartyService
{
    private readonly Supabase.Client _supabase;

    public PartyService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ─── Rejoindre une partie ────────────────────────────────────
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
            System.Diagnostics.Debug.WriteLine($"[PartyService] JoinSeries: {ex.Message}");
            return false;
        }
    }

    // ─── Quitter une partie ──────────────────────────────────────
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
                $"[PartyService] User {userId} a quitté la partie {seriesId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyService] LeaveSeries: {ex.Message}");
            return false;
        }
    }

    // ─── Compteurs ───────────────────────────────────────────────
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

    // ─── Liste des questions de la partie ────────────────────────
    // (le party réutilise la table quiz_questions, peuplée à la
    //  création par create_party_series)
    public async Task<List<QuizQuestion>> GetQuestionsAsync(string seriesId)
    {
        try
        {
            var result = await _supabase.From<QuizQuestion>()
                .Where(q => q.SeriesId == seriesId)
                .Order(q => q.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyService] GetQuestions: {ex.Message}");
            return new();
        }
    }

    // ─── Question courante pour le joueur ────────────────────────
    // RPC get_party_question_for_tv : renvoie la question + la liste
    // des joueurs connectés (= les options de vote).
    public async Task<JObject?> GetCurrentQuestionForPlayerAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_party_question_for_tv",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId }
                });
            if (res == null) return null;

            var content = res.Content;
            if (string.IsNullOrEmpty(content)) return null;
            return JObject.Parse(content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] GetCurrentQuestionForPlayer: {ex.Message}");
            return null;
        }
    }

    // ─── A déjà voté ? ───────────────────────────────────────────
    public async Task<bool> HasUserVotedAsync(string seriesId, string questionId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var existing = await _supabase.From<PartyAnswer>()
                .Where(a => a.SeriesId == seriesId
                            && a.QuestionId == questionId
                            && a.VoterId == userId)
                .Get();
            return existing.Models.Any();
        }
        catch { return false; }
    }

    // ─── Soumettre un vote ───────────────────────────────────────
    // RPC submit_party_vote : auto-vote AUTORISÉ (le joueur peut voter
    // pour lui-même). L'upsert côté SQL gère le changement de cible.
    public async Task<(bool Success, string Error)> SubmitVoteAsync(
        string seriesId,
        string questionId,
        string targetUserId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "not_authenticated");

            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] SubmitVote: series={seriesId}, question={questionId}, target={targetUserId}");

            var res = await _supabase.Rpc("submit_party_vote",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId },
                    { "p_question_id", questionId },
                    { "p_target_user_id", targetUserId }
                });

            var content = res?.Content;
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] SubmitVote RAW: {content}");
            if (string.IsNullOrEmpty(content))
                return (false, "empty_response");

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;
            var errorMsg = json.Value<string>("error") ?? string.Empty;
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] SubmitVote PARSED: success={success}, error='{errorMsg}'");
            return (success, errorMsg);
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[PartyService] SubmitVote FAILED: {msg}");
            return (false, msg);
        }
    }

    // ─── Résultats d'une question (détail des votes) ─────────────
    public async Task<JObject?> GetQuestionResultsAsync(string questionId)
    {
        try
        {
            var res = await _supabase.Rpc("get_party_question_results",
                new Dictionary<string, object>
                {
                    { "p_question_id", questionId }
                });
            if (res == null) return null;

            var content = res.Content;
            if (string.IsNullOrEmpty(content)) return null;
            return JObject.Parse(content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] GetQuestionResults: {ex.Message}");
            return null;
        }
    }

    // ─── Résultats finaux (top3 + leaderboard) ───────────────────
    public async Task<JObject?> GetFinalResultsAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_party_final_results",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId }
                });
            if (res == null) return null;

            var content = res.Content;
            if (string.IsNullOrEmpty(content)) return null;
            return JObject.Parse(content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] GetFinalResults: {ex.Message}");
            return null;
        }
    }

    // ─── Récap final question par question ───────────────────────
    public async Task<JObject?> GetRecapAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_party_recap",
                new Dictionary<string, object>
                {
                    { "p_series_id", seriesId }
                });
            if (res == null) return null;

            var content = res.Content;
            if (string.IsNullOrEmpty(content)) return null;
            return JObject.Parse(content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyService] GetRecap: {ex.Message}");
            return null;
        }
    }

    // ─── Catégories disponibles (picker dynamique, option B) ─────
    // Renvoie la liste des catégories distinctes actives pour la
    // langue donnée (avec fallback 'fr' côté SQL).
    public async Task<List<PartyCategory>> GetCategoriesAsync(string lang)
    {
        var result = new List<PartyCategory>();
        try
        {
            var res = await _supabase.Rpc("get_party_categories",
                new Dictionary<string, object>
                {
                    { "p_lang", lang }
                });
            var content = res?.Content;
            if (string.IsNullOrEmpty(content)) return result;

            var json = JObject.Parse(content);
            if (!(json.Value<bool?>("success") ?? false)) return result;

            var arr = json["categories"] as JArray;
            if (arr == null) return result;

            foreach (var c in arr)
            {
                result.Add(new PartyCategory
                {
                    Category = c.Value<string>("category") ?? string.Empty,
                    Count = c.Value<int?>("count") ?? 0
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] GetCategories: {ex.Message}");
        }
        return result;
    }

    // ─── Créer une partie (RPC create_party_series) ──────────────
    // Tirage aléatoire de p_count questions côté serveur, atomique.
    // p_category = null → "Mélange" (toutes catégories).
    public async Task<(bool Success, string ErrorCode, string? SeriesId,
        string? AccessCode, int QuestionsCount)> CreatePartyAsync(
            string title,
            string? description,
            int count,
            string lang,
            string? category,
            string? groupId,
            bool showFullLeaderboard)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "unauthenticated", null, null, 0);

            var parameters = new Dictionary<string, object>
            {
                { "p_user_id", userId },
                { "p_title", title },
                { "p_description", description ?? string.Empty },
                { "p_count", count },
                { "p_lang", lang },
                { "p_show_full_leaderboard", showFullLeaderboard }
            };

            // "Mélange" → on envoie le sentinel '__all__' compris par la RPC
            parameters["p_category"] =
                string.IsNullOrEmpty(category) ? "__all__" : category;

            if (!string.IsNullOrEmpty(groupId))
                parameters["p_group_id"] = groupId;
            else
                parameters["p_group_id"] = null!;

            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] CreateParty: title={title}, count={count}, lang={lang}, cat={category ?? "__all__"}");

            var res = await _supabase.Rpc("create_party_series", parameters);

            var content = res?.Content;
            if (string.IsNullOrEmpty(content))
                return (false, "empty_response", null, null, 0);

            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] CreateParty raw: {content}");

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;

            if (!success)
            {
                var errorCode = json.Value<string>("error") ?? "unknown_error";
                System.Diagnostics.Debug.WriteLine(
                    $"[PartyService] CreateParty FAILED: {errorCode}");
                return (false, errorCode, null, null, 0);
            }

            var seriesId = json.Value<string>("series_id");
            var accessCode = json.Value<string>("access_code");
            var qCount = json.Value<int?>("questions_count") ?? 0;

            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] CreateParty OK: series_id={seriesId}, code={accessCode}, q={qCount}");

            return (true, string.Empty, seriesId, accessCode, qCount);
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(
                $"[PartyService] CreateParty EXCEPTION: {msg}");
            return (false, "exception", null, null, 0);
        }
    }
}

/// <summary>
/// Catégorie de questions party (pour le picker dynamique).
/// </summary>
public class PartyCategory
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}
