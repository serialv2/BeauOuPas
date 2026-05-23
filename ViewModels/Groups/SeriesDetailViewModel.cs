using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.ViewModels.Projects;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using System.Text.Json;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Groups;
[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
[QueryProperty(nameof(GroupId), "GroupId")]
public partial class SeriesDetailViewModel : ObservableObject
{
    // ⚡ PARTIE RAPIDE : IsParty depend de IsQuiz + QuizStyle.
    partial void OnIsQuizChanged(bool value) => OnPropertyChanged(nameof(IsParty));
    partial void OnQuizStyleChanged(string value) => OnPropertyChanged(nameof(IsParty));

    private readonly SeriesService _seriesService;
    private readonly ProjectService _projectService;
    private readonly SeriesInvitationService _invitationService;
    private readonly SeriesRealtimeService _realtime;

    public SeriesDetailViewModel(
        SeriesService seriesService,
        ProjectService projectService,
        SeriesInvitationService invitationService,
        SeriesRealtimeService realtime)
    {
        _seriesService = seriesService;
        _projectService = projectService;
        _invitationService = invitationService;
        _realtime = realtime;

        // ⚡ Palier B : abonnement aux events Realtime
        _realtime.OnSeriesChanged = OnRealtimeSeriesChanged;
        _realtime.OnParticipantJoined = OnRealtimeParticipantJoined;
    }

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;
    [ObservableProperty] private string _groupId = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isCreator = false;

    // ⚡ NOUVEAU : true si l'utilisateur courant est inscrit à la série.
    // Sert au code-behind pour décider si Retour Android doit proposer
    // "Quitter la série" (participants) ou juste revenir en arrière (créateur).
    [ObservableProperty] private bool _isCurrentUserParticipant = false;

    // ─── Status & infos ─────────────────────────────────────────────
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _statusLabel = string.Empty;
    [ObservableProperty] private Color _statusColor = Colors.Gray;
    [ObservableProperty] private string? _accessCode = null;
    [ObservableProperty] private bool _isStandalone = false;
    [ObservableProperty] private int _maxProjects = 10;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectCountLabel))]
    private int _projectCount = 0;

    // Oubli i18n : libellé "X projet(s) ajouté(s)" traduit (StringFormat retiré du XAML)
    public string ProjectCountLabel => L.F("SeriesDetail_ProjectCount", ProjectCount);

    // ⚡ NOUVEAU : true si le créateur a coché "cacher les projets jusqu'au démarrage".
    [ObservableProperty] private bool _projectsHidden = true;

    // ─── Liste des projets (mode "révélé") ─────────────────────────
    [ObservableProperty] private List<SeriesProjectItem> _projects = new();
    [ObservableProperty] private bool _hasProjects = false;
    [ObservableProperty] private bool _noProjects = false;

    // ⚡ NOUVEAU : flag pour basculer entre vue lobby et vue révélée
    // (true uniquement si Status == preparing && ProjectsHidden)
    [ObservableProperty] private bool _isLobbyMode = false;

    // ⚡ Compteurs par type pour la vue lobby
    [ObservableProperty] private int _photoVoteCount = 0;
    [ObservableProperty] private int _duelCount = 0;
    [ObservableProperty] private int _pollCount = 0;
    [ObservableProperty] private int _totalProjectCount = 0;

    // Visibilité conditionnelle des compteurs
    [ObservableProperty] private bool _hasPhotoVote = false;
    [ObservableProperty] private bool _hasDuel = false;
    [ObservableProperty] private bool _hasPoll = false;

    // ⚡ Liste des participants avec leur compteur de projets ajoutés
    [ObservableProperty] private List<SeriesParticipantStat> _participants = new();
    [ObservableProperty] private int _participantCount = 0;
    [ObservableProperty] private bool _hasParticipants = false;

    // ⚡ Mes propres projets (visibles en clair pour moi en mode lobby)
    [ObservableProperty] private List<SeriesProjectItem> _myProjects = new();
    [ObservableProperty] private bool _hasMyProjects = false;

    // ⚡ Projets des autres (visibles UNIQUEMENT pour le créateur de la série,
    //    pour qu'il puisse modérer si quelqu'un poste un truc inapproprié).
    //    Pour les autres participants, ces projets restent cachés en mode lobby.
    [ObservableProperty] private List<SeriesProjectItem> _otherProjects = new();
    [ObservableProperty] private bool _hasOtherProjects = false;

    // ─── Boutons (vue créateur) ────────────────────────────────────
    [ObservableProperty] private bool _canStart = false;
    [ObservableProperty] private bool _canStop = false;
    [ObservableProperty] private bool _canReset = false;
    [ObservableProperty] private bool _canAddProjects = false;
    [ObservableProperty] private bool _canDelete = false;
    [ObservableProperty] private bool _canInvite = false;
    [ObservableProperty] private bool _canVote = false;

    // ─── ⚡ Mode QUIZZ : true si la série est un quiz ────────────────
    [ObservableProperty] private bool _isQuiz = false;

    // ─── 🎉 Mode PARTIE RAPIDE : style du quiz (kahoot/millionaire/
    // burger/weakest/party). Une "partie rapide" est techniquement un
    // quiz (is_quiz=true) avec quiz_style='party'. On lit ce champ pour
    // router le joueur vers PartyPlayPage au lieu de QuizPlayPage.
    [ObservableProperty] private string _quizStyle = string.Empty;

    // True si la série est une "Partie rapide" (Most Likely To).
    public bool IsParty => IsQuiz
        && string.Equals(QuizStyle, "party", System.StringComparison.OrdinalIgnoreCase);

    // ⚡ LOT 1 : drapeaux dérivés pour faciliter les bindings XAML
    // (évite des converters partout, et plus lisible)
    [ObservableProperty] private bool _isQuizMode = false;
    [ObservableProperty] private bool _isVoteMode = true;

    // ⚡ LOT 1 : libellé du bouton de jeu dynamique selon le type
    // ("▶ Voter maintenant" vs "▶ Jouer au quiz")
    // Valeur d'init neutre : écrasée par L.T(...) au chargement (voir UpdateFromSeries)
    [ObservableProperty] private string _playButtonLabel = string.Empty;

    // ⚡ LOT 1 : peut-on modifier les questions du quiz (créateur + preparing) ?
    [ObservableProperty] private bool _canEditQuiz = false;

    // 🔧 BUG C : peut-on voir la liste des questions ? Réservé au créateur,
    // pour ne pas spoiler les énoncés aux participants AVANT le quiz.
    // Calculé dans LoadAsync en même temps que les autres CanXxx.
    [ObservableProperty] private bool _canViewQuizQuestions = false;

    // ⚡ LOT 1 : drapeaux dérivés pour simplifier les bindings XAML
    // (évite d'avoir besoin du converter AllTrueConverter qui n'existe peut-être pas)
    // - ShowLobbyVote        : true si lobby + mode vote (les compteurs/participants)
    // - ShowProjectsListVote : true si pas lobby + mode vote (la liste détaillée)
    [ObservableProperty] private bool _showLobbyVote = false;
    [ObservableProperty] private bool _showProjectsListVote = false;

    // ─── ⚡ LOT 1 : Liste des questions du quiz ──────────────────────
    [ObservableProperty] private List<QuizQuestionDisplayItem> _questions = new();
    [ObservableProperty] private bool _hasQuestions = false;
    [ObservableProperty] private bool _noQuestions = false;
    [ObservableProperty] private int _questionCount = 0;

    // ─── ⚡ Lot Mode TV : état + boutons spécifiques ─────────────────
    [ObservableProperty] private bool _tvActive = false;
    [ObservableProperty] private bool _tvPaused = false;
    [ObservableProperty] private bool _animatorVotes = true;

    // Visibilité des boutons TV
    [ObservableProperty] private bool _canActivateTv = false;

    // Bouton "Démarrer la série" (mode TV, status preparing + tv_active)
    [ObservableProperty] private bool _canStartTv = false;

    // Boutons pause/désactivation (visibles si tv_active = true)
    [ObservableProperty] private bool _canControlTv = false;

    // Label pause/reprendre selon état
    [ObservableProperty] private string _tvPauseLabel = "⏸️ Pause";

    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : Stockage du JSON renvoyé par get_series_detail_full
    // (utilisé par LoadLobbyAsync et LoadRevealedProjectsAsync)
    // ═════════════════════════════════════════════════════════════════
    private JsonElement? _cachedAllProjectsArray = null;

    // ⚡ REFONDÉE : utilise la RPC get_series_detail_full (1 seul appel au lieu de 3+P+M)
    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(SeriesId)) return;
        IsLoading = true;
        try
        {
            // 1) Un seul appel RPC pour TOUT charger
            var json = await _seriesService.GetSeriesDetailFullJsonAsync(SeriesId);
            if (string.IsNullOrWhiteSpace(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 2) Extraire les infos série
            if (!root.TryGetProperty("series", out var seriesEl)
                || seriesEl.ValueKind != JsonValueKind.Object) return;

            // ─── Stocker le tableau de projets pour LoadProjectsAsync ───
            // Clone pour pouvoir y accéder après le 'using' du JsonDocument
            if (root.TryGetProperty("all_projects", out var allProjectsEl)
                && allProjectsEl.ValueKind == JsonValueKind.Array)
            {
                _cachedAllProjectsArray = JsonDocument.Parse(allProjectsEl.GetRawText()).RootElement;
            }
            else
            {
                _cachedAllProjectsArray = null;
            }

            // ─── Stocker les participants ───
            if (root.TryGetProperty("participants", out var participantsEl)
                && participantsEl.ValueKind == JsonValueKind.Array)
            {
                var participants = new List<SeriesParticipantStat>();
                foreach (var pEl in participantsEl.EnumerateArray())
                {
                    participants.Add(new SeriesParticipantStat
                    {
                        UserId = GetJsonString(pEl, "user_id") ?? string.Empty,
                        Username = GetJsonString(pEl, "username") ?? "?",
                        ProjectCount = GetJsonInt(pEl, "project_count")
                    });
                }
                Participants = participants;
            }
            else
            {
                Participants = new();
            }
            ParticipantCount = Participants.Count;
            HasParticipants = ParticipantCount > 0;

            // ─── Configuration depuis la série (lecture du JSON) ───
            IsCreator = GetJsonBool(root, "is_creator");
            var isParticipant = GetJsonBool(root, "is_participant");

            Status = GetJsonString(seriesEl, "status") ?? string.Empty;
            AccessCode = GetJsonString(seriesEl, "access_code");
            IsStandalone = GetJsonBool(seriesEl, "is_standalone");
            IsQuiz = GetJsonBool(seriesEl, "is_quiz");
            // 🎉 PARTIE RAPIDE : quiz_style distingue 'party' d'un quiz
            // classique. Fallback "" si le champ est absent (legacy).
            QuizStyle = GetJsonString(seriesEl, "quiz_style") ?? string.Empty;
            MaxProjects = GetJsonInt(seriesEl, "max_projects");

            // ⚡ LOT 1 : drapeaux dérivés pour faciliter les bindings XAML
            IsQuizMode = IsQuiz;
            IsVoteMode = !IsQuiz;
            PlayButtonLabel = IsQuiz ? L.T("SeriesDetail_PlayButton_Quiz") : L.T("SeriesDetail_PlayButton_Vote");

            // projects_hidden peut être null (legacy) → traiter comme caché par sécurité
            if (seriesEl.TryGetProperty("projects_hidden", out var phEl)
                && phEl.ValueKind == JsonValueKind.False)
            {
                ProjectsHidden = false;
            }
            else
            {
                ProjectsHidden = true;
            }

            // ⚡ MAI 2026 — B2 : flag "membres peuvent ajouter projets".
            // Default à true pour ne pas casser le comportement des séries
            // pré-mai 2026 qui n'ont pas la colonne (legacy).
            bool membersCanAddProjects = true;
            if (seriesEl.TryGetProperty("members_can_add_projects", out var mcapEl)
                && mcapEl.ValueKind == JsonValueKind.False)
            {
                membersCanAddProjects = false;
            }

            StatusLabel = Status switch
            {
                "preparing" => L.T("SeriesDetail_Status_Preparing"),
                "active" => L.T("SeriesDetail_Status_Active"),
                "finished" => L.T("SeriesDetail_Status_Finished"),
                _ => Status
            };

            StatusColor = Status switch
            {
                "preparing" => Color.FromArgb("#C9943E"),
                "active" => Color.FromArgb("#4A7A52"),
                "finished" => Color.FromArgb("#8A6F4A"),
                _ => Color.FromArgb("#8A6F4A")
            };

            // ⚡ LOT 1 : un quiz n'a pas de "projets" - on désactive l'ajout
            // de projets dans tous les cas pour éviter d'embrouiller l'utilisateur.
            // ⚡ MAI 2026 — B2 : les participants peuvent ajouter des projets
            // uniquement si le créateur a coché "Les membres peuvent ajouter
            // des projets" lors de la création. Le créateur peut toujours.
            CanAddProjects = !IsQuiz
                             && Status == "preparing"
                             && (IsCreator || (isParticipant && membersCanAddProjects));
            CanStart = IsCreator && Status == "preparing";
            CanStop = IsCreator && Status == "active";
            CanReset = IsCreator && (Status == "active" || Status == "finished");
            CanDelete = IsCreator;
            CanVote = Status == "active";

            // ⚡ LOT 1 : pour un quiz en préparation, le créateur peut modifier
            // les questions (rouvre la page de création/édition)
            CanEditQuiz = IsQuiz && IsCreator && Status == "preparing";

            // 🔧 BUG C : la liste des questions n'est visible QUE pour le
            // créateur du quiz, pour ne pas spoiler les énoncés aux participants.
            // 🎉 PARTIE RAPIDE : on NE montre PAS la liste des questions
            // (même au créateur) pour garder la surprise pendant la partie.
            CanViewQuizQuestions = IsQuiz && IsCreator && !IsParty;

            // ⚡ Lot Mode TV : lire l'état TV
            TvActive = GetJsonBool(seriesEl, "tv_active");
            TvPaused = GetJsonBool(seriesEl, "tv_paused");
            AnimatorVotes = GetJsonBool(seriesEl, "animator_votes");

            // Visibilité des boutons TV (uniquement pour le créateur)
            CanActivateTv = IsCreator && !TvActive && Status == "preparing";
            CanStartTv = IsCreator && TvActive && Status == "preparing";
            CanControlTv = IsCreator && TvActive;
            TvPauseLabel = TvPaused ? "▶ Reprendre" : "⏸️ Pause";

            // Quand le mode TV est actif, on cache le bouton "Démarrer" normal
            // (on utilise StartTvSeries à la place qui appelle la RPC tv_start_series)
            if (CanActivateTv || CanStartTv)
            {
                CanStart = false;  // remplacé par les boutons TV
            }

            // Tout participant peut inviter (sauf si la série est terminée)
            CanInvite = (isParticipant || IsCreator) && Status != "finished";

            // ⚡ NOUVEAU : on stocke aussi cette info pour la popup "Quitter la série"
            // affichée par le code-behind quand l'utilisateur fait Retour Android.
            IsCurrentUserParticipant = isParticipant;

            // 3) Charger les projets OU les questions selon le type
            if (IsQuiz)
            {
                await LoadQuizQuestionsAsync();
                // Pour un quiz, on ne fait pas de lobby projets : on cache tous les
                // états "lobby vote" et la liste de projets standard
                IsLobbyMode = false;
                ResetVoteSpecificFields();
            }
            else
            {
                await LoadProjectsAsync();
                // Pour un vote, on vide la liste questions (au cas où)
                Questions = new();
                HasQuestions = false;
                NoQuestions = false;
                QuestionCount = 0;
            }

            // ⚡ LOT 1 : calculer les flags dérivés une fois IsLobbyMode connu
            ShowLobbyVote = IsVoteMode && IsLobbyMode;
            ShowProjectsListVote = IsVoteMode && !IsLobbyMode;

            // 4) Realtime : démarrer pour suivre la série en live
            await _realtime.SubscribeAsync(SeriesId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SeriesDetail.Load: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// ⚡ LOT 1 : pour un quiz, on remet à zéro tous les compteurs/listes
    /// liés à la logique vote, pour que l'UI vote ne s'affiche pas par accident
    /// (un quiz n'a pas de projets ni de lobby vote).
    /// </summary>
    private void ResetVoteSpecificFields()
    {
        ProjectCount = 0;
        TotalProjectCount = 0;
        PhotoVoteCount = 0;
        DuelCount = 0;
        PollCount = 0;
        HasPhotoVote = false;
        HasDuel = false;
        HasPoll = false;
        Projects = new();
        HasProjects = false;
        NoProjects = false;
        MyProjects = new();
        HasMyProjects = false;
        OtherProjects = new();
        HasOtherProjects = false;
    }

    /// <summary>
    /// ⚡ LOT 1 : charge la liste des questions d'un quiz (créateur uniquement
    /// affiche un détail riche). Lecture seule dans cette page : pour modifier,
    /// on rouvre CreateQuizPage via EditQuizQuestionsAsync.
    /// </summary>
    private async Task LoadQuizQuestionsAsync()
    {
        try
        {
            var rawQuestions = await _seriesService.GetQuizQuestionsAsync(SeriesId);

            var displayItems = rawQuestions
                .OrderBy(q => q.Position)
                .Select(q => new QuizQuestionDisplayItem
                {
                    Id = q.Id,
                    Position = q.Position,
                    Title = string.IsNullOrWhiteSpace(q.Title) ? "(sans titre)" : q.Title,
                    QuestionText = q.QuestionText ?? string.Empty,
                    PhotoUrl = q.PhotoUrl,
                    PositionLabel = $"Question {q.Position + 1}"
                })
                .ToList();

            Questions = displayItems;
            QuestionCount = displayItems.Count;
            HasQuestions = displayItems.Count > 0;
            NoQuestions = displayItems.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesDetail] LoadQuizQuestions: {ex.Message}");
            Questions = new();
            QuestionCount = 0;
            HasQuestions = false;
            NoQuestions = true;
        }
    }

    private async Task LoadProjectsAsync()
    {
        // ⚡ NOUVEAU : le mode lobby ne s'active que si la série est en préparation
        // ET que les projets sont configurés en mode "cachés".
        // - preparing + cachés    → mode lobby (compteurs + participants + mes projets)
        // - preparing + visibles  → liste complète immédiate
        // - active / finished     → liste révélée (comportement standard)
        var userId = _seriesService.CurrentUserId ?? string.Empty;
        IsLobbyMode = Status == "preparing" && ProjectsHidden;
        if (IsLobbyMode)
        {
            await LoadLobbyAsync(userId);
        }
        else
        {
            await LoadRevealedProjectsAsync();
        }
    }

    /// <summary>
    /// ⚡ REFONDÉE : lit depuis le JSON déjà chargé par LoadAsync (zéro réseau).
    /// </summary>
    private Task LoadLobbyAsync(string userId)
    {
        if (_cachedAllProjectsArray == null
            || _cachedAllProjectsArray.Value.ValueKind != JsonValueKind.Array)
        {
            ProjectCount = 0;
            TotalProjectCount = 0;
            PhotoVoteCount = 0;
            DuelCount = 0;
            PollCount = 0;
            HasPhotoVote = false;
            HasDuel = false;
            HasPoll = false;
            MyProjects = new();
            HasMyProjects = false;
            OtherProjects = new();
            HasOtherProjects = false;
            Projects = new();
            HasProjects = false;
            NoProjects = !HasParticipants;
            return Task.CompletedTask;
        }

        // 1 seule passe : compteurs + tri par auteur
        var typeCounts = new Dictionary<string, int>();
        var myItems = new List<SeriesProjectItem>();
        var otherItems = new List<SeriesProjectItem>();
        var total = 0;

        foreach (var el in _cachedAllProjectsArray.Value.EnumerateArray())
        {
            total++;
            var type = GetJsonString(el, "type") ?? "";
            typeCounts.TryGetValue(type, out var count);
            typeCounts[type] = count + 1;

            var item = BuildItemFromJson(el);

            if (item.AuthorUserId == userId)
                myItems.Add(item);
            else if (IsCreator)
                otherItems.Add(item);
        }

        ProjectCount = total;
        TotalProjectCount = total;
        PhotoVoteCount = typeCounts.GetValueOrDefault("photo_vote", 0);
        DuelCount = typeCounts.GetValueOrDefault("duel", 0);
        PollCount = typeCounts.GetValueOrDefault("poll", 0);
        HasPhotoVote = PhotoVoteCount > 0;
        HasDuel = DuelCount > 0;
        HasPoll = PollCount > 0;

        MyProjects = myItems;
        HasMyProjects = myItems.Count > 0;

        // ⚡ Plus besoin de EnrichAuthorUsernamesAsync : usernames déjà dans le JSON
        OtherProjects = otherItems;
        HasOtherProjects = otherItems.Count > 0;

        // En mode lobby, on cache la liste détaillée
        Projects = new();
        HasProjects = false;
        NoProjects = !HasParticipants;

        return Task.CompletedTask;
    }

    /// <summary>
    /// ⚡ REFONDÉE : lit depuis le JSON déjà chargé (filtre is_revealed=true).
    /// </summary>
    private Task LoadRevealedProjectsAsync()
    {
        var items = new List<SeriesProjectItem>();

        if (_cachedAllProjectsArray != null
            && _cachedAllProjectsArray.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in _cachedAllProjectsArray.Value.EnumerateArray())
            {
                // En mode "revealed", on ne montre que les projets is_revealed=true
                if (!GetJsonBool(el, "is_revealed")) continue;
                items.Add(BuildItemFromJson(el));
            }
        }

        ProjectCount = items.Count;
        Projects = items;
        HasProjects = items.Count > 0;
        NoProjects = items.Count == 0;

        // Reset lobby
        TotalProjectCount = items.Count;
        HasPhotoVote = false;
        HasDuel = false;
        HasPoll = false;
        // ⚠️ Ne PAS reset Participants ici (déjà rempli par LoadAsync)
        MyProjects = new();
        HasMyProjects = false;

        return Task.CompletedTask;
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : Helpers de parsing JSON (pour exploiter la RPC)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Construit un SeriesProjectItem directement depuis un JsonElement
    /// venant de la RPC get_series_detail_full.
    /// </summary>
    private SeriesProjectItem BuildItemFromJson(JsonElement el)
    {
        var type = GetJsonString(el, "type") ?? string.Empty;
        return new SeriesProjectItem
        {
            SeriesProjectId = GetJsonString(el, "series_project_id") ?? string.Empty,
            ProjectId = GetJsonString(el, "project_id") ?? string.Empty,
            Title = GetJsonString(el, "title") ?? string.Empty,
            Description = GetJsonString(el, "description") ?? string.Empty,
            Type = type,
            Position = GetJsonInt(el, "position"),
            IsRevealed = GetJsonBool(el, "is_revealed"),
            AuthorUserId = GetJsonString(el, "added_by") ?? string.Empty,
            AuthorUsername = GetJsonString(el, "author_username") ?? string.Empty,
            TypeLabel = type switch
            {
                "photo_vote" => "📷",
                "duel" => "⚖️",
                "poll" => "📊",
                _ => "📋"
            }
        };
    }

    private static string? GetJsonString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static int GetJsonInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return 0;
    }

    private static bool GetJsonBool(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop))
            return prop.ValueKind == JsonValueKind.True;
        return false;
    }

    // ═════════════════════════════════════════════════════════════════
    // BuildItem ORIGINAL : conservé car utilisé ailleurs (Edit/Delete)
    // ═════════════════════════════════════════════════════════════════
    private SeriesProjectItem BuildItem(SeriesProject sp, Project project)
        => new SeriesProjectItem
        {
            SeriesProjectId = sp.Id,
            ProjectId = sp.ProjectId,
            Title = project.Title,
            Description = project.Description ?? string.Empty,
            Type = project.Type,
            Position = sp.Position,
            IsRevealed = sp.IsRevealed,
            AuthorUserId = sp.AddedBy,
            AuthorUsername = string.Empty,
            TypeLabel = project.Type switch
            {
                "photo_vote" => "📷",
                "duel" => "⚖️",
                "poll" => "📊",
                _ => "📋"
            }
        };

    /// <summary>
    /// Conservée pour rétrocompatibilité au cas où elle serait appelée ailleurs.
    /// </summary>
    private async Task EnrichAuthorUsernamesAsync(List<SeriesProjectItem> items)
    {
        if (items.Count == 0) return;
        var distinctUserIds = items
            .Select(i => i.AuthorUserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0) return;

        try
        {
            var profiles = await _seriesService.GetProfilesByIdsAsync(distinctUserIds);
            var byId = profiles.ToDictionary(p => p.Id, p => p.Username);
            foreach (var item in items)
            {
                if (byId.TryGetValue(item.AuthorUserId, out var username))
                    item.AuthorUsername = username;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnrichAuthorUsernames: {ex.Message}");
        }
    }

    // ─── Créer un projet pour cette série ────────────────────────
    [RelayCommand]
    private async Task AddProjectAsync()
    {
        await Shell.Current.GoToAsync("CreateSeriesProjectPage",
            new Dictionary<string, object>
            {
                { "SeriesId",    SeriesId },
                { "SeriesTitle", SeriesTitle }
            });
    }

    // ⚡ LOT 1 : modifier les questions d'un quiz (rouvre CreateQuizPage)
    [RelayCommand]
    private async Task EditQuizQuestionsAsync()
    {
        if (!IsQuiz)
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_NotApplicable_Title"),
                L.T("SeriesDetail_NotAQuiz_Msg"),
                L.T("Common_OK"));

            return;
        }
        if (!IsCreator || Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
                 L.T("SeriesDetail_Locked_Title"),
                L.T("SeriesDetail_QuestionsLocked_Msg"),
                L.T("Common_OK"));

            return;
        }

        // On rouvre la page de création de quiz en mode édition.
        // CreateQuizPage devra détecter qu'un SeriesId est fourni et charger
        // les questions existantes au lieu de partir d'une page vide.
        
         await Shell.Current.GoToAsync("EditQuizQuestionsPage",
        new Dictionary<string, object>
        {
            { "SeriesId",    SeriesId },
            { "SeriesTitle", SeriesTitle ?? string.Empty }
        });

    }

    // ─── Inviter des amis ────────────────────────────────────────
    [RelayCommand]
    private async Task InviteFriendsAsync()
    {
        await Shell.Current.GoToAsync("InviteToSeriesPage",
            new Dictionary<string, object>
            {
                { "SeriesId",    SeriesId },
                { "SeriesTitle", SeriesTitle }
            });
    }

    // ─── Démarrer ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task StartSeriesAsync()
    {
        // ⚡ LOT 1 : pour un quiz on vérifie QuestionCount, pas ProjectCount
        if (IsQuiz)
        {
            if (QuestionCount == 0)
            {
                await Shell.Current.DisplayAlert(
                   L.T("SeriesDetail_Cannot_Title"),
                    L.T("SeriesDetail_NeedQuestion_Msg"),
                    L.T("Common_OK"));

                return;
            }
            bool confirm = await Shell.Current.DisplayAlert(
                 L.T("SeriesDetail_StartQuiz_Title"),
                 L.T("SeriesDetail_StartQuiz_Msg"),
                 L.T("Common_Start"), L.T("Common_Cancel"));
            if (!confirm) return;
        }
        else
        {
            if (ProjectCount == 0)
            {
                await Shell.Current.DisplayAlert(L.T("SeriesDetail_Cannot_Title"), L.T("SeriesDetail_NeedProject_Msg"), L.T("Common_OK"));
                return;
            }
            bool confirm = await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_StartSeries_Title"),
                L.T("SeriesDetail_StartSeries_Msg"),
                L.T("Common_Start"), L.T("Common_Cancel"));

            if (!confirm) return;
        }

        var ok = await _seriesService.StartSeriesAsync(SeriesId);
        if (ok)
        {
            await LoadAsync();
            await GoToVoteAsync();
        }
    }

    // ─── Aller voter ──────────────────────────────────────────────
    [RelayCommand]
    private async Task GoToVoteAsync()
    {
        // 🎉 PARTIE RAPIDE : si quiz_style='party' -> ecran party,
        // sinon comportement d'origine (quiz classique ou vote normal).
        var route = IsParty ? "PartyPlayPage"
                  : IsQuiz  ? "QuizPlayPage"
                            : "SeriesVotePage";
        await Shell.Current.GoToAsync(route,
            new Dictionary<string, object>
            {
                { "SeriesId",    SeriesId },
                { "SeriesTitle", SeriesTitle }
            });
    }

    // ─── Terminer ─────────────────────────────────────────────────
    [RelayCommand]
    private async Task StopSeriesAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            L.T("SeriesDetail_FinishSeries_Title"),
            L.T("SeriesDetail_FinishSeries_Msg"),
            L.T("Common_Finish"), L.T("Common_Cancel"));

        if (!confirm) return;

        var ok = await _seriesService.StopSeriesAsync(SeriesId);
        if (ok) await LoadAsync();
    }

    // ─── Remettre en préparation ──────────────────────────────────
    [RelayCommand]
    private async Task ResetSeriesAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
             L.T("SeriesDetail_Reprepare_Title"),
            L.T("SeriesDetail_Reprepare_Msg"),
            L.T("Common_Confirm"), L.T("Common_Cancel"));

        if (!confirm) return;

        var ok = await _seriesService.ResetSeriesAsync(SeriesId);
        if (ok) await LoadAsync();
    }

    // ─── ⚡ NOUVEAU : Modifier le titre + description d'un projet ─────
    [RelayCommand]
    private async Task EditProjectTitleAsync(SeriesProjectItem item)
    {
        if (item == null) return;
        if (Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_Locked_Title"),
                L.T("SeriesDetail_ProjectsLockedEdit_Msg"),
                L.T("Common_OK"));


            return;
        }

        var newTitle = await Shell.Current.DisplayPromptAsync(
                   L.T("SeriesDetail_EditTitle_Title"),
                   L.T("SeriesDetail_EditTitle_Msg"),
                   initialValue: item.Title,
                   accept: L.T("Common_Next"),
                   cancel: L.T("Common_Cancel"),
                   maxLength: 100);
        if (string.IsNullOrWhiteSpace(newTitle)) return;

        var newDescription = await Shell.Current.DisplayPromptAsync(
             L.T("SeriesDetail_EditDesc_Title"),
             L.T("SeriesDetail_EditDesc_Msg"),
             initialValue: item.Description,
             accept: L.T("Common_Save"),
             cancel: L.T("Common_Ignore"),
             maxLength: 500);


        var descriptionToSave = newDescription ?? item.Description;

        var (ok, error) = await _projectService.UpdateProjectMetadataAsync(
            item.ProjectId, newTitle.Trim(), descriptionToSave);

        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
            return;
        }

        if (item.Type != "poll")
        {
            bool wantPhotos = await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_Photos_Title"),
                L.T("SeriesDetail_EditPhotos_Msg"),
                L.T("Common_Yes"), L.T("SeriesDetail_Later"));


            if (wantPhotos)
            {
                var updatedItem = new SeriesProjectItem
                {
                    SeriesProjectId = item.SeriesProjectId,
                    ProjectId = item.ProjectId,
                    Type = item.Type,
                    Title = newTitle.Trim(),
                    Description = descriptionToSave ?? string.Empty
                };
                await EditProjectPhotosAsync(updatedItem);
                return;
            }
        }

        await LoadAsync();
    }

    // ─── ⚡ NOUVEAU : Modifier les photos d'un projet ─────────────────
    [RelayCommand]
    private async Task EditProjectPhotosAsync(SeriesProjectItem item)
    {
        if (item == null) return;
        if (Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_Locked_Title"),
                L.T("SeriesDetail_ProjectsLockedEdit_Msg"),
                L.T("Common_OK"));
            return;
        }

        if (item.Type == "poll")
        {
            await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_NotApplicable_Title"),
                L.T("SeriesDetail_PollNoPhoto_Msg"),
                L.T("Common_OK"));

            return;
        }

        if (item.Type == "photo_vote")
        {
            var pick = await MediaPicker.PickPhotoAsync();
            if (pick == null) return;
            using var stream = await pick.OpenReadAsync();
            var (ok, error) = await _projectService.UpdateProjectPhotoAsync(
                item.ProjectId, "single", stream, pick.FileName);
            if (!ok)
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
                return;
            }
            await Shell.Current.DisplayAlert(L.T("Common_Success"), L.T("SeriesDetail_PhotoUpdated_Msg"), L.T("Common_OK"));
        }
        else if (item.Type == "duel")
        {
            var side = await Shell.Current.DisplayActionSheet(
                L.T("SeriesDetail_WhichPhoto_Title"),
                L.T("Common_Cancel"), null,
                L.T("SeriesDetail_PhotoLeft"), L.T("SeriesDetail_PhotoRight"));


            string sideKey = side switch
            {
                "📸 Photo de gauche" => "left",
                "📸 Photo de droite" => "right",
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(sideKey)) return;

            var pick = await MediaPicker.PickPhotoAsync();
            if (pick == null) return;
            using var stream = await pick.OpenReadAsync();
            var (ok, error) = await _projectService.UpdateProjectPhotoAsync(
                item.ProjectId, sideKey, stream, pick.FileName);
            if (!ok)
            {
                await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
                return;
            }
            await Shell.Current.DisplayAlert(L.T("Common_Success"), L.T("SeriesDetail_PhotoUpdated_Msg"), L.T("Common_OK"));
        }

        await LoadAsync();
    }

    // ─── ⚡ NOUVEAU : Supprimer mon propre projet ─────────────────────
    [RelayCommand]
    private async Task DeleteMyProjectAsync(SeriesProjectItem item)
    {
        if (item == null) return;
        if (Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_Locked_Title"),
                L.T("SeriesDetail_ProjectsLockedDelete_Msg"),
                L.T("Common_OK"));


            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("SeriesDetail_DeleteProject_Title"),
            L.F("SeriesDetail_DeleteProject_Msg", item.Title),
            L.T("Common_Delete"), L.T("Common_Cancel"));
        if (!confirm) return;

        var (ok, error) = await _seriesService.DeleteSeriesProjectAsync(
            item.SeriesProjectId, item.ProjectId);

        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));           
                return;
        }

        await LoadAsync();
    }

    // ─── ⚡ NOUVEAU : Supprimer le projet d'un autre (modération) ─────
    [RelayCommand]
    private async Task DeleteOthersProjectAsync(SeriesProjectItem item)
    {
        if (item == null) return;
        if (!IsCreator)
        {
            await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_NotAllowed_Title"),
                L.T("SeriesDetail_OnlyCreatorDelete_Msg"),
                L.T("Common_OK"));


            return;
        }
        if (Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_Locked_Title"),
                L.T("SeriesDetail_ProjectsLockedDelete_Msg"),
                L.T("Common_OK"));
            return;
        }

        var authorName = string.IsNullOrEmpty(item.AuthorUsername)
            ? "ce participant"
            : item.AuthorUsername;

        bool confirm = await Shell.Current.DisplayAlert(
          L.F("SeriesDetail_DeleteOtherProject_Title", authorName),
            L.F("SeriesDetail_DeleteOtherProject_Msg", item.Title),
            L.T("Common_Delete"), L.T("Common_Cancel"));
        if (!confirm) return;

        var (ok, error) = await _seriesService.DeleteSeriesProjectAsync(
            item.SeriesProjectId, item.ProjectId);

        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK")); return;
        }

        await LoadAsync();
    }

    // ─── Supprimer ────────────────────────────────────────────────
    [RelayCommand]
    // ─── Supprimer ────────────────────────────────────────────────
    // ⚡ Confirmation différenciée selon le statut :
    // - preparing : alerte simple (rien à perdre)
    // - active    : alerte forte (session en cours)
    // - finished  : alerte forte (résultats existent)
    //
    // Appelle la RPC delete_series_safe via SeriesService :
    // - vérifie côté SQL que l'utilisateur est bien le créateur
    // - supprime selfies du Storage + cascade FK BDD

    private async Task DeleteSeriesAsync()
    {
        // 1) Préparer le message de confirmation selon le statut
        string title;
        string message;
        switch (Status)
        {
            case "active":
                title = L.T("SeriesDetail_DeleteSeriesActive_Title");
                message = L.T("SeriesDetail_DeleteSeriesActive_Msg");
                break;

            case "finished":
                title = L.T("SeriesDetail_DeleteSeriesFinished_Title");
                message = L.T("SeriesDetail_DeleteSeriesFinished_Msg");
                break;

            default: // "preparing" ou autre
                title = L.T("SeriesDetail_DeleteSeries_Title");
                message = L.T("SeriesDetail_DeleteSeries_Msg");
                break;

        }

        bool confirm = await Shell.Current.DisplayAlert(
title, message, L.T("Common_Delete"), L.T("Common_Cancel"));
        if (!confirm) return;

        // 2) Appel de la RPC (avec suppression Storage en amont)
        IsLoading = true;
        try
        {
            var result = await _seriesService.DeleteSeriesAsync(SeriesId);

            if (result.Success)
            {
                await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_SeriesDeleted_Title"),
                string.IsNullOrEmpty(result.Message)
                    ? L.T("SeriesDetail_SeriesDeleted_Msg")
                    : result.Message,
                L.T("Common_OK"));
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert(
                     L.T("SeriesDetail_DeleteFailed_Title"),
                    string.IsNullOrEmpty(result.Message)
                       ? L.T("Common_ErrorOccurred")
                        : result.Message,

                    L.T("Common_OK"));

            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteSeriesAsync VM: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"),
                L.T("SeriesDetail_DeleteSeriesNow_Msg"),
                L.T("Common_OK"));

        }
        finally
        {
            IsLoading = false;
        }
    }
    // ═══════════════════════════════════════════════════════════════
    // ⚡ Lot Mode TV — Commandes
    // ═══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ActivateTvModeAsync()
    {
        if (!IsCreator || Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_NotAllowed_Title"),
                L.T("SeriesDetail_OnlyCreatorTv_Msg"),
                L.T("Common_OK"));

            return;
        }

        bool animatorParticipates = await Shell.Current.DisplayAlert(
            L.T("SeriesDetail_TvMode_Title"),
            L.T("SeriesDetail_TvParticipate_Msg"),
            L.T("SeriesDetail_TvIParticipate"),
            L.T("SeriesDetail_TvHostOnly"));


        var (ok, code, error) = await _seriesService.ActivateTvModeAsync(
            SeriesId, animatorParticipates);

        if (!ok)
        {
            await Shell.Current.DisplayAlert(
               L.T("Common_Error"),
                L.F("SeriesDetail_TvActivateError_Msg", error),
                L.T("Common_OK"));


            return;
        }

        if (animatorParticipates)
        {
            await _seriesService.AddAnimatorAsParticipantAsync(SeriesId);
        }

        await Shell.Current.DisplayAlert(
           L.T("SeriesDetail_TvActivated_Title"),
            L.F("SeriesDetail_TvActivated_Msg", code),
            L.T("Common_OK"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task StartTvSeriesAsync()
    {
        if (!IsCreator || !TvActive || Status != "preparing")
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_NotAllowed_Title"),
                L.T("SeriesDetail_TvMustBeActive_Msg"),
                L.T("Common_OK"));

            return;
        }

        // ⚡ LOT 1 : pour un quiz, vérifier qu'il y a des questions avant
        // d'autoriser le démarrage TV (sinon les joueurs verraient un quiz vide)
        if (IsQuiz && QuestionCount == 0)
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_Cannot_Title"),
                L.T("SeriesDetail_NeedQuestion_Msg"),
                L.T("Common_OK"));
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("SeriesDetail_StartSeriesTv_Title"),
            L.T("SeriesDetail_StartSeriesTv_Msg"),
            L.T("Common_Start"), L.T("Common_Cancel"));
        if (!confirm) return;

        var (ok, error) = await _seriesService.StartTvSeriesAsync(SeriesId);
        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleTvPauseAsync()
    {
        if (!IsCreator || !TvActive)
        {
            await Shell.Current.DisplayAlert(
                L.T("SeriesDetail_NotAllowed_Title"),
                L.T("SeriesDetail_TvMustBeActiveShort_Msg"),
                L.T("Common_OK"));

            return;
        }

        var (ok, nowPaused, error) = await _seriesService.ToggleTvPauseAsync(SeriesId);
        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
            return;
        }

        TvPaused = nowPaused;
        TvPauseLabel = nowPaused ? L.T("SeriesDetail_Tv_Resume") : L.T("SeriesDetail_Tv_Pause");
    }

    [RelayCommand]
    private async Task DeactivateTvModeAsync()
    {
        if (!IsCreator || !TvActive) return;

        bool confirm = await Shell.Current.DisplayAlert(
            L.T("SeriesDetail_DeactivateTv_Title"),
            L.T("SeriesDetail_DeactivateTv_Msg"),
            L.T("SeriesDetail_Deactivate"), L.T("Common_Cancel"));
        if (!confirm) return;

        var (ok, error) = await _seriesService.DeactivateTvModeAsync(SeriesId);
        if (!ok)
        {
            await Shell.Current.DisplayAlert(L.T("Common_Error"), error, L.T("Common_OK"));
            return;
        }

        await LoadAsync();
    }

    // ─── ⚡ NOUVEAU : Copier le code d'accès ──────────────────────
    [RelayCommand]
    private async Task CopyAccessCodeAsync()
    {
        if (string.IsNullOrEmpty(AccessCode)) return;
        try
        {
            await Clipboard.Default.SetTextAsync(AccessCode);
            await Shell.Current.DisplayAlert(
               L.T("SeriesDetail_Copied_Title"),
                L.F("SeriesDetail_CodeCopied_Msg", AccessCode),
                L.T("Common_OK"));


        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CopyAccessCode: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ⚡ Palier B : Handlers Realtime
    // ═══════════════════════════════════════════════════════════════

    private async void OnRealtimeSeriesChanged(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        try
        {
            var series = await _seriesService.GetSeriesAsync(SeriesId);
            if (series == null) return;

            var oldStatus = Status;
            var newStatus = series.Status;

            Status = newStatus;
            TvActive = series.TvActive;
            TvPaused = series.TvPaused;
            StatusLabel = newStatus switch
            {
                "preparing" => L.T("SeriesDetail_Status_Preparing"),
                "active" => L.T("SeriesDetail_Status_Active"),
                "finished" => L.T("SeriesDetail_Status_Finished"),
                _ => newStatus
            };

            StatusColor = newStatus switch
            {
                "preparing" => Color.FromArgb("#C9943E"),
                "active" => Color.FromArgb("#4A7A52"),
                "finished" => Color.FromArgb("#8A6F4A"),
                _ => Color.FromArgb("#8A6F4A")
            };
            CanVote = newStatus == "active";
            TvPauseLabel = TvPaused ? L.T("SeriesDetail_Tv_Resume") : L.T("SeriesDetail_Tv_Pause");
            if (oldStatus != "active" && newStatus == "active" && !IsCreator && IsCurrentUserParticipant)
            {
                // 🎉 PARTIE RAPIDE : bascule auto vers PartyPlayPage si 'party'.
                var route = IsParty ? "PartyPlayPage"
                          : IsQuiz  ? "QuizPlayPage"
                                    : "SeriesVotePage";
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesDetail] Bascule auto vers " + route + " (série démarrée, IsQuiz=" + IsQuiz + ", IsParticipant=true)");
                await Shell.Current.GoToAsync(route,
                    new Dictionary<string, object>
                    {
                        { "SeriesId", SeriesId },
                        { "SeriesTitle", SeriesTitle }
                    });
            }
            else if (oldStatus != "active" && newStatus == "active" && !IsCreator && !IsCurrentUserParticipant)
            {
                // ⚡ FIX : un user qui consulte juste la page de la série sans avoir
                // rejoint comme participant ne doit PAS être basculé en mode vote/quiz.
                // Cela arrivait parce que le Realtime envoie l'event à tous les
                // abonnés au channel, y compris les "spectateurs". Maintenant ils
                // voient juste le statut passer à "▶ En cours" mais restent sur la page.
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesDetail] Série démarrée mais user non-participant → pas de bascule");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesDetail] OnRealtimeSeriesChanged: {ex.Message}");
        }
    }

    private async void OnRealtimeParticipantJoined(PostgresChangesPayload<SocketResponsePayload> payload)
    {
        try
        {
            Participants = await _seriesService.GetParticipantsWithStatsAsync(SeriesId);
            ParticipantCount = Participants.Count;
            HasParticipants = ParticipantCount > 0;

            // ⚡ FIX : refresh aussi le flag "je suis participant" pour que la
            // bascule auto fonctionne si je viens juste de rejoindre.
            var currentUserId = _seriesService.CurrentUserId;
            if (!string.IsNullOrEmpty(currentUserId))
            {
                IsCurrentUserParticipant = Participants.Any(p => p.UserId == currentUserId);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SeriesDetail] Compteur participants mis à jour : {ParticipantCount} (IsParticipant={IsCurrentUserParticipant})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesDetail] OnRealtimeParticipantJoined: {ex.Message}");
        }
    }

    /// <summary>
    /// ⚡ NOUVEAU : appelée par le code-behind quand l'utilisateur confirme
    /// "Quitter la série" depuis le Retour Android sur SeriesDetailPage
    /// </summary>
    public async Task LeaveSeriesAsync()
    {
        try
        {
            await _seriesService.LeaveSeriesAsync(SeriesId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesDetail] LeaveSeries: {ex.Message}");
        }
    }

    /// <summary>
    /// 🎉 PARTIE RAPIDE — "Kill game".
    /// Appelée par le code-behind quand l'ANIMATEUR (créateur) quitte
    /// une partie rapide en cours : on termine la série côté serveur
    /// (status -> 'finished') pour ne pas laisser de parties zombies
    /// actives. Réutilise StopSeriesAsync du SeriesService (même appel
    /// que le bouton "Terminer"), donc aucun nouveau code serveur.
    /// </summary>
    public async Task KillGameAsync()
    {
        try
        {
            await _seriesService.StopSeriesAsync(SeriesId);
            System.Diagnostics.Debug.WriteLine(
                "[SeriesDetail] 🎉 KillGame : partie terminée (animateur a quitté)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesDetail] KillGame: {ex.Message}");
        }
    }

    /// <summary>
    /// À appeler quand on quitte la page (OnDisappearing du Code-Behind).
    /// </summary>
    public async Task CleanupAsync()
    {
        try
        {
            await _realtime.UnsubscribeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesDetail] Cleanup: {ex.Message}");
        }
    }
}

public class SeriesProjectItem
{
    public string SeriesProjectId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsRevealed { get; set; }

    // ⚡ NOUVEAU : pour le mode modération
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
}

// ⚡ LOT 1 : item d'affichage d'une question dans SeriesDetailPage (mode quiz).
// Lecture seule : un tap sur la question ne déclenche rien (modification se
// fait via le bouton "✏️ Modifier les questions" qui rouvre CreateQuizPage).
public class QuizQuestionDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public int Position { get; set; }
    public string PositionLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoUrl);
    public bool HasQuestionText => !string.IsNullOrWhiteSpace(QuestionText);
}
