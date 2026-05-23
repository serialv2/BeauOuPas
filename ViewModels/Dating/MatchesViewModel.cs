using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;

namespace BeauOuPas.ViewModels.Dating;

public partial class MatchesViewModel : ObservableObject
{
    private readonly ChatService _chatService;

    public MatchesViewModel(ChatService chatService)
    {
        _chatService = chatService;
    }

    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isEmpty = false;

    public List<MatchItem> Matches { get; private set; } = new();

    [RelayCommand]
    public async Task LoadMatchesAsync()
    {
        IsLoading = true;
        IsEmpty = false;

        try
        {
            Matches = await _chatService.GetMatchesAsync();
            IsEmpty = !Matches.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Matches error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Matches));
        }
    }

    [RelayCommand]
    public async Task OpenChatAsync(string chatId)
    {
        System.Diagnostics.Debug.WriteLine(
            $"Opening chat: [{chatId}]");

        if (string.IsNullOrEmpty(chatId))
        {
            await Shell.Current.DisplayAlert(
                L.T("Common_Error"), L.T("Matches_ChatIdEmpty"), L.T("Common_OK"));
            return;
        }

        await Shell.Current.GoToAsync(
            $"ChatPage?chatId={chatId}");
    }
}