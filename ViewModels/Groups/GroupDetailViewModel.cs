using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using System.Text.Json;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
public partial class GroupDetailViewModel : ObservableObject
{
    private readonly GroupService _groupService;
    private readonly SeriesService _seriesService;
    private readonly AuthService _authService;

    public GroupDetailViewModel(
        GroupService groupService,
        SeriesService seriesService,
        AuthService authService)
    {
        _groupService = groupService;
        _seriesService = seriesService;
        _authService = authService;
    }

    [ObservableProperty] private string _groupId = string.Empty;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isSending = false;
    [ObservableProperty] private bool _isAdmin = false;

    // ⚡ NOUVEAU : true si l'utilisateur courant est le créateur du groupe
    // (différent de IsAdmin : un admin peut être quelqu'un d'autre que le owner).
    // Utilisé pour afficher le bouton "Supprimer le groupe".
    [ObservableProperty] private bool _isOwner = false;

    // ─── Séries ───────────────────────────────────────────────────
    [ObservableProperty] private List<SeriesItem> _series = new();
    [ObservableProperty] private bool _hasSeries = false;
    [ObservableProperty] private bool _noSeries = false;

    // ─── Chat ─────────────────────────────────────────────────────
    [ObservableProperty] private List<GroupMessage> _messages = new();
    [ObservableProperty] private string _newMessage = string.Empty;
    [ObservableProperty] private bool _hasMessages = false;

    // ─── Membres ──────────────────────────────────────────────────
    [ObservableProperty] private List<MemberItem> _members = new();
    [ObservableProperty] private bool _hasMembers = false;

    // ⚡ FIX PERF MAJEUR : avant, ce hook déclenchait aussi LoadAllAsync,
    // ce qui provoquait un DOUBLE chargement (une fois ici quand le binding
    // assignait GroupId, une fois dans OnAppearing du code-behind). C'est
    // OnAppearing qui pilote maintenant le chargement (un seul appel,
    // garanti après que le binding soit complet).
    partial void OnGroupIdChanged(string value)
    {
        System.Diagnostics.Debug.WriteLine($"OnGroupIdChanged: {value}");
        // Ne PAS déclencher LoadAllAsync ici — OnAppearing s'en charge.
    }

    // ⚡ REFONDÉ : utilise la RPC get_group_details (1 seule requête au lieu de N+3)
    // ⚡ FIX PERF : parsing progressif — on affiche les séries dès qu'elles sont
    // disponibles, sans attendre que membres et messages soient parsés. Cela
    // donne au user une perception d'instantanéité même si la RPC retourne
    // beaucoup de données.
    [RelayCommand]
    public async Task LoadAllAsync()
    {
        // ⚡ DIAG : timing détaillé pour identifier ce qui rame
        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] T+0ms: LoadAllAsync ENTRÉE GroupId={GroupId}");

        if (string.IsNullOrEmpty(GroupId)) return;
        IsLoading = true;
        try
        {
            // ⚡ NOUVEAU : récupérer le owner du groupe en parallèle de la RPC
            //    pour pouvoir afficher le bouton "Supprimer" uniquement au créateur.
            //    Petit appel léger (Single() sur Group), donc négligeable.
            var groupTask = _groupService.GetGroupAsync(GroupId);
            var jsonTask = _groupService.GetGroupDetailsJsonAsync(GroupId);

            await Task.WhenAll(groupTask, jsonTask);
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] T+{sw.ElapsedMilliseconds}ms: RPC + GetGroup terminés");

