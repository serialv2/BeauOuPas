using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;
using System.Text.Json;

namespace BeauOuPas.ViewModels.Projects;

public partial class MyProjectsViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly AuthService _authService;

    public MyProjectsViewModel(
        ProjectService projectService,
        AuthService authService)
    {
        _projectService = projectService;
        _authService = authService;
    }

    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isEmpty = false;

    public List<ProjectItemViewModel> Projects { get; private set; } = new();

    // ─── Charger mes projets ─────────────────────────────────────────
    // ⚡ OPTIMISATION : 1 seul appel RPC au lieu de N+1 requêtes Supabase.
    // Avant : pour 20 projets, ~41 requêtes en série (5-10s sur mobile).
    // Après : 1 seule requête, ~0.3s.
    [RelayCommand]
    public async Task LoadProjectsAsync()
    {
        IsLoading = true;
        IsEmpty = false;

        try
        {
            // 1) Un seul aller-retour réseau qui ramène tout en JSON
            var json = await _projectService.GetMyProjectsWithStatsJsonAsync();

            // 2) Parsing du JSON et construction des ProjectItemViewModel
            var items = ParseProjectsJson(json);

            Projects = items;
            IsEmpty = !Projects.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadProjects error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Projects));
        }
    }

    // ─── Parser le JSON renvoyé par la RPC ──────────────────────────
    // Format attendu (cf. RPC get_my_projects_with_stats) :
    // [
    //   {
    //     "id": "...", "type": "photo_vote"|"duel"|"poll",
    //     "title": "...", "status": "approved"|"pending"|"rejected",
    //     "created_at": "...", "is_open": bool,
    //     "gender_filter": "...", "min_age": int, "max_age": int,
    //     "owner_id": "...", "reject_reason": null|"...",
    //     "photo_left_url": "...", "photo_right_url": null|"...",
    //     "stats_photo": null | { total_votes, total_jaime, total_moyen, total_pasfan },
    //     "stats_duel":  null | { total_votes, votes_left, votes_right },
    //     "stats_poll":  null | { total_votes }
    //   }
    // ]
    private List<ProjectItemViewModel> ParseProjectsJson(string json)
    {
        var items = new List<ProjectItemViewModel>();
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return items;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return items;

            foreach (var p in doc.RootElement.EnumerateArray())
            {
                var type = GetString(p, "type") ?? string.Empty;
                var photoLeftUrl = GetString(p, "photo_left_url") ?? string.Empty;
                var photoRightUrl = GetString(p, "photo_right_url") ?? string.Empty;

                // Photo principale + résumé des votes selon le type
                string photoUrl = string.Empty;
                string voteSummary = string.Empty;

                if (type == "photo_vote")
                {
                    photoUrl = photoLeftUrl;
                    voteSummary = BuildPhotoVoteSummary(p);
                }
                else if (type == "duel")
                {
                    (photoUrl, voteSummary) = BuildDuelSummary(p, photoLeftUrl, photoRightUrl);
                }
                else if (type == "poll")
                {
                    photoUrl = photoLeftUrl;
                    voteSummary = BuildPollSummary(p);
                }

                items.Add(new ProjectItemViewModel
                {
                    Id = GetString(p, "id") ?? string.Empty,
                    OwnerId = GetString(p, "owner_id") ?? string.Empty,
                    Title = GetString(p, "title") ?? string.Empty,
                    Type = type,
                    Status = GetString(p, "status") ?? string.Empty,
                    CreatedAt = GetDateTime(p, "created_at"),
                    IsOpen = GetBool(p, "is_open"),
                    GenderFilter = GetString(p, "gender_filter") ?? string.Empty,
                    MinAge = GetInt(p, "min_age"),
                    MaxAge = GetInt(p, "max_age"),
                    PhotoUrl = photoUrl,
                    PhotoLeftUrl = photoLeftUrl,
                    PhotoRightUrl = photoRightUrl,
                    RejectReason = GetString(p, "reject_reason"),
                    VoteSummary = voteSummary
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseProjectsJson error: {ex.Message}");
        }

        return items;
    }

    // ─── Helpers de parsing JSON (sécurisés contre les nulls) ───────

    private static string? GetString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.String) return v.GetString();
        return v.ToString();
    }

    private static int GetInt(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        return 0;
    }

    private static bool GetBool(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return false;
        return v.ValueKind == JsonValueKind.True;
    }

    private static DateTime GetDateTime(JsonElement obj, string key)
    {
        var s = GetString(obj, key);
        if (string.IsNullOrEmpty(s)) return DateTime.MinValue;
        if (DateTime.TryParse(s, out var dt)) return dt;
        return DateTime.MinValue;
    }

    // ─── Builders de résumés de vote (équivalents au code original) ──

    private static string BuildPhotoVoteSummary(JsonElement p)
    {
        if (!p.TryGetProperty("stats_photo", out var s) || s.ValueKind == JsonValueKind.Null)
            return string.Empty;

        var total = GetInt(s, "total_votes");
        if (total == 0) return L.T("MyProjects_NoVoteYet");

        var jaime = GetInt(s, "total_jaime");
        var moyen = GetInt(s, "total_moyen");
        var pasfan = GetInt(s, "total_pasfan");

        return L.F("MyProjects_PhotoSummary", jaime, moyen, pasfan, total);
    }

    private static (string PhotoUrl, string Summary) BuildDuelSummary(
        JsonElement p, string photoLeftUrl, string photoRightUrl)
    {
        if (!p.TryGetProperty("stats_duel", out var s) || s.ValueKind == JsonValueKind.Null)
            return (photoLeftUrl, string.Empty);

        var total = GetInt(s, "total_votes");
        if (total == 0)
            return (photoLeftUrl, L.T("MyProjects_NoVoteYet"));

        var votesLeft = GetInt(s, "votes_left");
        var votesRight = GetInt(s, "votes_right");

        // La photo affichée est celle du gagnant (logique d'origine conservée)
        var winnerUrl = votesLeft >= votesRight ? photoLeftUrl : photoRightUrl;

        var pctLeft = (int)Math.Round(votesLeft * 100.0 / total);
        var pctRight = 100 - pctLeft;

        return (winnerUrl, L.F("MyProjects_DuelSummary", pctLeft, pctRight, total));
    }

    private static string BuildPollSummary(JsonElement p)
    {
        if (!p.TryGetProperty("stats_poll", out var s) || s.ValueKind == JsonValueKind.Null)
            return string.Empty;

        var total = GetInt(s, "total_votes");
        return total == 0
            ? L.T("MyProjects_NoVoteYet")
            : L.F("MyProjects_PollSummary", total);
    }

    // ─── Afficher le détail ──────────────────────────────────────────
    [RelayCommand]
    private async Task ShowDetailAsync(ProjectItemViewModel item)
    {
        if (item == null) return;

        // Récupérer le ProjectDetailViewModel depuis le BindingContext
        // de la page via un event — on utilise un message CommunityToolkit
        // ou plus simplement on expose une propriété observable
        DetailRequest = item;
    }

    // Propriété surveillée par le code-behind pour déclencher la popup
    private ProjectItemViewModel? _detailRequest;
    public ProjectItemViewModel? DetailRequest
    {
        get => _detailRequest;
        set
        {
            _detailRequest = value;
            OnPropertyChanged();
        }
    }

    // ─── Fermer un projet ────────────────────────────────────────────
    [RelayCommand]
    private async Task CloseProjectAsync(string projectId)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            L.T("MyProjects_Close_Title"),
            L.T("MyProjects_Close_Msg"),
            L.T("Common_Yes"), L.T("Common_No"));

        if (!confirm) return;

        await _projectService.CloseProjectAsync(projectId);
        await LoadProjectsAsync();
    }

    // ─── Supprimer un projet ─────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteProjectAsync(string projectId)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            L.T("MyProjects_Delete_Title"),
            L.T("MyProjects_Delete_Msg"),
            L.T("Common_Yes"), L.T("Common_No"));

        if (!confirm) return;

        var success = await _projectService.DeleteProjectAsync(projectId);

        if (success)
            await LoadProjectsAsync();
        else
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"), L.T("MyProjects_DeleteFailed_Msg"), L.T("Common_OK"));
    }

    // ─── Créer un nouveau projet ─────────────────────────────────────
    [RelayCommand]
    private async Task CreateNewProjectAsync()
    {
        await Shell.Current.GoToAsync("//CreateProjectPage");
    }
}

