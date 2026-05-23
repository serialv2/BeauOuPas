using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Models;
using BeauOuPas.Services;
using BeauOuPas.Localization;
using System.Text.Json;

namespace BeauOuPas.ViewModels.Groups;

public partial class GroupsViewModel : ObservableObject
{
    private readonly GroupService _groupService;
    private readonly AuthService _authService;
    private readonly SeriesService _seriesService;
    private readonly SeriesInvitationService _invitationService;

    public GroupsViewModel(
        GroupService groupService,
        AuthService authService,
        SeriesService seriesService,
        SeriesInvitationService invitationService)
    {
        _groupService = groupService;
        _authService = authService;
        _seriesService = seriesService;
        _invitationService = invitationService;
    }

    // ─── Groupes ──────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasGroups = false;
    [ObservableProperty] private bool _isEmpty = false;
    [ObservableProperty] private List<GroupItem> _groups = new();

    // ─── Séries standalone ────────────────────────────────────────
    [ObservableProperty] private bool _isLoadingSeries = false;
    [ObservableProperty] private bool _hasStandaloneSeries = false;
    [ObservableProperty] private bool _noStandaloneSeries = false;
    [ObservableProperty] private List<StandaloneSeriesItem> _standaloneSeries = new();

    // ─── Invitations en attente ───────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InvitationsBadge))]
    [NotifyPropertyChangedFor(nameof(HasPendingInvitations))]
    private int _pendingInvitationsCount = 0;

    public bool HasPendingInvitations => PendingInvitationsCount > 0;
    public string InvitationsBadge => PendingInvitationsCount > 0
        ? $"📩 {PendingInvitationsCount} invitation{(PendingInvitationsCount > 1 ? "s" : "")} en attente"
        : string.Empty;

    // ⚡ REFONDÉ : utilise la nouvelle RPC pour charger en 1 requête
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var json = await _groupService.GetMyGroupsWithStatsJsonAsync();
            var items = ParseGroupsJson(json);

            Groups = items;
            HasGroups = items.Any();
            IsEmpty = !items.Any();

            await RefreshInvitationsCountAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadGroups: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // ─── Parser JSON renvoyé par la RPC ────────────────────────────
    private List<GroupItem> ParseGroupsJson(string? json)
    {
        var items = new List<GroupItem>();
        if (string.IsNullOrWhiteSpace(json)) return items;

        // 🔧 Ajout badge créateur : on récupère l'id du user courant une seule fois
        // pour éviter d'appeler _authService dans la boucle.
        var myUserId = _authService.CurrentUser?.Id ?? string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return items;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var ownerId = GetString(el, "owner_id") ?? string.Empty;
                items.Add(new GroupItem
                {
                    Id = GetString(el, "id") ?? string.Empty,
                    Name = GetString(el, "name") ?? string.Empty,
                    Description = GetString(el, "description"),
                    AvatarEmoji = "👥",
                    OwnerId = ownerId,
                    // 🔧 Badge créateur : true si je suis le owner du groupe
                    IsOwnedByMe = !string.IsNullOrEmpty(myUserId) && ownerId == myUserId,
                    MemberCount = GetInt(el, "member_count"),
                    ActiveSeriesCount = GetInt(el, "active_series_count")
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseGroupsJson: {ex.Message}");
        }

        return items;
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

    public async Task RefreshInvitationsCountAsync()
    {
        try
        {
            var invitations = await _invitationService.GetMyPendingInvitationsAsync();
            PendingInvitationsCount = invitations.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshInvitations: {ex.Message}");
        }
    }

    public async Task LoadStandaloneSeriesAsync()
    {
        if (IsLoadingSeries) return;
        IsLoadingSeries = true;
        try
        {
            var series = await _seriesService.GetMyStandaloneSeriesAsync();
            var items = series.Select(s => new StandaloneSeriesItem
            {
                Id = s.Id,
                Title = s.Title,
                Status = s.Status,
                MaxProjects = s.MaxProjects,
                AccessCode = s.AccessCode ?? string.Empty,
                StatusLabel = s.Status switch
                {
                    "preparing" => L.T("Series_Status_Preparing"),
                    "active" => L.T("Series_Status_Active"),
                    "finished" => L.T("Series_Status_Finished"),
                    _ => s.Status
                },
                StatusColor = s.Status switch
                {
                    "preparing" => Color.FromArgb("#C9943E"),
                    "active" => Color.FromArgb("#4A7A52"),
                    "finished" => Color.FromArgb("#8A6F4A"),
                    _ => Color.FromArgb("#8A6F4A")
                }
            }).ToList();

            StandaloneSeries = items;
            HasStandaloneSeries = items.Any();
            NoStandaloneSeries = !items.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStandaloneSeries: {ex.Message}");
        }
        finally { IsLoadingSeries = false; }
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        await Shell.Current.GoToAsync("CreateGroupPage");
    }

    [RelayCommand]
    private async Task OpenGroupAsync(GroupItem group)
    {
        await Shell.Current.GoToAsync("GroupDetailPage",
            new Dictionary<string, object>
            {
                { "GroupId", group.Id },
                { "GroupName", group.Name }
            });
    }

    [RelayCommand]
    private async Task CreateStandaloneSeriesAsync()
    {
        await Shell.Current.GoToAsync("CreateSeriesPage",
            new Dictionary<string, object>
            {
                { "GroupId", string.Empty },
                { "GroupName", string.Empty }
            });
    }

    [RelayCommand]
    private async Task OpenStandaloneSeriesAsync(StandaloneSeriesItem series)
    {
        await Shell.Current.GoToAsync("SeriesDetailPage",
            new Dictionary<string, object>
            {
                { "SeriesId", series.Id },
                { "SeriesTitle", series.Title },
                { "GroupId", string.Empty }
            });
    }

    [RelayCommand]
    private async Task OpenInvitationsAsync()
    {
        await Shell.Current.GoToAsync("MyInvitationsPage");
    }

    [RelayCommand]
    private async Task CopyAccessCodeAsync(StandaloneSeriesItem series)
    {
        if (series == null || string.IsNullOrEmpty(series.AccessCode)) return;
        try
        {
            await Clipboard.Default.SetTextAsync(series.AccessCode);
            await Shell.Current.DisplayAlert(
                L.T("Group_Copied_Title"),
                L.F("Group_CodeCopied_Msg", series.AccessCode),
                L.T("Common_OK"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CopyAccessCode: {ex.Message}");
        }
    }
}

public class GroupItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AvatarEmoji { get; set; } = "👥";
    public string OwnerId { get; set; } = string.Empty;

    // 🔧 Badge créateur : true si l'user courant est le owner du groupe.
    // Calculé une seule fois dans ParseGroupsJson pour éviter de
    // dépendre d'AuthService côté UI.
    public bool IsOwnedByMe { get; set; } = false;

    public int MemberCount { get; set; }
    public string MemberCountLabel => L.F("Groups_MemberCount", MemberCount);
    public int ActiveSeriesCount { get; set; }
    public bool HasActiveSeries => ActiveSeriesCount > 0;
    public string ActiveSeriesLabel => ActiveSeriesCount > 0
        ? L.F("Groups_ActiveSeriesCount", ActiveSeriesCount) : string.Empty;
}

public class StandaloneSeriesItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public int MaxProjects { get; set; }
    public string AccessCode { get; set; } = string.Empty;
}