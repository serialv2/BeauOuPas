using BeauOuPas.Models;
using BeauOuPas.ViewModels.Groups;
using Newtonsoft.Json.Linq;

namespace BeauOuPas.Services;

/// <summary>
/// Service côté joueur pour le mode QUIZZ.
/// Équivalent de SeriesVoteService mais pour les quiz_questions / quiz_answers.
///
/// Patterns clés :
/// - JoinSeriesAsync                 : ajoute le user à series_participants (idempotent)
/// - LeaveSeriesAsync                : retire le user (au "Quitter")
/// - GetParticipantCountAsync        : compte les joueurs (compteur "X/Y ont répondu")
/// - GetQuestionsAsync               : liste les questions de la série (sans is_correct)
/// - GetQuestionAnswerCountAsync     : nb de réponses arrivées pour une question donnée
/// - HasUserAnsweredAsync            : true si le user a déjà répondu à la question
/// - SubmitAnswerAsync               : RPC submit_quiz_answer (anti-triche, scoring server)
/// - GetCurrentQuestionForPlayerAsync: RPC get_quiz_question_for_tv (options sans is_correct)
/// - GetQuestionResultsAsync         : RPC get_quiz_question_results (reveal avec is_correct)  ← Phase 2
/// - GetFinalResultsAsync            : RPC get_quiz_final_results (leaderboard final)          ← Phase 2
/// - CreateQuizAsync                 : RPC create_quiz_series (création complète)
/// - ReplaceQuizQuestionsAsync       : RPC replace_quiz_questions (édition des questions)
/// - LoadQuizQuestionsForEditAsync   : charge questions+options pour EditQuizQuestionsPage
/// </summary>
public class QuizService
{
    private readonly Supabase.Client _supabase;

