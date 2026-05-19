using Supabase;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace BeauOuPas.Services;

/// <summary>
/// ⚡ Service Realtime DÉDIÉ au mode TV (séries de votes ET de quizz synchronisées).
///
/// Crée un client Supabase SÉPARÉ avec AutoConnectRealtime = true,
/// pour ne PAS interférer avec le client principal (SupabaseService) qui
/// reste en AutoConnectRealtime = false.
///
/// Modes :
///   - 'vote' (défaut, rétro-compat) : écoute series + series_projects + series_votes + series_participants
///   - 'quiz'                        : écoute series + quiz_questions + quiz_answers + series_participants
///
/// Les callbacks OnSeriesProjectChanged / OnVoteReceived restent côté 'vote'.
/// Les callbacks OnQuizQuestionChanged / OnQuizAnswerReceived sont côté 'quiz'.
/// OnSeriesChanged et OnParticipantJoined sont communs aux deux modes.
/// </summary>
public class SeriesRealtimeService : IDisposable
{
    private static Supabase.Client? _realtimeClient;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    private RealtimeChannel? _seriesChannel;
    private RealtimeChannel? _projectsChannel;          // mode 'vote' uniquement
    private RealtimeChannel? _participantsChannel;
    private RealtimeChannel? _votesChannel;             // mode 'vote' uniquement
    private RealtimeChannel? _quizQuestionsChannel;     // mode 'quiz' uniquement
    private RealtimeChannel? _quizAnswersChannel;       // mode 'quiz' uniquement

    private string? _currentSeriesId;
    private string _currentMode = "vote";   // "vote" | "quiz"
    private bool _isDisposed = false;

    // ─── Callbacks communs aux deux modes ─────────────────────────

    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnSeriesChanged;
    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnParticipantJoined;

    // ─── Callbacks mode 'vote' ────────────────────────────────────

    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnSeriesProjectChanged;
    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnVoteReceived;

    // ─── Callbacks mode 'quiz' ────────────────────────────────────

    /// <summary>UPDATE sur quiz_questions (= started_at posé/modifié par la TV).</summary>
    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnQuizQuestionChanged;

    /// <summary>INSERT sur quiz_answers (= compteur "X / Y ont répondu").</summary>
    public Action<PostgresChangesPayload<SocketResponsePayload>>? OnQuizAnswerReceived;

    // ─── Initialisation du client Realtime dédié ──────────────────

    private static async Task<Supabase.Client> GetRealtimeClientAsync()
    {
        if (_realtimeClient != null) return _realtimeClient;

        await _initLock.WaitAsync();
        try
        {
            if (_realtimeClient != null) return _realtimeClient;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true
            };

            _realtimeClient = new Supabase.Client(
                Constants.SupabaseUrl,
                Constants.SupabaseKey,
                options
            );

            await _realtimeClient.InitializeAsync();
            System.Diagnostics.Debug.WriteLine(
                "[SeriesRealtime] Client Realtime dédié initialisé");

            return _realtimeClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ─── S'abonner à une série ─────────────────────────────────────
    /// <summary>
    /// mode : "vote" (défaut, rétro-compat) ou "quiz".
    /// </summary>
    public async Task SubscribeAsync(string seriesId, string mode = "vote")
    {
        if (_isDisposed) return;
        if (string.IsNullOrEmpty(seriesId)) return;

        if (_currentSeriesId == seriesId && _currentMode == mode && _seriesChannel != null) return;

        if (_currentSeriesId != null && (_currentSeriesId != seriesId || _currentMode != mode))
        {
            await UnsubscribeAsync();
        }

        _currentSeriesId = seriesId;
        _currentMode = mode;

        try
        {
            var client = await GetRealtimeClientAsync();

            // ─── series : UPDATE filtré par id (commun aux 2 modes) ─
            _seriesChannel = client.Realtime.Channel("series-detail-" + seriesId);
            _seriesChannel.Register(new PostgresChangesOptions(
                "public",
                "series",
                ListenType.Updates,
                "id=eq." + seriesId));

            _seriesChannel.AddPostgresChangeHandler(ListenType.Updates, (sender, change) =>
            {
                try
                {
                    if (change?.Payload != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[SeriesRealtime] series UPDATE reçu");
                        var payload = change.Payload;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try { OnSeriesChanged?.Invoke(payload); }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[SeriesRealtime] OnSeriesChanged callback: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SeriesRealtime] series handler: {ex.Message}");
                }
            });

            await _seriesChannel.Subscribe();
            System.Diagnostics.Debug.WriteLine("[SeriesRealtime] series channel SUBSCRIBED");

            // ─── series_participants : INSERT (commun aux 2 modes) ──
            _participantsChannel = client.Realtime.Channel("series-participants-" + seriesId);
            _participantsChannel.Register(new PostgresChangesOptions(
                "public",
                "series_participants",
                ListenType.Inserts,
                "series_id=eq." + seriesId));

            _participantsChannel.AddPostgresChangeHandler(ListenType.Inserts, (sender, change) =>
            {
                try
                {
                    if (change?.Payload != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[SeriesRealtime] participant INSERT reçu");
                        var payload = change.Payload;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try { OnParticipantJoined?.Invoke(payload); }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[SeriesRealtime] OnParticipantJoined callback: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SeriesRealtime] participants handler: {ex.Message}");
                }
            });

            await _participantsChannel.Subscribe();
            System.Diagnostics.Debug.WriteLine("[SeriesRealtime] series_participants channel SUBSCRIBED");

            // ─── Mode VOTE : series_projects + series_votes ─────────
            if (mode == "vote")
            {
                // series_projects : UPDATE filtré par series_id
                _projectsChannel = client.Realtime.Channel("series-projects-" + seriesId);
                _projectsChannel.Register(new PostgresChangesOptions(
                    "public",
                    "series_projects",
                    ListenType.Updates,
                    "series_id=eq." + seriesId));

                _projectsChannel.AddPostgresChangeHandler(ListenType.Updates, (sender, change) =>
                {
                    try
                    {
                        if (change?.Payload != null)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "[SeriesRealtime] series_projects UPDATE reçu");
                            var payload = change.Payload;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try { OnSeriesProjectChanged?.Invoke(payload); }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[SeriesRealtime] OnSeriesProjectChanged callback: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SeriesRealtime] projects handler: {ex.Message}");
                    }
                });

                await _projectsChannel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[SeriesRealtime] series_projects channel SUBSCRIBED");

                // series_votes : INSERT filtré par series_id
                _votesChannel = client.Realtime.Channel("series-votes-" + seriesId);
                _votesChannel.Register(new PostgresChangesOptions(
                    "public",
                    "series_votes",
                    ListenType.Inserts,
                    "series_id=eq." + seriesId));

                _votesChannel.AddPostgresChangeHandler(ListenType.Inserts, (sender, change) =>
                {
                    try
                    {
                        if (change?.Payload != null)
                        {
                            var payload = change.Payload;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try { OnVoteReceived?.Invoke(payload); }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[SeriesRealtime] OnVoteReceived callback: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SeriesRealtime] votes handler: {ex.Message}");
                    }
                });

                await _votesChannel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[SeriesRealtime] series_votes channel SUBSCRIBED");
            }

            // ─── Mode QUIZ : quiz_questions + quiz_answers ──────────
            if (mode == "quiz")
            {
                // quiz_questions : UPDATE filtré par series_id (= started_at posé)
                _quizQuestionsChannel = client.Realtime.Channel("quiz-questions-" + seriesId);
                _quizQuestionsChannel.Register(new PostgresChangesOptions(
                    "public",
                    "quiz_questions",
                    ListenType.Updates,
                    "series_id=eq." + seriesId));

                _quizQuestionsChannel.AddPostgresChangeHandler(ListenType.Updates, (sender, change) =>
                {
                    try
                    {
                        if (change?.Payload != null)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "[SeriesRealtime] quiz_questions UPDATE reçu");
                            var payload = change.Payload;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try { OnQuizQuestionChanged?.Invoke(payload); }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[SeriesRealtime] OnQuizQuestionChanged callback: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SeriesRealtime] quiz_questions handler: {ex.Message}");
                    }
                });