// ─── ViewModel item ───────────────────────────────────────────────────
public class ProjectItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string PhotoLeftUrl { get; set; } = string.Empty;
    public string PhotoRightUrl { get; set; } = string.Empty;
    public string GenderFilter { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
    public string VoteSummary { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public bool IsOpen { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TypeLabel => Type switch
    {
        "photo_vote" => L.T("MyProjects_Type_Photo"),
        "duel" => L.T("MyProjects_Type_Duel"),
        "poll" => L.T("MyProjects_Type_Poll"),
        _ => Type
    };

    public string StatusLabel => Status switch
    {
        "pending" => L.T("MyProjects_Status_Pending"),
        "approved" => IsOpen ? L.T("MyProjects_Status_Online") : L.T("MyProjects_Status_Closed"),
        "rejected" => L.T("MyProjects_Status_Rejected"),
        _ => L.T("MyProjects_Status_Unknown")
    };

    public Color StatusColor => Status switch
    {
        "pending" => Color.FromArgb("#C9943E"),
        "approved" => IsOpen
            ? Color.FromArgb("#4A7A52")
            : Color.FromArgb("#8A6F4A"),
        "rejected" => Color.FromArgb("#B5482F"),
        _ => Color.FromArgb("#8A6F4A")
    };

    public string GenderLabel => GenderFilter switch
    {
        "male" => L.T("MyProjects_Gender_Male"),
        "female" => L.T("MyProjects_Gender_Female"),
        _ => L.T("MyProjects_Gender_All")
    };

    public string AgeLabel => L.F("MyProjects_AgeRange", MinAge, MaxAge);
    public string DateLabel => CreatedAt.ToString("dd/MM/yyyy");
    public bool CanClose => Status == "approved" && IsOpen;
    public bool IsRejected => Status == "rejected";
    public bool IsDuelType => Type == "duel";
    public bool HasVotes => !string.IsNullOrEmpty(VoteSummary);
}