using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Groups;

[QueryProperty(nameof(SeriesId), "SeriesId")]
[QueryProperty(nameof(SeriesTitle), "SeriesTitle")]
public partial class SeriesResultsViewModel : ObservableObject
{
    private readonly SeriesService _seriesService;
    private readonly SeriesVoteService _voteService;

    public SeriesResultsViewModel(
        SeriesService seriesService,
        SeriesVoteService voteService)
    {
        _seriesService = seriesService;
        _voteService = voteService;
    }

    [ObservableProperty] private string _seriesId = string.Empty;
    [ObservableProperty] private string _seriesTitle = string.Empty;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private List<ProjectResultItem> _results = new();

    partial void OnSeriesIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var seriesProjects = await _seriesService.GetSeriesProjectsAsync(SeriesId);
            var items = new List<ProjectResultItem>();

            foreach (var sp in seriesProjects)
            {
                var project = await _seriesService.GetProjectAsync(sp.ProjectId);
                if (project == null) continue;

                var results = await _voteService.GetProjectResultsAsync(sp.Id, project.Type);

                string summary = project.Type switch
                {
                    "photo_vote" => $"❤️ {results.Likes}  😐 {results.Mehs}  👎 {results.Dislikes}",
                    "duel"       => $"A: {results.VotesA} votes  vs  B: {results.VotesB} votes",
                    "poll"       => string.Join("  ", results.PollResults.Select(kv => $"{kv.Key}: {kv.Value}")),
                    _            => string.Empty
                };

                string winner = project.Type switch
                {
                    "photo_vote" => results.Likes >= results.Dislikes ? "❤️ Aimé !" : "👎 Pas convaincu",
                    "duel"       => results.VotesA >= results.VotesB  ? "🏆 A gagne !" : "🏆 B gagne !",
                    _            => string.Empty
                };

                items.Add(new ProjectResultItem
                {
                    Title      = project.Title,
                    Type       = project.Type,
                    TypeLabel  = project.Type switch
                    {
                        "photo_vote" => "📷",
                        "duel"       => "⚖️",
                        "poll"       => "📊",
                        _            => "📋"
                    },
                    Summary     = summary,
                    Winner      = winner,
                    TotalVotes  = results.TotalVotes,
                    SelfieUrls  = results.SelfieUrls,
                    HasSelfies  = results.SelfieUrls.Any(),
                    Results     = results
                });
            }

            Results = items;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SeriesResults: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("//GroupsPage");
}

public class ProjectResultItem
{
    public string Title      { get; set; } = string.Empty;
    public string Type       { get; set; } = string.Empty;
    public string TypeLabel  { get; set; } = string.Empty;
    public string Summary    { get; set; } = string.Empty;
    public string Winner     { get; set; } = string.Empty;
    public int    TotalVotes { get; set; }
    public List<string> SelfieUrls { get; set; } = new();
    public bool   HasSelfies { get; set; }
    public SeriesProjectResults Results { get; set; } = new();
}