                await _quizQuestionsChannel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[SeriesRealtime] quiz_questions channel SUBSCRIBED");

                // quiz_answers : INSERT filtré par series_id (compteur "X/Y")
                _quizAnswersChannel = client.Realtime.Channel("quiz-answers-" + seriesId);
                _quizAnswersChannel.Register(new PostgresChangesOptions(
                    "public",
                    "quiz_answers",
                    ListenType.Inserts,
                    "series_id=eq." + seriesId));

                _quizAnswersChannel.AddPostgresChangeHandler(ListenType.Inserts, (sender, change) =>
                {
                    try
                    {
                        if (change?.Payload != null)
                        {
                            var payload = change.Payload;
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try { OnQuizAnswerReceived?.Invoke(payload); }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[SeriesRealtime] OnQuizAnswerReceived callback: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SeriesRealtime] quiz_answers handler: {ex.Message}");
                    }
                });

                await _quizAnswersChannel.Subscribe();
                System.Diagnostics.Debug.WriteLine("[SeriesRealtime] quiz_answers channel SUBSCRIBED");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesRealtime] SubscribeAsync ERROR: {ex.Message}");
        }
    }

    // ─── Se désabonner ─────────────────────────────────────────────

    public async Task UnsubscribeAsync()
    {
        try
        {
            if (_seriesChannel != null)
            {
                _seriesChannel.Unsubscribe();
                _seriesChannel = null;
            }
            if (_projectsChannel != null)
            {
                _projectsChannel.Unsubscribe();
                _projectsChannel = null;
            }
            if (_participantsChannel != null)
            {
                _participantsChannel.Unsubscribe();
                _participantsChannel = null;
            }
            if (_votesChannel != null)
            {
                _votesChannel.Unsubscribe();
                _votesChannel = null;
            }
            if (_quizQuestionsChannel != null)
            {
                _quizQuestionsChannel.Unsubscribe();
                _quizQuestionsChannel = null;
            }
            if (_quizAnswersChannel != null)
            {
                _quizAnswersChannel.Unsubscribe();
                _quizAnswersChannel = null;
            }

            _currentSeriesId = null;

            System.Diagnostics.Debug.WriteLine("[SeriesRealtime] Toutes les subscriptions fermées");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesRealtime] UnsubscribeAsync ERROR: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        OnSeriesChanged = null;
        OnSeriesProjectChanged = null;
        OnParticipantJoined = null;
        OnVoteReceived = null;
        OnQuizQuestionChanged = null;
        OnQuizAnswerReceived = null;
        _ = UnsubscribeAsync();
    }
}
