using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(GroupId), "GroupId")]
[QueryProperty(nameof(GroupName), "GroupName")]
public partial class CreateSeriesViewModel : ObservableObject
{
    private readonly SeriesService _seriesService;

    public CreateSeriesViewModel(SeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    [ObservableProperty] private string? _groupId = null;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasGroup = false;

    partial void OnGroupIdChanged(string? value)
        => HasGroup = !string.IsNullOrEmpty(value);

    // ─── Max projets ──────────────────────────────────────────────
    [ObservableProperty] private int _maxProjects = 10;
    [ObservableProperty] private bool _max5 = false;
    [ObservableProperty] private bool _max10 = true;
    [ObservableProperty] private bool _max15 = false;
    [ObservableProperty] private bool _max20 = false;

    // ─── Max par membre ───────────────────────────────────────────
    [ObservableProperty] private int _maxPerMember = 2;
    [ObservableProperty] private bool _per1 = false;
    [ObservableProperty] private bool _per2 = true;
    [ObservableProperty] private bool _per3 = false;
    [ObservableProperty] private bool _perAll = false;

    // ─── ⚡ NOUVEAU : Visibilité des projets ──────────────────────
    /// <summary>
    /// null = pas encore choisi (validation : doit être true ou false avant Create).
    /// true = projets cachés (effet surprise).
    /// false = projets visibles par tous dès l'ajout.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibilityChosen))]
    [NotifyPropertyChangedFor(nameof(IsHiddenSelected))]
    [NotifyPropertyChangedFor(nameof(IsVisibleSelected))]
    private bool? _projectsHidden = null;

    public bool VisibilityChosen => ProjectsHidden.HasValue;
    public bool IsHiddenSelected => ProjectsHidden == true;
    public bool IsVisibleSelected => ProjectsHidden == false;

    [RelayCommand]
    private void SelectVisibility(string value)
    {
        // value attendu: "hidden" ou "visible"
        ProjectsHidden = value switch
        {
            "hidden" => true,
            "visible" => false,
            _ => null
        };
    }

    // ─── ⚡ NOUVEAU : Affichage des statistiques détaillées ────────
    /// <summary>
    /// true = la TV affichera la phase stats (par genre/âge) après chaque projet.
    /// false = la TV sautera cette phase, plus rapide.
    /// Activé par défaut.
    /// </summary>
    [ObservableProperty] private bool _showStats = true;

    [RelayCommand]
    private void ToggleShowStats()
    {
        ShowStats = !ShowStats;
    }

    // ⚡ MAI 2026 — B2 : autorisation pour les membres invités d'ajouter
    // leurs propres projets à la série. True par défaut (mode collaboratif).
    [ObservableProperty] private bool _membersCanAddProjects = true;

    [RelayCommand]
    private void ToggleMembersCanAddProjects()
    {
        MembersCanAddProjects = !MembersCanAddProjects;
    }

    [RelayCommand]
    private void SelectMax(string value)
    {
        MaxProjects = int.Parse(value);
        Max5 = value == "5"; Max10 = value == "10";
        Max15 = value == "15"; Max20 = value == "20";
    }

    [RelayCommand]
    private void SelectPer(string value)
    {
        MaxPerMember = value == "0" ? 999 : int.Parse(value);
        Per1 = value == "1"; Per2 = value == "2";
        Per3 = value == "3"; PerAll = value == "0";
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlert("Erreur", "Le titre est obligatoire.", "OK");
            return;
        }

        // ⚡ Validation du choix de visibilité (obligatoire).
        if (!ProjectsHidden.HasValue)
        {
            await Shell.Current.DisplayAlert(
                "Choix manquant",
                "Choisis si les projets sont cachés ou visibles avant le démarrage.",
                "OK");
            return;
        }

        IsLoading = true;
        try
        {
            var effectiveGroupId = string.IsNullOrEmpty(GroupId) ? null : GroupId;

            var series = await _seriesService.CreateSeriesAsync(
                groupId: effectiveGroupId,
                title: Title.Trim(),
                description: Description.Trim(),
                maxProjects: MaxProjects,
                maxPerMember: MaxPerMember,
                projectsHidden: ProjectsHidden.Value,
                showStats: ShowStats,
                isStandalone: effectiveGroupId == null,
                membersCanAddProjects: MembersCanAddProjects);

            if (series == null)
            {
                await Shell.Current.DisplayAlert("Erreur", "Impossible de créer la série.", "OK");
                return;
            }

            await Shell.Current.DisplayAlert(
                "✅ Série créée !",
                $"Code d'accès : {series.AccessCode}\n\nPartage ce code pour inviter des participants !",
                "Super !");

            // Rediriger vers la série créée (route absolue → reset la pile,
            // le bouton retour ne ramène pas au menu de choix ni au formulaire)
            // Retirer le formulaire de la pile, puis aller au détail de la série créée
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync(
                $"SeriesDetailPage?SeriesId={series.Id}&SeriesTitle={Uri.EscapeDataString(series.Title ?? string.Empty)}&GroupId={effectiveGroupId ?? string.Empty}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Erreur", ex.Message, "OK");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}