    public QuizService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ─── Rejoindre une série ─────────────────────────────────────
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] JoinSeries: {ex.Message}");
            return false;
        }
    }

    // ─── Quitter une série ──────────────────────────────────────
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
                $"[QuizService] User {userId} a quitté le quiz {seriesId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizService] LeaveSeries: {ex.Message}");
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

    public async Task<int> GetQuestionAnswerCountAsync(string questionId)
    {
        try
        {
            var result = await _supabase.From<QuizAnswer>()
                .Where(a => a.QuestionId == questionId).Get();
            return result.Models.Count;
        }
        catch { return 0; }
    }

    public async Task<bool> HasUserAnsweredAsync(string questionId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;

            var existing = await _supabase.From<QuizAnswer>()
                .Where(a => a.QuestionId == questionId && a.UserId == userId)
                .Get();
            return existing.Models.Any();
        }
        catch { return false; }
    }

    // ─── Liste des questions ────────────────────────────────────
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] GetQuestions: {ex.Message}");
            return new();
        }
    }

    // ─── Récupérer la question courante pour le joueur ──────────
    public async Task<JObject?> GetCurrentQuestionForPlayerAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_quiz_question_for_tv", new Dictionary<string, object>
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] GetCurrentQuestionForPlayer: {ex.Message}");
            return null;
        }
    }

    // ─── Soumettre une réponse ──────────────────────────────────
    public async Task<(bool Success, string Error, bool IsCorrect)> SubmitAnswerAsync(
        string questionId,
        List<string> selectedOptionIds,
        int responseTimeMs)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "Utilisateur non authentifié", false);

            System.Diagnostics.Debug.WriteLine(
                $"[QuizService] Submit: question={questionId}, options=[{string.Join(",", selectedOptionIds)}], time={responseTimeMs}ms");

            var res = await _supabase.Rpc("submit_quiz_answer", new Dictionary<string, object>
            {
                { "p_user_id", userId },
                { "p_question_id", questionId },
                { "p_selected_option_ids", selectedOptionIds },
                { "p_response_time_ms", responseTimeMs }
            });

            var content = res?.Content;
            if (string.IsNullOrEmpty(content))
                return (false, "Réponse vide du serveur", false);

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;
            var errorMsg = json.Value<string>("error") ?? string.Empty;
            var isCorrect = json.Value<bool?>("is_correct") ?? false;

            return (success, errorMsg, isCorrect);
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[QuizService] Submit FAILED: {msg}");
            return (false, msg, false);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ PHASE 2 — RÉCUPÉRATION DES RÉSULTATS (reveal + finish)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🔧 PHASE 2 : Récupère les résultats d'une question (avec is_correct sur chaque option).
    /// Utilisé par QuizPlayPage en phase REVEAL pour afficher la bonne réponse au joueur.
    /// 
    /// La RPC get_quiz_question_results retourne typiquement :
    /// {
    ///   "question_id": "...",
    ///   "title": "...",
    ///   "question_text": "...",
    ///   "options": [
    ///     { "id": "...", "text": "...", "is_correct": true/false, "vote_count": N }
    ///   ]
    /// }
    /// </summary>
    public async Task<JObject?> GetQuestionResultsAsync(string questionId)
    {
        try
        {
            var res = await _supabase.Rpc("get_quiz_question_results", new Dictionary<string, object>
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] GetQuestionResults: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 🔧 PHASE 2 : Récupère les résultats finaux du quiz (top 3 + leaderboard complet).
    /// Utilisé par QuizPlayPage en phase FINISH pour afficher le classement et le rang du joueur.
    /// 
    /// La RPC get_quiz_final_results retourne typiquement :
    /// {
    ///   "top3":        [ { user_id, username, avatar_url, score } x3 ],
    ///   "leaderboard": [ { user_id, username, avatar_url, score } x N ]
    /// }
    /// </summary>
    public async Task<JObject?> GetFinalResultsAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_quiz_final_results", new Dictionary<string, object>
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] GetFinalResults: {ex.Message}");
            return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // CRÉER UN QUIZ COMPLET (titre + questions + options)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Appelle la RPC create_quiz_series pour créer un quiz complet
    /// avec ses questions et ses options en une seule transaction.
    ///
    /// 🔧 FIX E2d : utilise des objets natifs C# (List&lt;Dictionary&gt;) au lieu d'un
    /// JArray.ToString() pour que Supabase sérialise correctement le tableau JSONB côté SQL.
    /// </summary>
    public async Task<(bool Success, string ErrorCode, string? SeriesId, string? AccessCode)> CreateQuizAsync(
        string? groupId,
        string title,
        string? description,
        string quizStyle,
        bool showFullLeaderboard,
        List<QuizQuestionItem> questions)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "unauthenticated", null, null);

            // Construction du payload en objets natifs C#
            var questionsList = new List<Dictionary<string, object?>>();
            foreach (var q in questions)
            {
                var optionsList = new List<Dictionary<string, object?>>();
                foreach (var opt in q.Options)
                {
                    optionsList.Add(new Dictionary<string, object?>
                    {
                        { "text", opt.Text ?? string.Empty },
                        { "is_correct", opt.IsCorrect }
                    });
                }

                var qDict = new Dictionary<string, object?>
                {
                    { "title", q.Title ?? string.Empty },
                    { "question_text", q.QuestionText ?? string.Empty },
                    { "options", optionsList }
                };

                if (!string.IsNullOrEmpty(q.PhotoUrl))
                    qDict["photo_url"] = q.PhotoUrl;

                questionsList.Add(qDict);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[QuizService] CreateQuiz: title={title}, questions={questions.Count}, style={quizStyle}");

            var parameters = new Dictionary<string, object>
            {
                { "p_user_id", userId },
                { "p_title", title },
                { "p_description", description ?? string.Empty },
                { "p_quiz_style", quizStyle },
                { "p_show_full_leaderboard", showFullLeaderboard },
                { "p_questions", questionsList }
            };

            if (!string.IsNullOrEmpty(groupId))
                parameters["p_group_id"] = groupId;
            else
                parameters["p_group_id"] = null!;

            var res = await _supabase.Rpc("create_quiz_series", parameters);

            var content = res?.Content;
            if (string.IsNullOrEmpty(content))
                return (false, "empty_response", null, null);

            System.Diagnostics.Debug.WriteLine($"[QuizService] CreateQuiz raw response: {content}");

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;

            if (!success)
            {
                var errorCode = json.Value<string>("error") ?? "unknown_error";
                System.Diagnostics.Debug.WriteLine($"[QuizService] CreateQuiz FAILED: {errorCode}");
                return (false, errorCode, null, null);
            }

            var seriesId = json.Value<string>("series_id");
            var accessCode = json.Value<string>("access_code");

            System.Diagnostics.Debug.WriteLine(
                $"[QuizService] CreateQuiz OK: series_id={seriesId}, access_code={accessCode}");

            return (true, string.Empty, seriesId, accessCode);
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[QuizService] CreateQuiz EXCEPTION: {msg}");
            return (false, "exception", null, null);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 🔧 LOT 1.5 : MODIFIER LES QUESTIONS D'UN QUIZ EXISTANT
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Appelle la RPC replace_quiz_questions pour remplacer toutes les
    /// questions d'un quiz existant en une seule transaction.
    /// 
    /// Conditions côté serveur :
    ///  - L'user doit être le créateur de la série
    ///  - La série doit être un quiz (is_quiz = true)
    ///  - La série doit être en status 'preparing'
    /// 
    /// Codes d'erreur retournés :
    ///  unauthenticated, series_not_found, not_creator, not_a_quiz,
    ///  series_not_editable, questions_invalid, no_questions,
    ///  too_many_questions, question_title_required, question_text_required,
    ///  options_invalid, options_count_invalid, option_text_required,
    ///  option_text_too_long, no_correct_answer, sql_error
    /// </summary>
    public async Task<(bool Success, string ErrorCode)> ReplaceQuizQuestionsAsync(
        string seriesId,
        List<QuizQuestionItem> questions)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return (false, "unauthenticated");

            var questionsList = new List<Dictionary<string, object?>>();
            foreach (var q in questions)
            {
                var optionsList = new List<Dictionary<string, object?>>();
                foreach (var opt in q.Options)
                {
                    optionsList.Add(new Dictionary<string, object?>
                    {
                        { "text", opt.Text ?? string.Empty },
                        { "is_correct", opt.IsCorrect }
                    });
                }

                var qDict = new Dictionary<string, object?>
                {
                    { "title", q.Title ?? string.Empty },
                    { "question_text", q.QuestionText ?? string.Empty },
                    { "options", optionsList }
                };

                if (!string.IsNullOrEmpty(q.PhotoUrl))
                    qDict["photo_url"] = q.PhotoUrl;

                questionsList.Add(qDict);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[QuizService] ReplaceQuestions: series={seriesId}, questions={questions.Count}");

            var parameters = new Dictionary<string, object>
            {
                { "p_series_id", seriesId },
                { "p_user_id", userId },
                { "p_questions", questionsList }
            };

            var res = await _supabase.Rpc("replace_quiz_questions", parameters);

            var content = res?.Content;
            if (string.IsNullOrEmpty(content))
                return (false, "empty_response");

            System.Diagnostics.Debug.WriteLine($"[QuizService] ReplaceQuestions raw: {content}");

            var json = JObject.Parse(content);
            var success = json.Value<bool?>("success") ?? false;

            if (!success)
            {
                var errorCode = json.Value<string>("error") ?? "unknown_error";
                System.Diagnostics.Debug.WriteLine($"[QuizService] ReplaceQuestions FAILED: {errorCode}");
                return (false, errorCode);
            }

            System.Diagnostics.Debug.WriteLine($"[QuizService] ReplaceQuestions OK");
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[QuizService] ReplaceQuestions EXCEPTION: {msg}");
            return (false, "exception");
        }
    }

    /// <summary>
    /// 🔧 LOT 1.5 : Charge les questions + leurs options d'un quiz existant
    /// pour les afficher dans EditQuizQuestionsPage.
    /// </summary>
    public async Task<List<QuizQuestionItem>> LoadQuizQuestionsForEditAsync(string seriesId)
    {
        var result = new List<QuizQuestionItem>();
        try
        {
            var questions = await _supabase.From<QuizQuestion>()
                .Where(q => q.SeriesId == seriesId)
                .Order(q => q.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();

            if (!questions.Models.Any()) return result;

            var questionIds = questions.Models.Select(q => q.Id).ToList();

            var options = await _supabase.From<QuizQuestionOption>()
                .Filter("question_id", Postgrest.Constants.Operator.In, questionIds)
                .Order(o => o.Position, Postgrest.Constants.Ordering.Ascending)
                .Get();

            foreach (var q in questions.Models)
            {
                var item = new QuizQuestionItem
                {
                    Id = q.Id,
                    Title = q.Title,
                    QuestionText = q.QuestionText,
                    PhotoUrl = q.PhotoUrl,
                    Options = options.Models
                        .Where(o => o.QuestionId == q.Id)
                        .OrderBy(o => o.Position)
                        .Select(o => new QuizQuestionOptionItem
                        {
                            Text = o.OptionText ?? string.Empty,
                            IsCorrect = o.IsCorrect
                        })
                        .ToList()
                };
                item.OptionsCount = item.Options.Count;
                item.HasCorrectAnswer = item.Options.Any(o => o.IsCorrect);
                result.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizService] LoadQuizQuestionsForEdit: {ex.Message}");
        }
        return result;
    }

    /// <summary>
    /// 📊 SUJET 3 : Récupère les statistiques détaillées d'un quiz terminé.
    /// 
    /// La RPC get_quiz_detailed_stats retourne :
    /// {
    ///   "totals": { players, questions, answers, correct, success_rate },
    ///   "hardest_question": { position, title, question_text, success_rate, ... } | null,
    ///   "easiest_question": { position, title, question_text, success_rate, ... } | null,
    ///   "gender_breakdown": [ { key:"male"|"female"|"other"|"unknown", count, percent, success_rate, ... } ],
    ///   "age_breakdown":    [ { key:"0_17"|"18_29"|"30_49"|"50_plus"|"unknown", count, percent, success_rate, ... } ]
    /// }
    /// 
    /// Utilisé par QuizPlayViewModel.LoadDetailedStatsAsync pour alimenter
    /// la section stats sur l'écran finish.
    /// </summary>
    public async Task<JObject?> GetDetailedStatsAsync(string seriesId)
    {
        try
        {
            var res = await _supabase.Rpc("get_quiz_detailed_stats", new Dictionary<string, object>
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
            System.Diagnostics.Debug.WriteLine($"[QuizService] GetDetailedStats: {ex.Message}");
            return null;
        }
    }
}
