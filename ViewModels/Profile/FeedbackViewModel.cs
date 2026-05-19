using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Profile;

public partial class FeedbackViewModel : ObservableObject
{
    private readonly FeedbackService _feedbackService;

    public FeedbackViewModel(FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [ObservableProperty] private int _rating = 5;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;

    // Couleurs des étoiles
    public Color Star1Color => Rating >= 1 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color Star2Color => Rating >= 2 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color Star3Color => Rating >= 3 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color Star4Color => Rating >= 4 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
    public Color Star5Color => Rating >= 5 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");

    [RelayCommand]
    private void SetRating(string value)
    {
        Rating = int.Parse(value);
        OnPropertyChanged(nameof(Star1Color));
        OnPropertyChanged(nameof(Star2Color));
        OnPropertyChanged(nameof(Star3Color));
        OnPropertyChanged(nameof(Star4Color));
        OnPropertyChanged(nameof(Star5Color));
    }

    [RelayCommand]
    private async Task SendFeedbackAsync()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            ErrorMessage = L.T("Feedback_ErrorEmpty");
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;

        try
        {
            var success = await _feedbackService.SendFeedbackAsync(Rating, Message);
            if (success)
            {
                await Shell.Current.DisplayAlert(
                    L.T("Feedback_SuccessTitle"),
                    L.T("Feedback_SuccessMessage"),
                    L.T("Common_OK"));
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = L.T("Common_Error");
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally { IsLoading = false; }
    }
}