            var group = groupTask.Result;
            var json = jsonTask.Result;
            var jsonSize = json?.Length ?? 0;
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] JSON taille = {jsonSize} chars");

            // Calcul de IsOwner
            var currentUserId = _groupService.CurrentUserId;
            IsOwner = group != null
                      && !string.IsNullOrEmpty(currentUserId)
                      && group.OwnerId == currentUserId;

            ParseSeriesOnly(json);
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] T+{sw.ElapsedMilliseconds}ms: ParseSeriesOnly OK ({Series.Count} séries)");
            IsLoading = false;          // on cache le loader dès que les séries sont là

            // Yield pour laisser le UI thread rendre la liste des séries
            // avant d'attaquer le reste du parsing.
            await Task.Yield();
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] T+{sw.ElapsedMilliseconds}ms: après Task.Yield (UI a peint)");

            ParseRest(json);
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] T+{sw.ElapsedMilliseconds}ms: ParseRest OK ({Members.Count} membres, {Messages.Count} messages)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] EXCEPTION T+{sw.ElapsedMilliseconds}ms: {ex.Message}");
            IsLoading = false;
        }
        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"[GroupDetail-DIAG] TOTAL = {sw.ElapsedMilliseconds}ms");
    }

    // ─── Parser JSON renvoyé par la RPC ────────────────────────────
    // ⚡ FIX PERF : ancien parser unique remplacé par 2 méthodes
    // (ParseSeriesOnly + ParseRest) appelées séparément avec yield UI
    // entre les deux pour donner une perception d'instantanéité.
    // Conservé pour rétrocompatibilité au cas où il serait appelé ailleurs.
    private void ParseGroupDetailsJson(string? json)
    {
        ParseSeriesOnly(json);
        ParseRest(json);
    }

    /// <summary>
    /// ⚡ FIX PERF : phase 1 du parsing — uniquement is_admin + séries.
    /// C'est ce que le user veut voir EN PREMIER quand il ouvre le groupe.
    /// </summary>
    private void ParseSeriesOnly(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ─── is_admin ─────────────────────────────────────────
            IsAdmin = root.TryGetProperty("is_admin", out var adminEl)
                      && adminEl.ValueKind == JsonValueKind.True;

            // ─── series ───────────────────────────────────────────
            var myUserId = _authService.CurrentUser?.Id ?? string.Empty;

            var seriesItems = new List<SeriesItem>();
            if (root.TryGetProperty("series", out var seriesEl)
                && seriesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in seriesEl.EnumerateArray())
                {
                    var status = GetString(s, "status") ?? "preparing";
                    var isQuiz = GetBool(s, "is_quiz");
                    var creatorId = GetString(s, "creator_id") ?? string.Empty;

                    seriesItems.Add(new SeriesItem
                    {
                        Id = GetString(s, "id") ?? string.Empty,
                        Title = GetString(s, "title") ?? string.Empty,
                        Status = status,
                        MaxProjects = GetInt(s, "max_projects"),
                        CreatorId = creatorId,
                        IsCreatedByMe = !string.IsNullOrEmpty(myUserId) && creatorId == myUserId,
                        AccessCode = GetString(s, "access_code") ?? string.Empty,
                        IsQuiz = isQuiz,
                        StatusLabel = status switch
                        {
                            "preparing" => "⏳ Préparation",
                            "active" => "▶ En cours",
                            "finished" => "✓ Terminée",
                            _ => status
                        },
                        StatusColor = status switch
                        {
                            "preparing" => Color.FromArgb("#C9943E"),
                            "active" => Color.FromArgb("#4A7A52"),
                            "finished" => Color.FromArgb("#8A6F4A"),
                            _ => Color.FromArgb("#8A6F4A")
                        }
                    });
                }
            }
            Series = seriesItems;
            HasSeries = seriesItems.Count > 0;
            NoSeries = seriesItems.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseSeriesOnly: {ex.Message}");
        }
    }

    /// <summary>
    /// ⚡ FIX PERF : phase 2 du parsing — membres et messages.
    /// Appelée APRÈS un Task.Yield pour laisser le UI thread peindre
    /// la liste des séries avant de bosser sur le reste.
    /// </summary>
    private void ParseRest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ─── members ──────────────────────────────────────────
            var members = new List<MemberItem>();
            if (root.TryGetProperty("members", out var membersEl)
                && membersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in membersEl.EnumerateArray())
                {
                    var role = GetString(m, "role") ?? "member";
                    members.Add(new MemberItem
                    {
                        UserId = GetString(m, "user_id") ?? string.Empty,
                        Username = GetString(m, "username") ?? "?",
                        AvatarUrl = GetString(m, "avatar_url"),
                        Role = role,
                        IsAdmin = role == "admin",
                        RoleLabel = role == "admin" ? "Admin" : "Membre"
                    });
                }
            }
            Members = members;
            HasMembers = members.Count > 0;

            // ─── messages ─────────────────────────────────────────
            var msgs = new List<GroupMessage>();
            if (root.TryGetProperty("messages", out var msgsEl)
                && msgsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in msgsEl.EnumerateArray())
                {
                    var msg = new GroupMessage
                    {
                        Id = GetString(m, "id") ?? string.Empty,
                        GroupId = GroupId,
                        SenderId = GetString(m, "sender_id") ?? string.Empty,
                        Content = GetString(m, "content") ?? string.Empty,
                        CreatedAt = GetDateTime(m, "created_at"),
                        IsMyMessage = m.TryGetProperty("is_my_message", out var imm)
                                      && imm.ValueKind == JsonValueKind.True
                    };
                    msgs.Add(msg);
                }
            }
            Messages = msgs;
            HasMessages = msgs.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseRest: {ex.Message}");
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static int GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return 0;
    }

    // ⚡ B6 : Helper pour lire un bool en tolérant les cas où le champ
    // n'existe pas (la RPC peut être ancienne et ne pas renvoyer is_quiz)
    private static bool GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)) return false;
        return prop.ValueKind == JsonValueKind.True;
    }

    private static DateTime GetDateTime(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            && DateTime.TryParse(prop.GetString(), out var dt))
            return dt;
        return DateTime.UtcNow;
    }

    // ─── Recharger uniquement les messages (après envoi) ───────────
    private async Task LoadMessagesAsync()
    {
        var msgs = await _groupService.GetMessagesAsync(GroupId);
        Messages = msgs;
        HasMessages = msgs.Count > 0;
    }

    // ─── Envoyer message ──────────────────────────────────────────
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage)) return;
        IsSending = true;
        var content = NewMessage;
        NewMessage = string.Empty;
        try
        {
            await _groupService.SendMessageAsync(GroupId, content);
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendMessage: {ex.Message}");
        }
        finally { IsSending = false; }
    }

    // ─── Créer une série ──────────────────────────────────────────
    // ⚡ MODIFIÉ : on passe maintenant par l'écran de choix Vote/Quiz
    [RelayCommand]
    private async Task CreateSeriesAsync()
    {
        System.Diagnostics.Debug.WriteLine($"CreateSeriesAsync: GroupId={GroupId}");
        await Shell.Current.GoToAsync("CreateSeriesTypePage",
            new Dictionary<string, object>
            {
                { "GroupId", GroupId },
                { "GroupName", GroupName }
            });
    }

    // ─── Ouvrir une série ─────────────────────────────────────────
    [RelayCommand]
    private async Task OpenSeriesAsync(SeriesItem series)
    {
        // ⚡ QW2 : en mode multi-sélection, un tap court toggle l'item au
        // lieu d'ouvrir la série. C'est aussi ce que fait Gmail.
        if (IsSeriesSelectionMode)
        {
            ToggleSeriesSelection(series);
            return;
        }

        await Shell.Current.GoToAsync("SeriesDetailPage",
            new Dictionary<string, object>
            {
                { "SeriesId", series.Id },
                { "SeriesTitle", series.Title },
                { "GroupId", GroupId }
            });
    }

    [RelayCommand]
    private async Task CopyAccessCodeAsync(SeriesItem series)
    {
        if (series == null || string.IsNullOrEmpty(series.AccessCode)) return;
        try
        {
            await Clipboard.Default.SetTextAsync(series.AccessCode);
            await Shell.Current.DisplayAlert(
                "✅ Copié",
                $"Le code « {series.AccessCode} » a été copié dans le presse-papiers.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CopyAccessCode: {ex.Message}");
        }
    }


    // ⚡ NOUVEAU : Suppression du groupe par son créateur
    // - Bouton visible uniquement si IsOwner == true
    // - Confirmation simple
    // - Affichage du résultat (succès, erreur, ou détail si série active)
    // - Retour à la liste des groupes en cas de succès
    [RelayCommand]
    private async Task DeleteGroupAsync()
    {
        if (!IsOwner)
        {
            await Shell.Current.DisplayAlert(
                "Non autorisé",
                "Seul le créateur du groupe peut le supprimer.",
                "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Supprimer le groupe ?",
            "Cette action est irréversible. Les séries rattachées seront conservées " +
            "(sans groupe). Les messages du chat seront définitivement supprimés.",
            "Supprimer", "Annuler");

        if (!confirm) return;

        IsLoading = true;
        try
        {
            var result = await _groupService.DeleteGroupAsync(GroupId);

            if (result.Success)
            {
                await Shell.Current.DisplayAlert(
                    "✅ Groupe supprimé",
                    result.Message,
                    "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    "❌ Suppression impossible",
                    string.IsNullOrEmpty(result.Message)
                        ? "Une erreur est survenue."
                        : result.Message,
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteGroupAsync: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "❌ Erreur",
                "Impossible de supprimer le groupe pour l'instant.",
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StopRealtime() { }
    // ═════════════════════════════════════════════════════════════════
    // ⚡ QW2 : MULTI-SÉLECTION GMAIL-STYLE (séries)
    //
    // Un long press sur une série → entre en mode sélection ET sélectionne
    // l'item (cf. EnterSeriesSelectionWith). En mode sélection, un tap
    // court toggle (cf. OpenSeriesAsync). Le bouton "Annuler" ou la touche
    // Retour Android sortent du mode (cf. ExitSeriesSelection). Le bouton
    // 🗑️ déclenche DeleteSelectedSeriesAsync : confirmation 1 fois puis
    // suppression batch en parallèle.
    // ═════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionHeaderText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSeries))]
    private bool _isSeriesSelectionMode = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionHeaderText))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSeries))]
    private int _selectedSeriesCount = 0;

    public bool HasSelectedSeries => SelectedSeriesCount > 0;

    public string SelectionHeaderText => $"🗑️ ({SelectedSeriesCount})";

    /// <summary>
    /// ⚡ QW2 (rev) : entre en mode sélection sans cocher d'item.
    /// L'utilisateur tape ensuite sur les items qu'il veut sélectionner.
    /// Appelé par le bouton "✓ Sélectionner" en haut de la liste.
    /// </summary>
    [RelayCommand]
    private void StartSeriesSelection()
    {
        if (!IsSeriesSelectionMode)
        {
            IsSeriesSelectionMode = true;
        }
    }

    /// <summary>
    /// Toggle la sélection d'un item (utilisé par le tap court en mode
    /// sélection, cf. OpenSeriesAsync).
    /// </summary>
    private void ToggleSeriesSelection(SeriesItem series)
    {
        if (series == null) return;
        series.IsSelected = !series.IsSelected;
        SelectedSeriesCount = Series.Count(s => s.IsSelected);

        // Si plus rien de sélectionné, on sort automatiquement du mode.
        // C'est ce que fait Gmail : décocher le dernier email sort de la
        // multi-sélection.
        if (SelectedSeriesCount == 0)
        {
            IsSeriesSelectionMode = false;
        }
    }

    /// <summary>
    /// Sort du mode sélection : déselectionne tout, ferme la barre d'actions.
    /// Appelé par le bouton "Annuler" et le Retour Android.
    /// </summary>
    [RelayCommand]
    private void ExitSeriesSelection()
    {
        foreach (var s in Series)
        {
            if (s.IsSelected) s.IsSelected = false;
        }
        SelectedSeriesCount = 0;
        IsSeriesSelectionMode = false;
    }

    /// <summary>
    /// Supprime toutes les séries sélectionnées : 1 confirmation, puis appels
    /// DeleteSeriesAsync en parallèle. Affiche un récap final.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedSeriesAsync()
    {
        var toDelete = Series.Where(s => s.IsSelected).ToList();
        if (toDelete.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Supprimer ?",
            toDelete.Count == 1
                ? $"Supprimer la série « {toDelete[0].Title} » ?\n\n" +
                  "Cette action est définitive."
                : $"Supprimer {toDelete.Count} séries ?\n\n" +
                  "Cette action est définitive.",
            "Supprimer",
            "Annuler");
        if (!confirm) return;

        IsLoading = true;
        try
        {
            // Suppression en parallèle, on attend tout puis on agrège.
            // Si une seule échoue, les autres ont quand même eu lieu.
            var tasks = toDelete.Select(s => _seriesService.DeleteSeriesAsync(s.Id))
                                .ToList();
            var results = await Task.WhenAll(tasks);

            int okCount = results.Count(r => r.Success);
            int koCount = results.Length - okCount;

            // Recharge la liste depuis la BDD (c'est le moyen le plus simple
            // d'avoir un état cohérent : statuts + séries restantes).
            await LoadAllAsync();

            // Reset du mode sélection
            SelectedSeriesCount = 0;
            IsSeriesSelectionMode = false;

            // Récap utilisateur
            if (koCount == 0)
            {
                // Pas de toast ici, le rechargement de la liste suffit comme
                // feedback. On évite de spammer un DisplayAlert pour une
                // action réussie comme attendue.
            }
            else
            {
                var firstError = results
                    .Where(r => !r.Success)
                    .Select(r => r.Message)
                    .FirstOrDefault() ?? "Erreur inconnue";

                await Shell.Current.DisplayAlert(
                    "Suppression partielle",
                    $"{okCount} supprimée(s), {koCount} échec(s).\n\n" +
                    $"Premier message d'erreur : {firstError}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteSelectedSeries: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "Erreur",
                $"Une erreur est survenue : {ex.Message}",
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

}
// ⚡ QW2 : converti en ObservableObject pour que la case à cocher se
// rafraîchisse quand on toggle IsSelected en mode multi-sélection.
public partial class SeriesItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public int MaxProjects { get; set; }
    public string CreatorId { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;

    // 🔧 Badge créateur : true si l'user courant a créé cette série.
    // Calculé une seule fois pendant le parsing JSON.
    public bool IsCreatedByMe { get; set; } = false;

    // ⚡ B6 : type de série pour afficher l'emoji adéquat dans la liste
    public bool IsQuiz { get; set; } = false;

    // ⚡ QW2 : sélectionné en mode multi-sélection ?
    // ⚡ FIX PERF : NotifyPropertyChangedFor pour que les couleurs de la
    //    checkbox se mettent à jour automatiquement quand on coche/décoche
    //    sans passer par des DataTrigger XAML (qui sont lents).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckboxBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(CheckboxBorderColor))]
    private bool _isSelected = false;

    public string TypeEmoji => IsQuiz ? "🧠" : "🗳️";
    public string TypeLabel => IsQuiz ? "Quiz" : "Vote";

    public bool HasAccessCode => !string.IsNullOrEmpty(AccessCode);
    public string AccessCodeLabel => string.IsNullOrEmpty(AccessCode)
        ? string.Empty
        : $"🔑 Code : {AccessCode}";

    // Pour un quiz, on n'affiche pas "Max X projets" mais "Quiz" simplement.
    // Pour un vote, on garde "Max X projets" comme avant.
    public string SubtitleLabel => IsQuiz
        ? "🧠 Quiz interactif"
        : $"Max {MaxProjects} projets";

    // ⚡ FIX PERF : on pré-calcule le titre avec la couronne plutôt que
    // d'utiliser un HorizontalStackLayout avec un Label conditionnel.
    // Économise un layout child et un binding dans le DataTemplate.
    public string TitleWithCrown => IsCreatedByMe ? $"👑 {Title}" : Title;

    // ⚡ FIX PERF : couleurs de la checkbox calculées en property au lieu
    // de DataTrigger XAML. Notifié via NotifyPropertyChangedFor sur IsSelected.
    public Color CheckboxBackgroundColor => IsSelected
        ? Color.FromArgb("#C2754C")
        : Colors.White;
    public Color CheckboxBorderColor => IsSelected
        ? Color.FromArgb("#C2754C")
        : Color.FromArgb("#D4C7B5");
}

public class MemberItem
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "member";
    public bool IsAdmin { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string FirstLetter => Username.Length > 0 ? Username[0].ToString() : "?";
}
