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
/// 🎮 PHASE 2 — Player mobile QUIZ
///
/// Page joueur d'un quiz. Réplique côté mobile la machine à états de la TV
/// (tv-quiz-app.js), avec ces différences :
///  - Le mobile NE PILOTE PAS l'avancement. C'est la TV qui appelle
///    tv_start_first_element et tv_advance_to_next.
///  - Le mobile ÉCOUTE le realtime sur (series, quiz_questions, quiz_answers,
///    series_participants) via SeriesRealtimeService en mode "quiz".
///  - Le mobile SOUMET la réponse via QuizService.SubmitAnswerAsync.
///
/// Machine à états (driven par series.status + quiz_questions[idx].started_at) :
///   LOBBY     : series.status = 'preparing'
///   INTRO     : status = 'active', Q[0].started_at == null
///               → countdown intro_duration_seconds, attend que la TV démarre Q1
///   QUESTION  : status = 'active', Q[idx].started_at != null, user n'a pas répondu
///               → 4 boutons options + countdown vote_duration_seconds
///   ANSWERED  : user a répondu, on attend le timer ou la TV
///               → "Réponse envoyée — en attente des autres"
///   REVEAL    : timer écoulé localement
///               → on charge get_quiz_question_results et on affiche la bonne réponse
///   FINISH    : status = 'finished'
///               → leaderboard final + ton rang + ton score
///   ERROR     : code invalide / TV désactivée / etc.
/// </summary>
[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class QuizPlayViewModel : ObservableObject, IDisposable
{
    private readonly QuizService _quizService;
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
    private IDispatcherTimer? _introTimer;
    private DateTime? _currentQuestionStartedAt;
    private DateTime? _introStartedAt;
    private bool _hasTriggeredReveal = false;

    // ─── Tracking de la réponse en cours ──────────────────────────
    private DateTime _questionShownAt = DateTime.UtcNow;
    private string? _selectedOptionId;
    private string? _currentQuestionId;

    // ─── Selfie : taux adaptatif selon nb participants ─────────────
    // Calculé à l'init dans InitAsync. Tirage probabiliste indépendant
    // à chaque soumission de réponse. Aucune limite "1 par session" :
    // chaque joueur peut tomber 0, 1, ou plusieurs fois selon son sort.
    private double _selfieRate = 0.0;
    private static readonly Random _selfieRandom = new Random();
    public Func<Task<Stream?>>? CaptureSelfieRequested { get; set; }
    [ObservableProperty] private bool _isSelfieCapturing = false;

    public QuizPlayViewModel(
        QuizService quizService,
        SeriesService seriesService,
        SeriesRealtimeService realtime,
        IAdService adService)
    {
        _quizService = quizService;
        _seriesService = seriesService;
        _realtime = realtime;
        _adService = adService;

        _realtime.OnSeriesChanged = OnRealtimeSeriesChanged;
        _realtime.OnQuizQuestionChanged = OnRealtimeQuizQuestionChanged;
        _realtime.OnQuizAnswerReceived = OnRealtimeQuizAnswerReceived;
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
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _isReveal = false;
    [ObservableProperty] private bool _isFinish = false;
    [ObservableProperty] private bool _isError = false;
    [ObservableProperty] private string _errorTitle = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Pause depuis la TV
    [ObservableProperty] private bool _isPaused = false;

    // ═══════════════════════════════════════════════════════════════
    // Lobby / Intro
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _quizTitle = string.Empty;
    [ObservableProperty] private string _quizDescription = string.Empty;
    [ObservableProperty] private int _totalQuestions = 0;
    [ObservableProperty] private int _participantCount = 0;
    [ObservableProperty] private int _introCountdown = 0;
    [ObservableProperty] private string _introCountdownText = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // 🎨 STYLE DU QUIZ (kahoot / millionaire / burger / weakest)
    // ───────────────────────────────────────────────────────────────
    // QuizStyle est lu depuis _series.QuizStyle au démarrage. Toutes
    // les propriétés calculées StyleXxx en dépendent — quand QuizStyle
    // change, les NotifyPropertyChangedFor déclenchent leur refresh.
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StylePageBackground))]
    [NotifyPropertyChangedFor(nameof(StyleAccent))]
    [NotifyPropertyChangedFor(nameof(StyleAccentLight))]
    [NotifyPropertyChangedFor(nameof(StyleHeaderBg))]
    [NotifyPropertyChangedFor(nameof(StyleQuestionTextColor))]
    [NotifyPropertyChangedFor(nameof(StyleOptionBg))]
    [NotifyPropertyChangedFor(nameof(StyleOptionTextColor))]
    [NotifyPropertyChangedFor(nameof(StyleOptionLetterBg))]
    [NotifyPropertyChangedFor(nameof(StyleOptionLetterColor))]
    [NotifyPropertyChangedFor(nameof(StyleIntroBg))]
    [NotifyPropertyChangedFor(nameof(StyleIntroAccent))]
    [NotifyPropertyChangedFor(nameof(StyleIntroSubAccent))]
    [NotifyPropertyChangedFor(nameof(StyleSecondaryText))]
    [NotifyPropertyChangedFor(nameof(StyleFinishWinnerBg))]
    private string _quizStyle = "kahoot";

    // ─── Couleurs principales selon le style ─────────────────────────
    // Utilisées dans QuizPlayPage.xaml via Binding sur ces propriétés.

    /// <summary>Fond général de la page (lobby, question body, finish).</summary>
    public string StylePageBackground => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // bleu nuit profond
        "burger"      => "#B5482F",      // brique rouge
        "weakest"     => "#5A7A8C",      // bleu nuit très foncé
        _             => "#F4EFE6",      // kahoot : gris clair
    };

    /// <summary>Couleur d'accentuation principale (titres, badges, boutons primaires).</summary>
    public string StyleAccent => QuizStyle switch
    {
        "millionaire" => "#C9943E",      // or
        "burger"      => "#C9943E",      // jaune pop
        "weakest"     => "#C9943E",      // jaune
        _             => "#C2754C",      // kahoot : rose
    };

    /// <summary>Variante claire de l'accent (chip de la lettre, pastilles).</summary>
    public string StyleAccentLight => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // bleu medium pour pastilles
        "burger"      => "#F5EBD5",      // jaune très clair
        "weakest"     => "#5A7A8C",      // bleu très foncé pour pastilles
        _             => "#E5DCC9",      // kahoot : rose clair
    };

    /// <summary>Fond du header haut (barre "Question N/M").</summary>
    public string StyleHeaderBg => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // header bleu très foncé
        "burger"      => "#5A7A8C",      // cyan TV cathodique
        "weakest"     => "#000000",      // noir pur
        _             => "#FFFFFF",      // kahoot : blanc
    };

    /// <summary>Couleur du texte de la question (énoncé principal).</summary>
    public string StyleQuestionTextColor => QuizStyle switch
    {
        "millionaire" => "#FFFFFF",
        "burger"      => "#252019",
        "weakest"     => "#FFFFFF",
        _             => "#3D2817",
    };

    /// <summary>Fond des cartes options (avant tap).</summary>
    public string StyleOptionBg => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // bleu hexagone
        "burger"      => "#FFFFFF",      // blanc avec bordure rouge
        "weakest"     => "#252019",      // gris très foncé
        _             => "#FFFFFF",      // kahoot : blanc
    };

    /// <summary>Couleur du texte d'une option.</summary>
    public string StyleOptionTextColor => QuizStyle switch
    {
        "millionaire" => "#FFFFFF",
        "burger"      => "#252019",
        "weakest"     => "#FFFFFF",
        _             => "#3D2817",
    };

    /// <summary>Fond du carré de la lettre A/B/C/D dans une option.</summary>
    public string StyleOptionLetterBg => QuizStyle switch
    {
        "millionaire" => "#C9943E",      // or
        "burger"      => "#B5482F",      // rouge Burger
        "weakest"     => "#000000",      // noir transparent (lettre seule en jaune)
        _             => "#E5DCC9",      // kahoot : rose clair
    };

    /// <summary>Couleur de la lettre A/B/C/D dans son carré.</summary>
    public string StyleOptionLetterColor => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // bleu nuit (contraste sur or)
        "burger"      => "#FFFFFF",      // blanc
        "weakest"     => "#C9943E",      // jaune
        _             => "#C2754C",      // kahoot : rose
    };

    /// <summary>Fond de l'écran intro (préparation).</summary>
    public string StyleIntroBg => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",      // bleu nuit
        "burger"      => "#B5482F",      // brique
        "weakest"     => "#5A7A8C",      // nuit
        _             => "#6B7F5C",      // kahoot : violet
    };

    /// <summary>Couleur du tag "Préparez-vous" en intro.</summary>
    public string StyleIntroAccent => QuizStyle switch
    {
        "millionaire" => "#C9943E",
        "burger"      => "#C9943E",
        "weakest"     => "#C9943E",
        _             => "#E5DCC9",      // kahoot : rose pastel
    };

    /// <summary>Couleur du sous-texte (description) en intro.</summary>
    public string StyleIntroSubAccent => QuizStyle switch
    {
        "millionaire" => "#F5EBD5",
        "burger"      => "#F5EBD5",
        "weakest"     => "#5A7A8C",
        _             => "#F5EBD5",      // kahoot : violet pastel
    };

    /// <summary>Couleur des textes secondaires (compteur, infos).</summary>
    public string StyleSecondaryText => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",
        "burger"      => "#F5EBD5",
        "weakest"     => "#8A6F4A",
        _             => "#8A6F4A",      // kahoot : gris
    };

    /// <summary>Fond du bandeau vainqueur sur l'écran finish.</summary>
    public string StyleFinishWinnerBg => QuizStyle switch
    {
        "millionaire" => "#5A7A8C",
        "burger"      => "#B5482F",
        "weakest"     => "#5A7A8C",
        _             => "#6B7F5C",      // kahoot : violet
    };

    // ═══════════════════════════════════════════════════════════════
    // Question en cours
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private int _currentIndex = 0;
    [ObservableProperty] private string _questionPositionLabel = string.Empty;
    [ObservableProperty] private string _questionTitle = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string? _questionPhotoUrl;

    [ObservableProperty] private int _countdown = 0;
    [ObservableProperty] private string _countdownColor = "#C2754C";

    public ObservableCollection<QuizPlayOptionItem> Options { get; } = new();

    [ObservableProperty] private int _answerCount = 0;
    [ObservableProperty] private string _answerCountLabel = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // Reveal
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private bool _wasCorrect = false;
    [ObservableProperty] private string _revealTitle = string.Empty;
    [ObservableProperty] private string _revealMessage = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // Finish
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private string _winnerName = string.Empty;
    [ObservableProperty] private int _myRank = 0;
    [ObservableProperty] private int _myScore = 0;
    [ObservableProperty] private string _myRankLabel = string.Empty;
    [ObservableProperty] private string _myScoreLabel = string.Empty;
    public ObservableCollection<QuizPlayLeaderItem> Leaderboard { get; } = new();

    // ═══════════════════════════════════════════════════════════════
    // 📊 Stats détaillées (sujet 3 — page de stats sous le leaderboard)
    // ───────────────────────────────────────────────────────────────
    // Affiché tout en bas de l'écran finish, sous le leaderboard.
    // Section visible UNIQUEMENT si HasStats == true (au moins 1 vote
    // dans le quiz, sinon on n'a rien à montrer).
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private bool _hasStats = false;

    // Stats globales (cards en haut de la section)
    [ObservableProperty] private int _statsTotalPlayers = 0;
    [ObservableProperty] private int _statsTotalQuestions = 0;
    [ObservableProperty] private int _statsTotalAnswers = 0;
    [ObservableProperty] private int _statsSuccessRate = 0;

    // Question la + dure / la + facile (visible si dispo)
    [ObservableProperty] private bool _hasHardestQuestion = false;
    [ObservableProperty] private string _hardestQuestionText = string.Empty;
    [ObservableProperty] private int _hardestQuestionRate = 0;

    [ObservableProperty] private bool _hasEasiestQuestion = false;
    [ObservableProperty] private string _easiestQuestionText = string.Empty;
    [ObservableProperty] private int _easiestQuestionRate = 0;

    // Visibilité des sections barres (alimentées par LoadDetailedStatsAsync)
    [ObservableProperty] private bool _hasGenderBars = false;
    [ObservableProperty] private bool _hasAgeBars = false;

    // Barres genre + âge (collections affichées en CollectionView)
    public ObservableCollection<QuizPlayStatBarItem> GenderBars { get; } = new();
    public ObservableCollection<QuizPlayStatBarItem> AgeBars { get; } = new();

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
                    $"[QuizPlay] InitAsync ignoré (initializing={_isInitializing}, initialized={_isInitialized})");
                return;
            }
            _isInitializing = true;
        }

        try
        {
            IsLoading = true;
            HideAllScreens();

            // 1) Rejoindre la série (idempotent)
            await _quizService.JoinSeriesAsync(SeriesId);

            // 2) Charger l'état de la série
            _series = await _seriesService.GetSeriesAsync(SeriesId);
            if (_series == null)
            {
                ShowError(L.T("Common_Error"), L.T("Quiz_Error_SeriesNotFound"));
                return;
            }

            QuizTitle = _series.Title;
            QuizDescription = _series.Description ?? string.Empty;
            IsPaused = _series.TvPaused;
            CurrentIndex = _series.CurrentProjectIndex;

            // 🎨 Récupération du style du quiz pour adapter visuellement
            // toutes les pages (kahoot/millionaire/burger/weakest).
            // Fallback : kahoot si null/vide ou inconnu.
            var rawStyle = _series.QuizStyle?.Trim().ToLowerInvariant();
            QuizStyle = rawStyle switch
            {
                "millionaire" or "burger" or "weakest" or "kahoot" => rawStyle,
                _ => "kahoot",
            };
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] Style appliqué : {QuizStyle}");

            // 3) Charger les questions (sans is_correct, c'est le RPC qui gère)
            _questions = await _quizService.GetQuestionsAsync(SeriesId);
            TotalQuestions = _questions.Count;

            // 4) Charger compteur participants
            ParticipantCount = await _quizService.GetParticipantCountAsync(SeriesId);

            // 4bis) Taux de selfie adaptatif : ~0.60 selfie par joueur sur
            // l'ensemble de la session (10 joueurs × 10 questions × 0.06 ≈ 6
            // selfies au total). Clamp pour garder du jeu sur petits/gros groupes.
            var safeCount = Math.Max(1, ParticipantCount);
            _selfieRate = Math.Clamp(0.60 / safeCount, 0.01, 0.60);
            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] Selfie rate: {_selfieRate:P1} ({safeCount} participants)");

            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] Init : status={_series.Status}, idx={CurrentIndex}, " +
                $"questions={_questions.Count}, participants={ParticipantCount}");

            _isInitialized = true;

            // 5) S'abonner au realtime AVANT le premier render (pour ne rater aucun event
            //    pendant le rendu)
            await _realtime.SubscribeAsync(SeriesId, "quiz");

            // 5bis) Précharge systématiquement l'interstitielle. Affichée :
            //  - soit sur la transition preparing → active (cf. OnRealtimeSeriesChanged)
            //  - soit ici même si on arrive directement en INTRO (animateur solo qui
            //    lance le quiz depuis l'écran TV : status est déjà "active" à l'init).
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
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] InitAsync: {ex.Message}");
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
    // (réplique de tv-quiz-app.js renderCurrentScreen)
    // ═══════════════════════════════════════════════════════════════

    private void RenderCurrentScreen()
    {
        if (_series == null) return;

        // Pause overlay (calculé indépendamment, ne change pas l'écran courant)
        IsPaused = _series.TvPaused;

        if (_series.Status == "preparing")
        {
            StopAllTimers();
            ShowLobby();
        }
        else if (_series.Status == "active")
        {
            // status active : on regarde si Q1 a déjà été démarrée
            var firstQ = _questions.Count > 0 ? _questions[0] : null;
            if (firstQ != null && firstQ.StartedAt == null)
            {
                // Pas encore démarré → INTRO
                if (!IsIntro) ShowIntro();
            }
            else
            {
                // Une question est démarrée → QUESTION ou ANSWERED ou REVEAL
                var idx = _series.CurrentProjectIndex;
                if (idx < 0 || idx >= _questions.Count) return;
                var q = _questions[idx];
                if (q?.StartedAt != null)
                {
                    // Reset les flags si on change de question
                    if (_currentQuestionId != q.Id)
                    {
                        _hasTriggeredReveal = false;
                        _selectedOptionId = null;
                    }
                    ShowQuestion(q, idx);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[QuizPlay] Question sans started_at, on attend le realtime");
                }
            }
        }
        else if (_series.Status == "finished")
        {
            StopAllTimers();
            _ = ShowFinishAsync();
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
    // Écran 2 — INTRO (countdown intro_duration_seconds)
    // ═══════════════════════════════════════════════════════════════

    private void ShowIntro()
    {
        HideAllScreens();
        IsIntro = true;

        var duration = _series?.IntroDurationSeconds ?? 20;
        IntroCountdown = duration;
        IntroCountdownText = string.Format(L.T("Quiz_Intro_StartingIn"), duration);

        // 🔧 BUG FIX : on cale sur series.tv_started_at posé par tv_start_series côté
        // serveur. Comme ça tous les mobiles qui rejoignent au même moment partagent
        // le même point de référence UTC (au lieu de chacun démarrer à son DateTime.UtcNow
        // local au moment de OnAppearing). Si tv_started_at est NULL pour une raison X
        // (legacy ?), fallback sur DateTime.UtcNow.
        if (_series?.TvStartedAt != null)
        {
            _introStartedAt = _series.TvStartedAt.Value.Kind == DateTimeKind.Utc
                ? _series.TvStartedAt.Value
                : DateTime.SpecifyKind(_series.TvStartedAt.Value, DateTimeKind.Utc);
            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] Intro calée sur tv_started_at={_introStartedAt:o}");
        }
        else
        {
            _introStartedAt = DateTime.UtcNow;
            System.Diagnostics.Debug.WriteLine(
                "[QuizPlay] Intro calée sur DateTime.UtcNow (tv_started_at null)");
        }

        // Calcul initial du remaining (au cas où le mobile arrive en cours d'intro)
        UpdateIntroRemainingFromStartedAt();

        StopIntroTimer();
        _introTimer = Application.Current!.Dispatcher.CreateTimer();
        _introTimer.Interval = TimeSpan.FromMilliseconds(500);
        _introTimer.Tick += OnIntroTick;
        _introTimer.Start();
    }

    private void UpdateIntroRemainingFromStartedAt()
    {
        if (_introStartedAt == null) { IntroCountdown = 0; return; }

        var duration = _series?.IntroDurationSeconds ?? 20;
        var elapsed = (DateTime.UtcNow - _introStartedAt.Value).TotalSeconds;
        var remaining = (int)Math.Round(duration - elapsed);
        if (remaining < 0) remaining = 0;
        if (remaining > duration) remaining = duration;

        IntroCountdown = remaining;
        IntroCountdownText = string.Format(L.T("Quiz_Intro_StartingIn"), remaining);
    }

    private void OnIntroTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        if (IsPaused) return;
        if (_introStartedAt == null) return;

        UpdateIntroRemainingFromStartedAt();

        // À 0 on stoppe juste le timer ; c'est la TV qui pose le started_at
        // sur Q[0], et le realtime nous reroutera vers QUESTION.
        if (IntroCountdown <= 0)
        {
            StopIntroTimer();
            System.Diagnostics.Debug.WriteLine(
                "[QuizPlay] Intro terminée, attente du started_at de Q1 via realtime");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 3 — QUESTION (4 options tappables + countdown)
    // ═══════════════════════════════════════════════════════════════

    private async void ShowQuestion(QuizQuestion q, int idx)
    {
        HideAllScreens();

        _currentQuestionId = q.Id;

        // Si l'user a déjà répondu à cette question, on saute direct à ANSWERED
        // (cas où il revient sur la page après avoir répondu et fermé l'app)
        bool alreadyAnswered = await _quizService.HasUserAnsweredAsync(q.Id);

        QuestionPositionLabel = string.Format(
            L.T("Quiz_Question_Progress"), idx + 1, _questions.Count);
        QuestionTitle = q.Title ?? string.Empty;
        QuestionText = q.QuestionText ?? string.Empty;
        QuestionPhotoUrl = q.PhotoUrl;

        // Charge les options via la RPC anti-spoiler (pas de is_correct)
        await LoadCurrentQuestionOptionsAsync();

        // Compteur "X/Y ont répondu" (initialisé à la valeur courante)
        AnswerCount = await _quizService.GetQuestionAnswerCountAsync(q.Id);
        UpdateAnswerCountLabel();

        _currentQuestionStartedAt = q.StartedAt;
        _questionShownAt = DateTime.UtcNow;

        if (alreadyAnswered)
        {
            // L'user a déjà répondu (peut-être pendant qu'il était parti), on
            // affiche directement l'écran d'attente avec le countdown qui tourne.
            IsAnswered = true;
        }
        else
        {
            IsQuestion = true;
        }

        StartQuestionCountdown();
    }

    /// <summary>
    /// Charge les options de la question courante via la RPC anti-spoiler.
    /// La RPC ne renvoie pas is_correct (c'est volontaire — pas de spoil).
    /// </summary>
    private async Task LoadCurrentQuestionOptionsAsync()
    {
        try
        {
            var rawData = await _quizService.GetCurrentQuestionForPlayerAsync(SeriesId);
            if (rawData == null)
            {
                System.Diagnostics.Debug.WriteLine("[QuizPlay] RPC get_quiz_question_for_tv : null");
                return;
            }

            var qNode = rawData["question"];
            if (qNode == null) return;

            // Synchronise le vote_duration_seconds si renvoyé au top-level
            var vds = rawData["vote_duration_seconds"];
            if (vds != null && _series != null)
            {
                _series.VoteDurationSeconds = (int)vds;
            }

            Options.Clear();
            var optsArray = qNode["options"];
            if (optsArray != null)
            {
                int i = 0;
                foreach (var opt in optsArray)
                {
                    var letter = ((char)('A' + i)).ToString();
                    var text = (string?)opt["text"]
                        ?? (string?)opt["option_text"]
                        ?? string.Empty;
                    var optId = (string?)opt["id"] ?? string.Empty;

                    Options.Add(new QuizPlayOptionItem
                    {
                        Id = optId,
                        Letter = letter,
                        Text = text,
                        IsSelected = false,
                        IsCorrect = false,        // pas de spoiler ici
                        IsRevealed = false
                    });
                    i++;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] LoadOptions: {ex.Message}");
        }
    }

    private void UpdateAnswerCountLabel()
    {
        var total = Math.Max(1, ParticipantCount);
        AnswerCountLabel = string.Format(L.T("Quiz_Question_AnswerCount"), AnswerCount, total);
    }

    // ─── Countdown ────────────────────────────────────────────────

    private void StartQuestionCountdown()
    {
        StopCountdownTimer();

        if (_currentQuestionStartedAt == null) return;

        UpdateCountdownFromStartedAt();
        UpdateCountdownColor();

        _countdownTimer = Application.Current!.Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromMilliseconds(500);
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void UpdateCountdownFromStartedAt()
    {
        if (_currentQuestionStartedAt == null) { Countdown = 0; return; }

        var startedUtc = _currentQuestionStartedAt.Value.Kind == DateTimeKind.Utc
            ? _currentQuestionStartedAt.Value
            : DateTime.SpecifyKind(_currentQuestionStartedAt.Value, DateTimeKind.Utc);

        var dur = _series?.VoteDurationSeconds ?? 20;
        var elapsed = (DateTime.UtcNow - startedUtc).TotalSeconds;
        var remaining = (int)Math.Round(dur - elapsed);
        if (remaining < 0) remaining = 0;
        if (remaining > dur) remaining = dur;
        Countdown = remaining;
    }

    private void UpdateCountdownColor()
    {
        CountdownColor = Countdown switch
        {
            <= 5 => "#B5482F",
            <= 10 => "#C9943E",
            _ => "#C2754C"
        };
    }

    private async void OnCountdownTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;
        if (IsPaused) return;

        UpdateCountdownFromStartedAt();
        UpdateCountdownColor();

        if (Countdown <= 0)
        {
            StopCountdownTimer();
            // Timer écoulé → on passe au reveal local (idempotent grâce à _hasTriggeredReveal)
            if (!_hasTriggeredReveal && _currentQuestionId != null)
            {
                _hasTriggeredReveal = true;
                await TriggerRevealAsync(_currentQuestionId);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SOUMETTRE UNE RÉPONSE
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SelectOptionAsync(QuizPlayOptionItem? option)
    {
        if (option == null) return;
        if (!IsQuestion) return;
        if (string.IsNullOrEmpty(_currentQuestionId)) return;
        if (IsPaused) return;

        // Visuellement on marque la sélection (pas multi-select pour l'instant —
        // BeauOuPas n'a qu'une bonne réponse par question d'après le récap projet).
        foreach (var o in Options) o.IsSelected = false;
        option.IsSelected = true;
        _selectedOptionId = option.Id;

        var responseTimeMs = (int)(DateTime.UtcNow - _questionShownAt).TotalMilliseconds;

        var (success, error, isCorrect) = await _quizService.SubmitAnswerAsync(
            _currentQuestionId,
            new List<string> { option.Id },
            responseTimeMs);

        if (!success)
        {
            // Erreur de submit : on déselectionne pour permettre de retenter
            option.IsSelected = false;
            _selectedOptionId = null;
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                error,
                L.T("Common_OK"));
            return;
        }

        // OK — on bascule sur l'écran "réponse envoyée, en attente"
        WasCorrect = isCorrect;
        IsQuestion = false;
        IsAnswered = true;
        // Le timer continue de tourner ; à 0 on bascule sur reveal.

        // ─── Selfie : tirage probabiliste indépendant ─────────────
        // Taux calculé dans InitAsync selon le nb de participants.
        // Tirage à chaque question : tout le monde a sa chance à chaque
        // réponse, pas de cap "1 par session". Capture immédiate (pendant
        // que la cam est dispo) puis upload différé 0-10s. Fire and forget
        // intentionnel : ne bloque pas la transition d'écran.
        bool shouldDoSelfie = !_isDisposed
            && _selfieRate > 0
            && _selfieRandom.NextDouble() < _selfieRate;

        if (shouldDoSelfie)
        {
            // ⚡ Capture l'ID de question à L'INSTANT du vote. Pendant les ~8-18s
            // entre capture et upload, le user peut être passé sur Q[n+1] — on
            // doit garder l'attachement à la question pour laquelle le selfie
            // a été pris (sinon il s'afficherait au mauvais reveal côté TV).
            _ = CaptureAndUploadSelfieAsync(_currentQuestionId);
        }
    }

    // ─── Capture + upload selfie en arrière-plan ──────────────────
    /// <summary>
    /// Capture immédiate de la photo (la cam est prête à l'instant du
    /// vote), puis upload différé d'un délai aléatoire 0-10s pour lisser
    /// le pic réseau si gros événement. Non-bloquant pour l'utilisateur.
    /// </summary>
    private async Task CaptureAndUploadSelfieAsync(string? questionId)
    {
        IsSelfieCapturing = true;
        try
        {
            if (CaptureSelfieRequested == null) return;

            using var stream = await CaptureSelfieRequested.Invoke();
            if (stream == null || _isDisposed)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[QuizPlay] Selfie stream null (capture failed/denied/disposed)");
                return;
            }

            using var bufferedStream = new MemoryStream();
            await stream.CopyToAsync(bufferedStream);

            var delayMs = _selfieRandom.Next(0, 10_001);
            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] Selfie captured, upload in {delayMs}ms");
            await Task.Delay(delayMs);

            if (_isDisposed) return;

            bufferedStream.Position = 0;
            var ok = await _seriesService.UploadSessionSelfieAsync(
                SeriesId, bufferedStream, questionId);

            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] Selfie upload result: {ok}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlay] CaptureAndUploadSelfie: {ex.Message}");
        }
        finally
        {
            IsSelfieCapturing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 4 — REVEAL
    // ═══════════════════════════════════════════════════════════════

    private async Task TriggerRevealAsync(string questionId)
    {
        try
        {
            HideAllScreens();
            IsReveal = true;

            // Charge les résultats avec is_correct via get_quiz_question_results
            var results = await _quizService.GetQuestionResultsAsync(questionId);
            if (results != null)
            {
                var optsNode = results["options"];
                if (optsNode != null)
                {
                    // Marque dans Options laquelle est correcte / sélectionnée
                    foreach (var optResult in optsNode)
                    {
                        var optId = (string?)optResult["id"];
                        var isCorrect = (bool?)optResult["is_correct"] ?? false;
                        var existing = Options.FirstOrDefault(o => o.Id == optId);
                        if (existing != null)
                        {
                            existing.IsCorrect = isCorrect;
                            existing.IsRevealed = true;
                        }
                    }
                }
            }

            // Affichage du verdict
            if (string.IsNullOrEmpty(_selectedOptionId))
            {
                // Pas de réponse envoyée
                WasCorrect = false;
                RevealTitle = L.T("Quiz_Reveal_NoAnswer");
                RevealMessage = L.T("Quiz_Reveal_NoAnswerMessage");
            }
            else if (WasCorrect)
            {
                RevealTitle = L.T("Quiz_Reveal_Correct");
                RevealMessage = L.T("Quiz_Reveal_CorrectMessage");
            }
            else
            {
                RevealTitle = L.T("Quiz_Reveal_Wrong");
                RevealMessage = L.T("Quiz_Reveal_WrongMessage");
            }

            // ⚠️ On ne passe PAS à la question suivante automatiquement — c'est
            // la TV qui appelle tv_advance_to_next, et l'event realtime
            // (series UPDATE current_project_index) nous remettra sur QUESTION.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] TriggerReveal: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Écran 5 — FINISH
    // ═══════════════════════════════════════════════════════════════

    private async Task ShowFinishAsync()
    {
        HideAllScreens();
        IsFinish = true;

        try
        {
            var finalResults = await _quizService.GetFinalResultsAsync(SeriesId);
            if (finalResults == null) return;

            // Top 3 (winner)
            var top3 = finalResults["top3"] as Newtonsoft.Json.Linq.JArray;
            if (top3 != null && top3.Count > 0)
            {
                var winner = top3[0];
                if (winner != null)
                {
                    WinnerName = (string?)winner["username"] ?? "?";
                }
            }

            // Leaderboard complet
            Leaderboard.Clear();
            var leaderboard = finalResults["leaderboard"];
            if (leaderboard != null)
            {
                int rank = 1;
                var myUserId = _quizService.CurrentUserId;
                foreach (var entry in leaderboard)
                {
                    var userId = (string?)entry["user_id"] ?? string.Empty;
                    var username = (string?)entry["username"] ?? "?";
                    var score = (int?)entry["score"] ?? 0;
                    var avatarUrl = (string?)entry["avatar_url"];

                    bool isMe = !string.IsNullOrEmpty(myUserId) && userId == myUserId;
                    if (isMe)
                    {
                        MyRank = rank;
                        MyScore = score;
                    }

                    Leaderboard.Add(new QuizPlayLeaderItem
                    {
                        Rank = rank,
                        Username = username,
                        Score = score,
                        AvatarUrl = avatarUrl,
                        IsMe = isMe
                    });
                    rank++;
                }
            }

            MyRankLabel = string.Format(L.T("Quiz_Finish_YourRank"), MyRank);
            MyScoreLabel = string.Format(L.T("Quiz_Finish_YourScore"), MyScore);

            // 📊 Charge les stats détaillées (sujet 3) en tâche de fond — non bloquant
            // si ça plante, on a déjà l'essentiel (winner + leaderboard).
            _ = LoadDetailedStatsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] ShowFinish: {ex.Message}");
        }
    }

    /// <summary>
    /// Charge les stats détaillées du quiz via la RPC get_quiz_detailed_stats.
    /// Alimente HasStats, StatsXxx, GenderBars, AgeBars.
    /// Si la RPC échoue ou retourne pas assez de données, HasStats reste false
    /// et la section stats n'est pas affichée.
    /// </summary>
    private async Task LoadDetailedStatsAsync()
    {
        try
        {
            var stats = await _quizService.GetDetailedStatsAsync(SeriesId);
            if (stats == null)
            {
                HasStats = false;
                return;
            }

            // ─── Totaux ─────────────────────────────────────────────
            var totals = stats["totals"];
            if (totals != null)
            {
                StatsTotalPlayers = (int?)totals["players"] ?? 0;
                StatsTotalQuestions = (int?)totals["questions"] ?? 0;
                StatsTotalAnswers = (int?)totals["answers"] ?? 0;
                StatsSuccessRate = (int?)totals["success_rate"] ?? 0;
            }

            // ─── Question la plus difficile ─────────────────────────
            var hardest = stats["hardest_question"];
            if (hardest != null && hardest.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            {
                HasHardestQuestion = true;
                HardestQuestionText = (string?)hardest["question_text"] ?? string.Empty;
                HardestQuestionRate = (int?)hardest["success_rate"] ?? 0;
            }
            else
            {
                HasHardestQuestion = false;
            }

            // ─── Question la plus facile ────────────────────────────
            var easiest = stats["easiest_question"];
            if (easiest != null && easiest.Type != Newtonsoft.Json.Linq.JTokenType.Null)
            {
                HasEasiestQuestion = true;
                EasiestQuestionText = (string?)easiest["question_text"] ?? string.Empty;
                EasiestQuestionRate = (int?)easiest["success_rate"] ?? 0;
            }
            else
            {
                HasEasiestQuestion = false;
            }

            // ─── Barres genre ───────────────────────────────────────
            GenderBars.Clear();
            var genderArr = stats["gender_breakdown"] as Newtonsoft.Json.Linq.JArray;
            if (genderArr != null)
            {
                foreach (var g in genderArr)
                {
                    var key = (string?)g["key"] ?? "unknown";
                    var label = key switch
                    {
                        "male"   => L.T("Quiz_Stats_GenderMale"),
                        "female" => L.T("Quiz_Stats_GenderFemale"),
                        "other"  => L.T("Quiz_Stats_GenderOther"),
                        _        => L.T("Quiz_Stats_GenderUnknown"),
                    };
                    var count = (int?)g["count"] ?? 0;
                    if (count == 0) continue; // on n'affiche pas les catégories vides

                    GenderBars.Add(new QuizPlayStatBarItem
                    {
                        Label = label,
                        Count = count,
                        Percent = (int?)g["percent"] ?? 0,
                        SuccessRate = (int?)g["success_rate"] ?? 0,
                    });
                }
            }

            // ─── Barres tranches d'âge ──────────────────────────────
            AgeBars.Clear();
            var ageArr = stats["age_breakdown"] as Newtonsoft.Json.Linq.JArray;
            if (ageArr != null)
            {
                foreach (var a in ageArr)
                {
                    var key = (string?)a["key"] ?? "unknown";
                    var label = key switch
                    {
                        "0_17"    => L.T("Quiz_Stats_Age0To17"),
                        "18_29"   => L.T("Quiz_Stats_Age18To29"),
                        "30_49"   => L.T("Quiz_Stats_Age30To49"),
                        "50_plus" => L.T("Quiz_Stats_Age50Plus"),
                        _         => L.T("Quiz_Stats_AgeUnknown"),
                    };
                    var count = (int?)a["count"] ?? 0;
                    if (count == 0) continue;

                    AgeBars.Add(new QuizPlayStatBarItem
                    {
                        Label = label,
                        Count = count,
                        Percent = (int?)a["percent"] ?? 0,
                        SuccessRate = (int?)a["success_rate"] ?? 0,
                    });
                }
            }

            // On n'affiche la section stats que si on a au moins une donnée
            HasGenderBars = GenderBars.Count > 0;
            HasAgeBars = AgeBars.Count > 0;
            HasStats = StatsTotalAnswers > 0
                       && (HasGenderBars || HasAgeBars
                           || HasHardestQuestion || HasEasiestQuestion);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] LoadDetailedStats: {ex.Message}");
            HasStats = false;
        }
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        StopAllTimers();
        try
        {
            await _quizService.LeaveSeriesAsync(SeriesId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] Leave: {ex.Message}");
        }
        await Shell.Current.GoToAsync("..");
    }

    // ═══════════════════════════════════════════════════════════════
    // REALTIME HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private async void OnRealtimeSeriesChanged(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            // Comme dans SeriesVoteViewModel : on recharge depuis la BDD plutôt que
            // de parser le payload, c'est plus simple et fiable.
            var fresh = await _seriesService.GetSeriesAsync(SeriesId);
            if (fresh == null) return;

            var prev = _series;
            _series = fresh;

            // Pause / reprise
            IsPaused = fresh.TvPaused;

            // TV désactivée → on quitte
            if (!fresh.TvActive)
            {
                StopAllTimers();
                ShowError(L.T("Quiz_Error_TvDeactivatedTitle"),
                          L.T("Quiz_Error_TvDeactivatedMessage"));
                return;
            }

            // 🎬 Game start : preparing → active. On affiche une interstitielle
            // côté joueur (fire-and-forget, jamais bloquant). La pub se superpose
            // pendant l'intro ; le state machine continue en arrière-plan. Une fois
            // la pub fermée, on signale à la TV pour qu'elle puisse démarrer Q1
            // dès que tous les joueurs ont signé.
            if (prev != null && prev.Status == "preparing" && fresh.Status == "active")
            {
                _ = ShowInterstitialThenSignalAsync();
            }

            // Status change ou index change → reroute
            if (prev == null
                || prev.Status != fresh.Status
                || prev.CurrentProjectIndex != fresh.CurrentProjectIndex)
            {
                CurrentIndex = fresh.CurrentProjectIndex;
                _hasTriggeredReveal = false;
                _selectedOptionId = null;
                RenderCurrentScreen();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] OnSeriesChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeQuizQuestionChanged(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            // Une question vient d'avoir son started_at posé. On recharge la liste
            // pour avoir tous les started_at à jour, puis on re-route.
            _questions = await _quizService.GetQuestionsAsync(SeriesId);

            if (_series != null)
            {
                var idx = _series.CurrentProjectIndex;
                if (idx >= 0 && idx < _questions.Count)
                {
                    var q = _questions[idx];
                    if (q.StartedAt != null && _currentQuestionId != q.Id)
                    {
                        // C'est une nouvelle question qui démarre
                        RenderCurrentScreen();
                    }
                }
                // Cas spécial : on était en INTRO et Q[0].started_at vient d'être posé
                else if (IsIntro && _questions.Count > 0 && _questions[0].StartedAt != null)
                {
                    RenderCurrentScreen();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] OnQuizQuestionChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeQuizAnswerReceived(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            // On rafraîchit le compteur de réponses pour la question courante
            if (!string.IsNullOrEmpty(_currentQuestionId))
            {
                AnswerCount = await _quizService.GetQuestionAnswerCountAsync(_currentQuestionId);
                UpdateAnswerCountLabel();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] OnAnswerReceived: {ex.Message}");
        }
    }

    private async void OnRealtimeParticipantJoined(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;
        try
        {
            ParticipantCount = await _quizService.GetParticipantCountAsync(SeriesId);
            UpdateAnswerCountLabel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] OnParticipantJoined: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private void HideAllScreens()
    {
        IsLobby = false;
        IsIntro = false;
        IsQuestion = false;
        IsAnswered = false;
        IsReveal = false;
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

    /// <summary>
    /// Affiche la pub interstitielle (ou skip si pas dispo) puis signale à la TV
    /// que ce joueur a fini, pour qu'elle puisse démarrer Q1 dès que tous ont signé.
    /// </summary>
    private async Task ShowInterstitialThenSignalAsync()
    {
        try { await _adService.ShowInterstitialBeforeGameStartAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[QuizPlay] Interstitial ex: {ex.Message}"); }
        finally { await _seriesService.MarkInterstitialSeenAsync(SeriesId); }
    }

    private void StopAllTimers()
    {
        StopCountdownTimer();
        StopIntroTimer();
    }

    private void StopCountdownTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer = null;
        }
    }

    private void StopIntroTimer()
    {
        if (_introTimer != null)
        {
            _introTimer.Stop();
            _introTimer.Tick -= OnIntroTick;
            _introTimer = null;
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
            System.Diagnostics.Debug.WriteLine($"[QuizPlay] Cleanup: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _realtime.OnSeriesChanged = null;
        _realtime.OnQuizQuestionChanged = null;
        _realtime.OnQuizAnswerReceived = null;
        _realtime.OnParticipantJoined = null;
        StopAllTimers();
        _ = _realtime.UnsubscribeAsync();
    }
}

// ═══════════════════════════════════════════════════════════════════
// ITEM MODELS
// ═══════════════════════════════════════════════════════════════════

public partial class QuizPlayOptionItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Letter { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isCorrect;
    [ObservableProperty] private bool _isRevealed;
}

public class QuizPlayLeaderItem
{
    public int Rank { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsMe { get; set; }
}

/// <summary>
/// Une barre de statistique sur l'écran finish (par genre ou tranche d'âge).
/// Contient le label, le nombre de joueurs, le pourcentage et le taux de
/// réussite. Affichée comme une barre horizontale avec chiffres dedans.
/// </summary>
public class QuizPlayStatBarItem
{
    /// <summary>Libellé affiché à gauche de la barre (ex: "Hommes", "18 - 29 ans").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Nombre de joueurs dans cette catégorie.</summary>
    public int Count { get; set; }

    /// <summary>Pourcentage de joueurs dans cette catégorie sur le total (0-100).</summary>
    public int Percent { get; set; }

    /// <summary>Taux de réussite des joueurs de cette catégorie (0-100).</summary>
    public int SuccessRate { get; set; }

    /// <summary>
    /// Texte affiché DANS la barre (ex: "5 joueurs (42%)").
    /// Calculé une fois côté C# pour éviter d'avoir un converter complexe en XAML.
    /// </summary>
    public string BarText => $"{Count} • {Percent}%";

    /// <summary>
    /// Texte secondaire affiché APRÈS la barre (taux de réussite de la catégorie).
    /// Ex: "Bon à 75%".
    /// </summary>
    public string SuccessRateText => $"✓ {SuccessRate}%";

    /// <summary>
    /// Largeur relative de la barre, entre 0.0 et 1.0. Bindée sur Grid.ColumnDefinitions
    /// avec un ratio (largeur barre = Percent%, reste = 100-Percent%).
    /// </summary>
    public GridLength FilledWidth => new GridLength(Math.Max(1, Percent), GridUnitType.Star);

    /// <summary>
    /// Largeur relative de la zone vide après la barre.
    /// </summary>
    public GridLength EmptyWidth => new GridLength(Math.Max(1, 100 - Percent), GridUnitType.Star);
}
