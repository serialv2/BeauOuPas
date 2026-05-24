using BeauOuPas.Localization;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using System.Collections.ObjectModel;

namespace BeauOuPas.ViewModels.Groups;

/// <summary>
/// 🎉 Écran joueur — MODE "PARTIE RAPIDE" (party game type "Most Likely To").
///
/// Page joueur d'une partie rapide. Calquée sur QuizPlayViewModel (même
/// cycle de vie, mêmes conventions MVVM CommunityToolkit, même intégration
/// Shell via [QueryProperty]) MAIS 100% indépendante : ce fichier ne modifie
/// PAS QuizPlayViewModel, PAS QuizService, PAS SeriesRealtimeService, et ne
/// casse rien de l'existant.
///
/// Différences clés avec le quiz :
///  - PAS de bonne réponse, PAS de reveal côté téléphone, PAS de stats joueur
///    (les résultats détaillés sont affichés sur la TV uniquement, comme on
///     l'a décidé pour le party).
///  - Les "options" ne sont pas A/B/C/D fixes mais la LISTE DES JOUEURS
///    connectés (pseudo + avatar). Le joueur tape sur un pseudo pour voter.
///  - Auto-vote AUTORISÉ : le joueur peut voter pour lui-même (son propre
///    pseudo apparaît dans la liste, c'est voulu).
///  - La question courante + la liste des joueurs viennent de la RPC
///    get_party_question_for_tv (exposée par PartyService.GetCurrentQuestionForPlayerAsync).
///    Structure JSON renvoyée :
///       { success, vote_duration_seconds,
///         question:{ id, position, title, question_text, photo_url,
///                    started_at, options:[ {user_id, username, avatar_url} ] } }
///
/// Realtime : on réutilise SeriesRealtimeService en mode "quiz" (sans le
/// modifier) pour écouter series / series_participants / quiz_questions.
/// Le service n'écoute PAS party_answers, mais ce n'est pas un problème ici :
/// le joueur n'a pas besoin du compteur de votes en direct (c'est la TV qui
/// l'affiche). Un poll léger via la RPC suffit pour suivre l'avancement.
///
/// Machine à états (driven par series.status + quiz_questions[idx].started_at) :
///   LOBBY     : series.status = 'preparing'
///               → "En attente du lancement de la partie…" + compteur joueurs
///   INTRO     : status = 'active', Q[0].started_at == null
///               → "La partie va commencer…" (attend que la TV démarre Q1)
///   QUESTION  : status = 'active', Q[idx].started_at != null, pas encore voté
///               → texte de la question + liste des joueurs (boutons) + countdown
///   VOTED     : le joueur a voté
///               → "Vote envoyé ✓ — en attente des autres"
///   FINISH    : status = 'finished'
///               → "Partie terminée ! Résultats sur l'écran TV 📺"
///   ERROR     : code invalide / TV désactivée / etc.
/// </summary>
[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class PartyPlayViewModel : ObservableObject, IDisposable
{
    private readonly PartyService _partyService;
    private readonly SeriesService _seriesService;
    private readonly SeriesRealtimeService _realtime;
    private readonly IAdService _adService;

    // ─── Init / cleanup ──────────────────────────────────────────
    private bool _isInitializing = false;
    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private readonly object _initLock = new();

    // ─── Données chargées ─────────────────────────────────────────
    private Series? _series;
    private List<QuizQuestion> _questions = new();

    // ─── Timers ───────────────────────────────────────────────────
    private IDispatcherTimer? _countdownTimer;
    private IDispatcherTimer? _pollTimer;
    private DateTime? _currentQuestionStartedAt;

    // ─── Tracking du vote en cours ───────────────────────────────
    private string? _currentQuestionId;
    private bool _hasVotedCurrent = false;

    public PartyPlayViewModel(
        PartyService partyService,
        SeriesService seriesService,
        SeriesRealtimeService realtime,
        IAdService adService)
    {
        _partyService = partyService;
        _seriesService = seriesService;
        _realtime = realtime;
        _adService = adService;

        _realtime.OnSeriesChanged = OnRealtimeSeriesChanged;
        _realtime.OnQuizQuestionChanged = OnRealtimeQuestionChanged;
        _realtime.OnParticipantJoined = OnRealtimeParticipantJoined;
    }

    // ═══════════════════════════════════════════════════════════════
    // Paramètres reçus via Shell
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // État UI — un seul écran visible à la fois (IsXxx)
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isLobby = false;
    [ObservableProperty] private bool _isIntro = false;
    [ObservableProperty] private bool _isQuestion = false;
    [ObservableProperty] private bool _isVoted = false;
    [ObservableProperty] private bool _isFinish = false;
    [ObservableProperty] private bool _isError = false;
    [ObservableProperty] private string _errorTitle = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Pause depuis la TV
    [ObservableProperty] private bool _isPaused = false;

    // ═══════════════════════════════════════════════════════════════
    // Lobby / Intro
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _partyTitle = string.Empty;
    [ObservableProperty] private string _partyDescription = string.Empty;
    [ObservableProperty] private int _totalQuestions = 0;
    [ObservableProperty] private int _participantCount = 0;
    [ObservableProperty] private string _participantCountLabel = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // Question en cours
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private int _currentIndex = 0;
    [ObservableProperty] private string _questionPositionLabel = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;

    [ObservableProperty] private int _countdown = 0;
    [ObservableProperty] private string _countdownColor = "#C2754C";

    /// <summary>
    /// Les "options" = les joueurs connectés. Chaque item est un gros bouton
    /// tappable affichant le pseudo (et l'avatar si dispo). Tap → vote.
    /// </summary>
    public ObservableCollection<PartyPlayerItem> Players { get; } = new();

    // ═══════════════════════════════════════════════════════════════
    // Écran "Vote envoyé"
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _votedForName = string.Empty;
    [ObservableProperty] private string _votedMessage = string.Empty;

    /// <summary>
    /// True si on a un pseudo pour qui on a voté (utilisé pour l'affichage
    /// conditionnel sans dépendre d'un converter string→bool externe).
    /// Recalculé automatiquement quand VotedForName change.
    /// </summary>
    public bool HasVotedForName => !string.IsNullOrWhiteSpace(VotedForName);

    // Oubli i18n : libellé "Tu as voté pour : {0}" traduit (StringFormat retiré du XAML)
    public string VotedForLabel => L.F("Party_VotedFor", VotedForName);

    partial void OnVotedForNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasVotedForName));
        OnPropertyChanged(nameof(VotedForLabel));
    }

    // ═══════════════════════════════════════════════════════════════
    // INIT — Appelé depuis OnAppearing()
    // ═══════════════════════════════════════════════════════════════

    public async Task InitAsync()
    {
        lock (_initLock)
        {
            if (_isDisposed) return;
            if (_isInitialized || _isInitializing)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PartyPlay] InitAsync ignoré (initializing={_isInitializing}, initialized={_isInitialized})");
                return;
            }
            _isInitializing = true;
        }

        try
        {
            IsLoading = true;
            HideAllScreens();

            // 1) Rejoindre la partie (idempotent)
            await _partyService.JoinSeriesAsync(SeriesId);

            // 2) Charger l'état de la série
            _series = await _seriesService.GetSeriesAsync(SeriesId);
            if (_series == null)
            {
                ShowError(L.T("Common_Error"), L.T("Quiz_Error_SeriesNotFound"));
                return;
            }

            PartyTitle = _series.Title;
            PartyDescription = _series.Description ?? string.Empty;
            IsPaused = _series.TvPaused;
            CurrentIndex = _series.CurrentProjectIndex;

            // 3) Charger les questions (réutilise quiz_questions, peuplée à la création)
            _questions = await _partyService.GetQuestionsAsync(SeriesId);
            TotalQuestions = _questions.Count;

            // 4) Charger compteur participants
            ParticipantCount = await _partyService.GetParticipantCountAsync(SeriesId);
            UpdateParticipantLabel();

            System.Diagnostics.Debug.WriteLine(
                $"[PartyPlay] Init : status={_series.Status}, idx={CurrentIndex}, " +
                $"questions={_questions.Count}, participants={ParticipantCount}");

            _isInitialized = true;

            // 5) S'abonner au realtime AVANT le premier render
            //    (mode "quiz" réutilisé : écoute series + series_participants
            //     + quiz_questions ; on ne touche pas SeriesRealtimeService)
            await _realtime.SubscribeAsync(SeriesId, "quiz");

            // 5bis) Précharge systématiquement l'interstitielle. Affichée :
            //  - soit sur la transition preparing → active (cas multi-joueurs synchrones)
            //  - soit ici même si on arrive directement en INTRO (cas typique : animateur
            //    qui lance la partie depuis l'écran TV puis navigue vers PartyPlay,
            //    le status est déjà "active" à l'init donc OnRealtimeSeriesChanged
            //    ne voit jamais la transition).
            _ = _adService.LoadInterstitialAsync();

            if (_series.Status == "active"
                && _questions.Count > 0
                && _questions[0].StartedAt == null)
            {
                _ = ShowInterstitialThenSignalAsync();
            }

            // 6) Premier rendu
            RenderCurrentScreen();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] InitAsync: {ex.Message}");
            ShowError(L.T("Common_Error"), ex.Message);
        }
        finally
        {
            IsLoading = false;
            _isInitializing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ROUTEUR PRINCIPAL — décide quel écran afficher
    // (réplique de tv-party-app.js renderCurrentScreen)
    // ═══════════════════════════════════════════════════════════════

    private void RenderCurrentScreen()
    {
        if (_series == null) return;

        IsPaused = _series.TvPaused;

        if (_series.Status == "finished")
        {
            StopAllTimers();
            ShowFinish();
            return;
        }

        if (_series.Status == "preparing")
        {
            StopCountdownTimer();
            ShowLobby();
            return;
        }

        if (_series.Status == "active")
        {
            var firstQ = _questions.Count > 0 ? _questions[0] : null;
            if (firstQ != null && firstQ.StartedAt == null)
            {
                // Partie active mais Q1 pas encore démarrée → INTRO
                if (!IsIntro) ShowIntro();
                return;
            }

            var idx = _series.CurrentProjectIndex;
            if (idx < 0 || idx >= _questions.Count)
            {
                // Index hors borne : sans doute la partie se termine
                ShowLobby();
                return;
            }

            var q = _questions[idx];
            if (q?.StartedAt != null)
            {
                // Nouvelle question ? on reset le flag de vote
                if (_currentQuestionId != q.Id)
                {
                    _currentQuestionId = q.Id;
                    _hasVotedCurrent = false;
                }

                // Le joueur a-t-il déjà voté à cette question ?
                _ = ShowQuestionAsync(q);
            }
            else
            {
                // Question pas encore démarrée par la TV → on patiente en intro
                if (!IsIntro) ShowIntro();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 1 — LOBBY
    // ═══════════════════════════════════════════════════════════════

    private void ShowLobby()
    {
        HideAllScreens();
        IsLobby = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 2 — INTRO
    // ═══════════════════════════════════════════════════════════════

    private void ShowIntro()
    {
        HideAllScreens();
        IsIntro = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 3 — QUESTION (liste des joueurs en boutons)
    // ═══════════════════════════════════════════════════════════════

    private async Task ShowQuestionAsync(QuizQuestion q)
    {
        try
        {
            // On récupère la question courante + la liste des joueurs via la
            // RPC get_party_question_for_tv (déjà exposée par PartyService).
            var data = await _partyService.GetCurrentQuestionForPlayerAsync(SeriesId);

            if (data == null || !(data.Value<bool?>("success") ?? false))
            {
                // En cas d'échec RPC, on retombe sur les données locales
                // (au moins le texte de la question reste affiché).
                HideAllScreens();
                QuestionText = q.QuestionText ?? q.Title ?? string.Empty;
                CurrentIndex = q.Position;
                QuestionPositionLabel = string.Format(
                    L.T("Party_Question_Position"), q.Position + 1, TotalQuestions);
                IsQuestion = true;
                return;
            }

            var voteDuration = data.Value<int?>("vote_duration_seconds") ?? 20;
            var questionNode = data["question"];
            if (questionNode == null)
            {
                ShowLobby();
                return;
            }

            var qId = (string?)questionNode["id"] ?? q.Id;
            var qPos = (int?)questionNode["position"] ?? q.Position;
            var qText = (string?)questionNode["question_text"]
                        ?? (string?)questionNode["title"]
                        ?? q.QuestionText
                        ?? string.Empty;

            _currentQuestionId = qId;
            CurrentIndex = qPos;
            QuestionText = qText;
            QuestionPositionLabel = string.Format(
                L.T("Party_Question_Position"), qPos + 1, TotalQuestions);

            // ⚡ CORRECTIF : si on a DÉJÀ voté à cette question (drapeau
            // local posé instantanément par VoteAsync), on reste sur
            // l'écran "voté" SANS attendre d'appel réseau. On ne consulte
            // HasUserVotedAsync (réseau, donc en retard) QUE si le drapeau
            // local est faux — utile au cas où le joueur a voté sur un
            // autre appareil / a relancé l'app en cours de question.
            if (_hasVotedCurrent)
            {
                ShowVotedScreen();
                StartCountdownFromStartedAt(
                    (DateTime?)questionNode["started_at"], voteDuration);
                return;
            }
            if (await _partyService.HasUserVotedAsync(SeriesId, qId))
            {
                _hasVotedCurrent = true;
                ShowVotedScreen();
                StartCountdownFromStartedAt(
                    (DateTime?)questionNode["started_at"], voteDuration);
                return;
            }

            // Construction de la liste des joueurs (= les options de vote)
            Players.Clear();
            var options = questionNode["options"];
            if (options != null)
            {
                foreach (var opt in options)
                {
                    var uid = (string?)opt["user_id"] ?? string.Empty;
                    if (string.IsNullOrEmpty(uid)) continue;
                    Players.Add(new PartyPlayerItem
                    {
                        UserId = uid,
                        Username = (string?)opt["username"] ?? "Joueur",
                        AvatarUrl = (string?)opt["avatar_url"]
                    });
                }
            }

            HideAllScreens();
            IsQuestion = true;

            // Countdown basé sur started_at (synchro avec la TV)
            StartCountdownFromStartedAt(
                (DateTime?)questionNode["started_at"], voteDuration);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] ShowQuestion: {ex.Message}");
            // Dégradé : on affiche au moins la question locale
            HideAllScreens();
            QuestionText = q.QuestionText ?? q.Title ?? string.Empty;
            IsQuestion = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SOUMETTRE UN VOTE (tap sur un pseudo)
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task VoteAsync(PartyPlayerItem? player)
    {
        if (player == null) return;
        if (!IsQuestion) return;
        if (string.IsNullOrEmpty(_currentQuestionId)) return;
        if (IsPaused) return;
        if (_hasVotedCurrent) return;

        // Marque visuellement la sélection
        foreach (var p in Players) p.IsSelected = false;
        player.IsSelected = true;

        // ⚡ CORRECTIF RACE CONDITION :
        // On bascule sur l'écran "voté" IMMÉDIATEMENT, AVANT l'appel
        // réseau. Sinon, pendant l'await SubmitVoteAsync (~100-300ms), un
        // événement realtime "quiz_questions UPDATE" (la TV décompte le
        // timer chaque seconde) relance ShowQuestionAsync, qui ne voit pas
        // encore _hasVotedCurrent=true et réaffiche la liste de pseudos
        // par-dessus → le vote semble "non pris en compte".
        // On pose donc le drapeau et l'écran tout de suite ; si le vote
        // échoue côté serveur, on annule proprement juste après.
        _hasVotedCurrent = true;
        VotedForName = player.Username;
        ShowVotedScreen();

        var (success, error) = await _partyService.SubmitVoteAsync(
            SeriesId, _currentQuestionId, player.UserId);

        if (!success)
        {
            // Vote refusé par le serveur → on annule la bascule optimiste
            _hasVotedCurrent = false;
            player.IsSelected = false;
            HideAllScreens();
            IsQuestion = true;
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                string.IsNullOrEmpty(error) ? L.T("Common_Error") : error,
                L.T("Common_OK"));
            return;
        }

        // Vote confirmé côté serveur — on est déjà sur l'écran "voté".
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 4 — VOTÉ (en attente des autres / de la TV)
    // ═══════════════════════════════════════════════════════════════

    private void ShowVotedScreen()
    {
        HideAllScreens();
        VotedMessage = L.T("Party_Voted_Waiting");
        IsVoted = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 5 — FINISH (résultats sur la TV)
    // ═══════════════════════════════════════════════════════════════

    private void ShowFinish()
    {
        StopAllTimers();
        HideAllScreens();
        IsFinish = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Countdown (synchro avec la TV via started_at)
    // ═══════════════════════════════════════════════════════════════

    private void StartCountdownFromStartedAt(DateTime? startedAt, int duration)
    {
        StopCountdownTimer();
        _currentQuestionStartedAt = startedAt?.ToUniversalTime();

        UpdateCountdown(duration);

        _countdownTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_countdownTimer == null) return;
        _countdownTimer.Interval = TimeSpan.FromMilliseconds(500);
        _countdownTimer.Tick += (s, e) => UpdateCountdown(duration);
        _countdownTimer.Start();
    }

    private void UpdateCountdown(int duration)
    {
        if (_currentQuestionStartedAt == null)
        {
            Countdown = duration;
            return;
        }

        var elapsed = (DateTime.UtcNow - _currentQuestionStartedAt.Value).TotalSeconds;
        var remaining = Math.Max(0, duration - (int)Math.Floor(elapsed));
        Countdown = remaining;

        CountdownColor = remaining <= 3 ? "#B5482F"      // rouge Sézane
                       : remaining <= 6 ? "#C2754C"      // terracotta
                       : "#6B7F5C";                      // sauge

        if (remaining <= 0)
        {
            StopCountdownTimer();
            // Timer écoulé : si pas voté, on bascule quand même sur "voté"
            // (le joueur ne peut plus voter, c'est la TV qui avance).
            if (!_hasVotedCurrent && IsQuestion)
            {
                ShowVotedScreen();
                VotedMessage = L.T("Party_Voted_TimeUp");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Quitter
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LeaveAsync()
    {
        StopAllTimers();
        try
        {
            await _partyService.LeaveSeriesAsync(SeriesId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] Leave: {ex.Message}");
        }
        await Shell.Current.GoToAsync("..");
    }

    // ═══════════════════════════════════════════════════════════════
    // REALTIME HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private async void OnRealtimeSeriesChanged(
        PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            var fresh = await _seriesService.GetSeriesAsync(SeriesId);
            if (fresh == null) return;

            var prev = _series;
            _series = fresh;

            IsPaused = fresh.TvPaused;

            if (!fresh.TvActive)
            {
                StopAllTimers();
                ShowError(L.T("Quiz_Error_TvDeactivatedTitle"),
                          L.T("Quiz_Error_TvDeactivatedMessage"));
                return;
            }

            // 🎬 Game start : preparing → active. Interstitielle non-bloquante.
            if (prev != null && prev.Status == "preparing" && fresh.Status == "active")
            {
                _ = ShowInterstitialThenSignalAsync();
            }

            if (prev == null
                || prev.Status != fresh.Status
                || prev.CurrentProjectIndex != fresh.CurrentProjectIndex)
            {
                CurrentIndex = fresh.CurrentProjectIndex;
                RenderCurrentScreen();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] OnSeriesChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeQuestionChanged(
        PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            _questions = await _partyService.GetQuestionsAsync(SeriesId);

            if (_series == null) return;

            var idx = _series.CurrentProjectIndex;
            if (idx >= 0 && idx < _questions.Count)
            {
                var q = _questions[idx];
                if (q.StartedAt != null && _currentQuestionId != q.Id)
                {
                    RenderCurrentScreen();
                }
            }
            else if (IsIntro && _questions.Count > 0
                     && _questions[0].StartedAt != null)
            {
                RenderCurrentScreen();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] OnQuestionChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeParticipantJoined(
        PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            ParticipantCount = await _partyService.GetParticipantCountAsync(SeriesId);
            UpdateParticipantLabel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] OnParticipantJoined: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Affiche la pub interstitielle (ou skip si pas dispo) puis signale à la
    /// TV via RPC mark_interstitial_seen que ce joueur a fini. Permet à la TV
    /// de démarrer la 1ère question dès que tout le monde a signé, sans
    /// attendre la fin du countdown intro complet.
    /// </summary>
    private async Task ShowInterstitialThenSignalAsync()
    {
        try { await _adService.ShowInterstitialBeforeGameStartAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PartyPlay] Interstitial ex: {ex.Message}"); }
        finally { await _seriesService.MarkInterstitialSeenAsync(SeriesId); }
    }

    private void UpdateParticipantLabel()
    {
        ParticipantCountLabel = string.Format(
            L.T("Party_Lobby_PlayersConnected"), ParticipantCount);
    }

    private void HideAllScreens()
    {
        IsLobby = false;
        IsIntro = false;
        IsQuestion = false;
        IsVoted = false;
        IsFinish = false;
        IsError = false;
    }

    private void ShowError(string title, string message)
    {
        StopAllTimers();
        HideAllScreens();
        ErrorTitle = title;
        ErrorMessage = message;
        IsError = true;
    }

    private void StopAllTimers()
    {
        StopCountdownTimer();
        StopPollTimer();
    }

    private void StopCountdownTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer = null;
        }
    }

    private void StopPollTimer()
    {
        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer = null;
        }
    }

    public async Task CleanupAsync()
    {
        StopAllTimers();
        try
        {
            await _realtime.UnsubscribeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlay] Cleanup: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _realtime.OnSeriesChanged = null;
        _realtime.OnQuizQuestionChanged = null;
        _realtime.OnParticipantJoined = null;
        StopAllTimers();
        _ = _realtime.UnsubscribeAsync();
    }
}

// ═══════════════════════════════════════════════════════════════════
// ITEM MODEL — un joueur (= une option de vote)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Un joueur connecté affiché comme un gros bouton tappable sur l'écran
/// de question. Tap → vote pour ce joueur (auto-vote autorisé).
/// </summary>
public partial class PartyPlayerItem : ObservableObject
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Initiale du pseudo, affichée dans la pastille quand pas d'avatar.
    /// </summary>
    public string Initial =>
        string.IsNullOrWhiteSpace(Username)
            ? "?"
            : Username.Trim().Substring(0, 1).ToUpperInvariant();

    /// <summary>True si l'avatar doit être affiché (URL non vide).</summary>
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    /// <summary>True si l'initiale doit être affichée (pas d'avatar).</summary>
    public bool ShowInitial => !HasAvatar;

    [ObservableProperty] private bool _isSelected;
}