using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;

namespace BeauOuPas.ViewModels.Groups;

public partial class JoinSeriesViewModel : ObservableObject
{
    private readonly SeriesService _seriesService;

    public JoinSeriesViewModel(SeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;

    [RelayCommand]
    private async Task JoinAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Saisis un code d'accès.";
            HasError = true;
            return;
        }

        HasError = false;
        IsLoading = true;
        try
        {
            var (success, error, series) = await _seriesService.JoinSeriesByCodeAsync(Code.Trim().ToUpper());

            if (!success || series == null)
            {
                ErrorMessage = error;
                HasError = true;
                return;
            }

            // Log de debug pour vérifier que IsQuiz remonte bien depuis Postgrest
            System.Diagnostics.Debug.WriteLine(
                "[Join DEBUG] Title=" + series.Title +
                ", Status=" + series.Status +
                ", IsQuiz=" + series.IsQuiz +
                ", QuizStyle=" + (series.QuizStyle ?? "null"));

            if (series.Status == "preparing")
            {
                await Shell.Current.GoToAsync("SeriesDetailPage",
                    new Dictionary<string, object>
                    {
                        { "SeriesId", series.Id },
                        { "SeriesTitle", series.Title },
                        { "GroupId", series.GroupId ?? string.Empty }
                    });
            }
            else if (series.Status == "active")
            {
                // ⚡ Routage selon le type de série
                var route = series.IsQuiz ? "QuizPlayPage" : "SeriesVotePage";
                await Shell.Current.GoToAsync(route,
                    new Dictionary<string, object>
                    {
                        { "SeriesId", series.Id },
                        { "SeriesTitle", series.Title }
                    });
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    "Série terminée",
                    $"La série \"{series.Title}\" est terminée.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}