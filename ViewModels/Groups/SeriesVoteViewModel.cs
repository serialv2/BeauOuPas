using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;
using BeauOuPas.Localization;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class SeriesVoteViewModel : ObservableObject, IDisposable
{
    private readonly SeriesService _seriesService;
    private readonly SeriesVoteService _voteService;
    private readonly PhotoUploadService _uploadService;
    private readonly SeriesRealtimeService _realtime;
    private readonly IAdService _adService;

    // Statut précédent suivi localement pour détecter la transition
    // preparing → active (= moment du game start) et déclencher la pub.
    private string? _lastStatus = null;

    // ⚡ Doit correspondre à VOTE_DURATION_SECONDS dans tv-config.js
    private const int VoteSeconds = 20;

    private IDispatcherTimer? _countdownTimer;
    private int _secondsLeft = VoteSeconds;
    private List<SeriesProject> _seriesProjects = new();
    private bool _isDisposed = false;

    private bool _isInitializing = false;
    private bool _isInitialized = false;
    private readonly object _initLock = new object();

    private bool _isAdvancing = false;
    private DateTime? _projectStartedAt = null;
    // ─── Selfie : taux adaptatif selon nb de participants ─────────
    // Calculé une seule fois dans InitAsync.
    // Formule : taux = clamp(0.60 / nbParticipants, 0.01, 0.60)
    // → 1 personne : 60% / 4 personnes : 15% / 100 personnes : 0.6%
    private double _selfieRate = 0.0;
    private static readonly Random _selfieRandom = new Random();
    public Func<Task<Stream?>>? CaptureSelfieRequested { get; set; }

    public SeriesVoteViewModel(
        SeriesService seriesService,
        SeriesVoteService voteService,
        PhotoUploadService uploadService,
        SeriesRealtimeService realtime,
        IAdService adService)
    {
        _seriesService = seriesService;
        _voteService = voteService;
        _uploadService = uploadService;
        _realtime = realtime;
        _adService = adService;

        _realtime.OnSeriesChanged = OnRealtimeSeriesChanged;
        _realtime.OnSeriesProjectChanged = OnRealtimeProjectChanged;
        _realtime.OnVoteReceived = OnRealtimeVoteReceived;
        _realtime.OnParticipantJoined = OnRealtimeParticipantJoined;
    }

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isVoting = false;
    [ObservableProperty] private bool _isShowingResults = false;
    [ObservableProperty] private bool _isSeriesFinished = false;

    // ⚡ NOUVEAU : true tant que la TV n'a pas démarré le projet (started_at null)
    [ObservableProperty] private bool _isWaitingForTv = false;

    [ObservableProperty] private bool _isSelfieCapturing = false;

    // ⚡ NOUVEAU : true si la série est en pause (lu depuis Realtime, pas localement)
    [ObservableProperty] private bool _isPaused = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIndex))]
    [NotifyPropertyChangedFor(nameof(ProjectIndexLabel))]
    private int _currentIndex = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectIndexLabel))]
    private int _totalProjects = 0;

    [ObservableProperty] private string _currentProjectTitle = string.Empty;
    [ObservableProperty] private string _currentProjectType = string.Empty;
    [ObservableProperty] private string _currentSeriesProjectId = string.Empty;
    [ObservableProperty] private bool _currentSelfieEnabled = true;

    public int DisplayIndex => CurrentIndex + 1;
    public string ProjectIndexLabel => TotalProjects > 0
        ? L.F("SeriesVote_ProjectIndex", DisplayIndex, TotalProjects)
        : L.F("SeriesVote_ProjectIndexSingle", DisplayIndex);

    [ObservableProperty] private string _photoUrl = string.Empty;
    [ObservableProperty] private bool _isPhotoType = false;
    [ObservableProperty] private string _photoLeftUrl = string.Empty;
    [ObservableProperty] private string _photoRightUrl = string.Empty;
    [ObservableProperty] private bool _isDuelType = false;
    [ObservableProperty] private List<PollOptionItem> _pollOptions = new();
    [ObservableProperty] private bool _isPollType = false;

    [ObservableProperty] private int _countdown = VoteSeconds;
    [ObservableProperty] private string _countdownColor = "#C2754C";
    [ObservableProperty] private int _voteCount = 0;
    [ObservableProperty] private int _participantCount = 0;
    [ObservableProperty] private string _voteProgress = "0/0 ont voté";
    [ObservableProperty] private bool _hasVoted = false;

    [ObservableProperty] private SeriesProjectResults _currentResults = new();

    // ─────────────────────────────────────────────────────────────────
    // INITIALISATION
    // ─────────────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        lock (_initLock)
        {
            if (_isDisposed) return;
            if (_isInitialized || _isInitializing)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesVote] InitAsync ignoré (initializing={_isInitializing}, initialized={_isInitialized})");
                return;
            }
            _isInitializing = true;
        }

        try
        {
            IsLoading = true;

            // 1) Rejoindre la série
            // 1) Rejoindre la série
            await _voteService.JoinSeriesAsync(SeriesId);

            // 1.bis) Calculer le taux de selfie selon le nb de participants
            // (rejointe juste avant, donc on est compté)
            try
            {
                var participantCount = await _voteService.GetParticipantCountAsync(SeriesId);
                if (participantCount < 1) participantCount = 1;
                _selfieRate = Math.Clamp(0.60 / participantCount, 0.01, 0.60);
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesVote] Selfie rate: {_selfieRate:P1} ({participantCount} participants)");
            }
            catch (Exception ex)
            {
                _selfieRate = 0.15;  // fallback prudent
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesVote] Selfie rate fallback to 15%: {ex.Message}");
            }
            // 2) Charger les projets
            _seriesProjects = await _seriesService.GetSeriesProjectsAsync(SeriesId);
            TotalProjects = _seriesProjects.Count;

            // 3) Charger l'état actuel de la série
            var series = await _seriesService.GetSeriesAsync(SeriesId);
            if (series == null)
            {
                IsLoading = false;
                _isInitializing = false;
                return;
            }

            if (series.Status == "finished")
            {
                IsSeriesFinished = true;
                IsLoading = false;
                _isInitialized = true;
                _isInitializing = false;
                return;
            }

            CurrentIndex = series.CurrentProjectIndex;
            ParticipantCount = await _voteService.GetParticipantCountAsync(SeriesId);
            IsPaused = series.TvPaused;
            _lastStatus = series.Status;

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVote] Init : statut={series.Status}, projet={CurrentIndex + 1}/{TotalProjects}, paused={IsPaused}");

            _isInitialized = true;

            // 4) S'abonner au Realtime AVANT de charger le projet courant.
            //    Comme ça, si la TV démarre/avance pendant le chargement,
            //    on reçoit l'event et on se met à jour.
            await _realtime.SubscribeAsync(SeriesId);

            // 4bis) Précharge systématiquement l'interstitielle. Affichée :
            //  - soit sur la transition preparing → active (cas multi-joueurs synchrones)
            //  - soit ici même si on arrive directement sur le 1er projet pas démarré
            //    (animateur solo qui lance la série depuis l'écran TV : status est
            //    déjà "active" à l'init donc OnRealtimeSeriesChanged ne voit jamais
            //    la transition).
            _ = _adService.LoadInterstitialAsync();

            if (series.Status == "active"
                && _seriesProjects.Count > 0
                && _seriesProjects[0].StartedAt == null)
            {
                _ = ShowInterstitialThenSignalAsync();
            }

            // 5) Charger le projet courant (avec son started_at, qui peut être null
            //    si la TV n'a pas encore démarré ou pas encore avancé).
            await LoadCurrentProjectAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] Init: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _isInitializing = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // CHARGEMENT DU PROJET COURANT
    // ─────────────────────────────────────────────────────────────────

    private async Task LoadCurrentProjectAsync()
    {
        if (_isDisposed) return;

        if (CurrentIndex >= _seriesProjects.Count)
        {
            IsSeriesFinished = true;
            IsVoting = false;
            IsShowingResults = false;
            IsWaitingForTv = false;
            return;
        }

        StopTimer();

        // Reset complet de l'état avant de charger le projet suivant
        _isAdvancing = false;
        HasVoted = false;
        IsVoting = false;
        IsShowingResults = false;
        IsSelfieCapturing = false;
        IsWaitingForTv = false;

        var sp = _seriesProjects[CurrentIndex];
        CurrentSeriesProjectId = sp.Id;
        CurrentSelfieEnabled = sp.SelfieEnabled;

        _projectStartedAt = sp.StartedAt;
        System.Diagnostics.Debug.WriteLine(
            $"[SeriesVote] Projet {CurrentIndex + 1}/{TotalProjects} - started_at={_projectStartedAt}");

        var project = await _seriesService.GetProjectAsync(sp.ProjectId);
        if (project == null)
        {
            System.Diagnostics.Debug.WriteLine("[SeriesVote] Projet introuvable !");
            return;
        }

        CurrentProjectTitle = project.Title;
        CurrentProjectType = project.Type;
        IsPhotoType = project.Type == "photo_vote";
        IsDuelType = project.Type == "duel";
        IsPollType = project.Type == "poll";

        PhotoUrl = string.Empty;
        PhotoLeftUrl = string.Empty;
        PhotoRightUrl = string.Empty;
        PollOptions = new();

        if (project.Type == "photo_vote")
        {
            var photos = await _seriesService.GetProjectPhotosAsync(sp.ProjectId);
            PhotoUrl = photos.FirstOrDefault()?.Url ?? string.Empty;
        }
        else if (project.Type == "duel")
        {
            var photos = await _seriesService.GetProjectPhotosAsync(sp.ProjectId);
            PhotoLeftUrl = photos.FirstOrDefault(p => p.Side == "left")?.Url ?? string.Empty;
            PhotoRightUrl = photos.FirstOrDefault(p => p.Side == "right")?.Url ?? string.Empty;
        }
        else if (project.Type == "poll")
        {
            var options = await _seriesService.GetPollOptionsAsync(sp.ProjectId);
            PollOptions = options.Select(o => new PollOptionItem
            {
                Id = o.Id,
                Text = o.Text
            }).ToList();
        }

        VoteCount = await _voteService.GetVoteCountAsync(sp.Id);
        UpdateVoteProgress();
        HasVoted = await _voteService.HasUserVotedAsync(sp.Id);

        // ⚡ NOUVEAU : si started_at est null, la TV n'a pas encore démarré ce projet.
        //    On affiche "En attente de la TV..." au lieu d'inventer un timer.
        if (!HasValidStartedAt())
        {
            System.Diagnostics.Debug.WriteLine(
                "[SeriesVote] Pas de started_at → attente de la TV");
            IsWaitingForTv = true;
            IsVoting = false;
            return;
        }

        // started_at est OK : on peut afficher la page de vote et démarrer le countdown
        IsVoting = true;
        IsShowingResults = false;
        IsWaitingForTv = false;

        StartCountdown();
    }

    /// <summary>
    /// Vérifie si on a un started_at valide (non null et année > 2000 pour
    /// éviter les MIN_VALUE qui pourraient remonter de la DB).
    /// </summary>
    private bool HasValidStartedAt()
        => _projectStartedAt.HasValue && _projectStartedAt.Value.Year > 2000;

    // ─────────────────────────────────────────────────────────────────
    // COUNTDOWN — calculé depuis started_at, source unique de vérité
    // ─────────────────────────────────────────────────────────────────

    private void StartCountdown()
    {
        StopTimer();

        if (!HasValidStartedAt())
        {
            // Sécurité : ne devrait jamais arriver vu LoadCurrentProjectAsync
            System.Diagnostics.Debug.WriteLine(
                "[SeriesVote] StartCountdown sans started_at, abort");
            IsWaitingForTv = true;
            IsVoting = false;
            return;
        }

        // Calcul initial du temps restant
        UpdateSecondsLeftFromStartedAt();
        Countdown = _secondsLeft;
        UpdateCountdownColor();

        System.Diagnostics.Debug.WriteLine(
            $"[SeriesVote] StartCountdown : {_secondsLeft}s restantes");

        _countdownTimer = Application.Current!.Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromMilliseconds(500);
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    /// <summary>
    /// Recalcule _secondsLeft depuis started_at + VoteSeconds.
    /// Cette méthode est la SEULE source de vérité du compteur.
    /// </summary>
    private void UpdateSecondsLeftFromStartedAt()
    {
        if (!HasValidStartedAt())
        {
            _secondsLeft = 0;
            return;
        }

        var startedAtUtc = _projectStartedAt!.Value.Kind == DateTimeKind.Utc
            ? _projectStartedAt.Value
            : DateTime.SpecifyKind(_projectStartedAt.Value, DateTimeKind.Utc);

        var elapsed = (DateTime.UtcNow - startedAtUtc).TotalSeconds;
        var remaining = (int)Math.Round(VoteSeconds - elapsed);

        if (remaining < 0) remaining = 0;
        if (remaining > VoteSeconds) remaining = VoteSeconds;

        _secondsLeft = remaining;
    }

    private void UpdateCountdownColor()
    {
        CountdownColor = _secondsLeft switch
        {
            <= 5 => "#B5482F",
            <= 10 => "#C9943E",
            _ => "#C2754C"
        };
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (_isDisposed || _isAdvancing) return;

        // Si la série est en pause, on ne décrémente pas le compteur
        if (IsPaused) return;

        UpdateSecondsLeftFromStartedAt();
        Countdown = _secondsLeft;
        UpdateCountdownColor();

        if (_secondsLeft <= 0)
        {
            _isAdvancing = true;
            StopTimer();

            // ⚡ Le téléphone N'AVANCE PAS la série lui-même.
            //    C'est la TV qui appelle tv_advance_to_next quand son timer expire.
            //    Le téléphone attend juste l'event Realtime "series UPDATE" qui
            //    arrivera avec le nouveau current_project_index.
            //    En attendant, on affiche "Vote terminé" / "En attente du suivant".
            HasVoted = true;
            IsWaitingForTv = true;
            IsVoting = false;
            System.Diagnostics.Debug.WriteLine(
                "[SeriesVote] Timer fini, attente du signal de la TV");
        }
    }

    private void UpdateVoteProgress()
        => VoteProgress = $"{VoteCount}/{ParticipantCount} ont voté";

    // ─────────────────────────────────────────────────────────────────
    // VOTE
    // ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task VoteAsync(string value)
    {
        if (HasVoted || _isDisposed) return;

        var spIdAtTap = CurrentSeriesProjectId;
        if (string.IsNullOrEmpty(spIdAtTap)) return;

        HasVoted = true;

        var (success, error) = await _voteService.VoteAsync(
            SeriesId, spIdAtTap, value);

        if (!success)
        {
            HasVoted = false;
            await Shell.Current.DisplayAlert(
                L.T("SeriesVote_VoteError_Title"),
                L.F("SeriesVote_VoteError_Msg", error),
                L.T("Common_OK"));
            return;
        }

        if (spIdAtTap == CurrentSeriesProjectId && !_isDisposed)
        {
            VoteCount = await _voteService.GetVoteCountAsync(spIdAtTap);
            UpdateVoteProgress();
        }

        // Selfie : un seul par session
        // ─── Selfie : tirage probabiliste adaptatif ────────────────
        // Taux calculé dans InitAsync selon le nb de participants.
        // Pas de cap : tout le monde a sa chance à chaque projet.
        // Upload étalé sur 0-10s pour lisser le pic réseau si gros événement.
        bool shouldDoSelfie = CurrentSelfieEnabled
            && !_isDisposed
            && _selfieRate > 0
            && _selfieRandom.NextDouble() < _selfieRate;

        if (shouldDoSelfie)
        {
            // ⚡ Capture immédiate (pendant que la cam est dispo) puis upload
            // en background avec délai aléatoire — ne bloque PAS le vote.
            // Fire and forget intentionnel : le vote suivant doit pouvoir
            // partir sans attendre la fin de la chaîne capture+upload.
            _ = CaptureAndUploadSelfieAsync(spIdAtTap);
        }
    }

    // ─── Capture + upload selfie en arrière-plan ──────────────────
    /// <summary>
    /// Capture immédiate de la photo (pour profiter de la dispo caméra),
    /// puis upload différé d'un délai aléatoire 0-10s (anti-pic réseau).
    /// </summary>
    private async Task CaptureAndUploadSelfieAsync(string seriesProjectId)
    {
        IsSelfieCapturing = true;
        try
        {
            if (CaptureSelfieRequested == null) return;

            // 1) Capture immédiate (la cam est prête, on en profite)
            using var stream = await CaptureSelfieRequested.Invoke();
            if (stream == null || _isDisposed)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesVote] Selfie stream null (capture failed/denied/disposed)");
                return;
            }

            // 2) Recopie le stream en mémoire (le using va le fermer)
            using var bufferedStream = new MemoryStream();
            await stream.CopyToAsync(bufferedStream);

            // 3) Délai aléatoire 0-10s pour étaler les uploads
            var delayMs = _selfieRandom.Next(0, 10_001);
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVote] Selfie captured, upload in {delayMs}ms");
            await Task.Delay(delayMs);

            if (_isDisposed) return;

            // 4) Upload en background
            bufferedStream.Position = 0;
            var ok = await _voteService.UploadSelfieAsync(
                SeriesId, seriesProjectId, bufferedStream,
                $"selfie_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg");

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVote] Selfie upload result: {ok} (project {seriesProjectId})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVote] CaptureAndUploadSelfie: {ex.Message}");
        }
        finally
        {
            IsSelfieCapturing = false;
        }
    }

    [RelayCommand]
    private async Task GoToResultsAsync()
        => await Shell.Current.GoToAsync("SeriesResultsPage",
            new Dictionary<string, object>
            {
                { "SeriesId", SeriesId },
                { "SeriesTitle", SeriesTitle }
            });

    // ─────────────────────────────────────────────────────────────────
    // Interstitielle + signal à la TV
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Affiche la pub interstitielle (ou skip si pas dispo) puis signale à la TV
    /// que ce joueur a fini, pour qu'elle puisse démarrer le 1er projet dès que
    /// tous ont signé (sans attendre la fin du countdown intro complet).
    /// </summary>
    private async Task ShowInterstitialThenSignalAsync()
    {
        try { await _adService.ShowInterstitialBeforeGameStartAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SeriesVote] Interstitial ex: {ex.Message}"); }
        finally { await _seriesService.MarkInterstitialSeenAsync(SeriesId); }
    }

    // ─────────────────────────────────────────────────────────────────
    // GESTION DU TIMER
    // ─────────────────────────────────────────────────────────────────

    private void StopTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer.Stop();
            _countdownTimer = null;
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        StopTimer();
    }

    public void Reset()
    {
        StopTimer();
        lock (_initLock)
        {
            _isDisposed = false;
            _isInitialized = false;
            _isInitializing = false;
            _isAdvancing = false;
        }
    }
    /// <summary>
    /// ⚡ NOUVEAU : appelée par le code-behind quand l'utilisateur confirme
    /// "Quitter la série". On le retire de la liste des participants pour
    /// que le compteur de la TV se mette à jour.
    /// </summary>
    public async Task LeaveSeriesAsync()
    {
        try
        {
            await _voteService.LeaveSeriesAsync(SeriesId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] LeaveSeries: {ex.Message}");
        }
    }
    public async Task CleanupAsync()
    {
        try
        {
            await _realtime.UnsubscribeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] Cleanup: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HANDLERS REALTIME
    // ═══════════════════════════════════════════════════════════════

    private async void OnRealtimeSeriesChanged(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;

        try
        {
            var series = await _seriesService.GetSeriesAsync(SeriesId);
            if (series == null) return;

            // 🎬 Game start : preparing → active. Interstitielle non-bloquante.
            // (la pub se superpose au démarrage, on ne bloque pas le state machine).
            // Une fois la pub fermée, on signale à la TV via mark_interstitial_seen.
            if (_lastStatus == "preparing" && series.Status == "active")
            {
                _ = ShowInterstitialThenSignalAsync();
            }
            _lastStatus = series.Status;

            // Pause / reprise depuis la TV
            if (IsPaused != series.TvPaused)
            {
                IsPaused = series.TvPaused;
                System.Diagnostics.Debug.WriteLine($"[SeriesVote] Pause changée : {IsPaused}");
            }

            // Série terminée par la TV
            if (series.Status == "finished" && !IsSeriesFinished)
            {
                System.Diagnostics.Debug.WriteLine("[SeriesVote] Série terminée, navigation vers résultats");
                IsSeriesFinished = true;
                IsVoting = false;
                IsShowingResults = false;
                IsWaitingForTv = false;
                StopTimer();
                await Shell.Current.GoToAsync("SeriesResultsPage",
                    new Dictionary<string, object>
                    {
                        { "SeriesId", SeriesId },
                        { "SeriesTitle", SeriesTitle }
                    });
                return;
            }

            // La TV a avancé au projet suivant
            if (series.CurrentProjectIndex != CurrentIndex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesVote] TV avance au projet {series.CurrentProjectIndex + 1} (était {CurrentIndex + 1})");

                StopTimer();
                _isAdvancing = false;
                HasVoted = false;

                CurrentIndex = series.CurrentProjectIndex;

                // On recharge la liste des projets pour avoir les started_at à jour
                _seriesProjects = await _seriesService.GetSeriesProjectsAsync(SeriesId);

                await LoadCurrentProjectAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] OnRealtimeSeriesChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeProjectChanged(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;

        try
        {
            // ⚡ CRITIQUE : c'est ICI qu'on reçoit le started_at posé par la TV.
            //    Si on était en "attente de la TV", on bascule sur la page de vote.
            _seriesProjects = await _seriesService.GetSeriesProjectsAsync(SeriesId);
            if (CurrentIndex < _seriesProjects.Count)
            {
                var sp = _seriesProjects[CurrentIndex];
                if (sp.Id == CurrentSeriesProjectId && sp.StartedAt != _projectStartedAt)
                {
                    _projectStartedAt = sp.StartedAt;
                    System.Diagnostics.Debug.WriteLine(
                        $"[SeriesVote] started_at mis à jour : {_projectStartedAt}");

                    // ⚡ Si on attendait la TV et qu'on a maintenant un started_at,
                    //    on bascule sur la page de vote et on lance le countdown.
                    if (HasValidStartedAt())
                    {
                        IsWaitingForTv = false;
                        IsVoting = true;
                        IsShowingResults = false;
                        StartCountdown();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] OnRealtimeProjectChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeVoteReceived(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;

        try
        {
            var spId = CurrentSeriesProjectId;
            if (string.IsNullOrEmpty(spId)) return;
            VoteCount = await _voteService.GetVoteCountAsync(spId);
            UpdateVoteProgress();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] OnRealtimeVoteReceived: {ex.Message}");
        }
    }

    private async void OnRealtimeParticipantJoined(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        if (_isDisposed) return;

        try
        {
            ParticipantCount = await _voteService.GetParticipantCountAsync(SeriesId);
            UpdateVoteProgress();
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVote] Participant joined, total = {ParticipantCount}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVote] OnRealtimeParticipantJoined: {ex.Message}");
        }
    }
}

public class PollOptionItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}