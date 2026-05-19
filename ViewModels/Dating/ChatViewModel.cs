using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BeauOuPas.Services;
using BeauOuPas.Localization;
using BeauOuPas.Models;

namespace BeauOuPas.ViewModels.Dating;

[QueryProperty(nameof(ChatId), "chatId")]
public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService;
    public bool IsSending { get; private set; } = false;

    public ChatViewModel(ChatService chatService) { _chatService = chatService; }

    [ObservableProperty] private string _chatId = string.Empty;
    [ObservableProperty] private string _newMessage = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isChatBlocked = false;

    public List<Message> Messages { get; private set; } = new();

    partial void OnChatIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            MainThread.BeginInvokeOnMainThread(async () => await LoadMessagesAsync());
    }

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        if (string.IsNullOrEmpty(ChatId) || IsSending) return;
        try
        {
            Messages = await _chatService.GetMessagesAsync(ChatId);
            await _chatService.MarkMessagesAsReadAsync(ChatId);
            var chat = await _chatService.GetChatAsync(ChatId);
            IsChatBlocked = chat?.IsBlocked ?? false;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}"); }
        finally { OnPropertyChanged(nameof(Messages)); }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage) || string.IsNullOrEmpty(ChatId) || IsSending) return;
        if (IsChatBlocked)
        {
            await Shell.Current.DisplayAlert(L.T("Chat_BlockedTitle"), L.T("Chat_BlockedSend"), L.T("Common_OK"));
            return;
        }

        IsSending = true;
        var content = NewMessage;
        NewMessage = string.Empty;

        var tempMsg = new Message { Id = Guid.NewGuid().ToString(), ChatId = ChatId, SenderId = _chatService.CurrentUserId, Content = content, CreatedAt = DateTime.UtcNow, IsMyMessage = true };
        Messages = new List<Message>(Messages) { tempMsg };
        OnPropertyChanged(nameof(Messages));

        var success = await _chatService.SendMessageAsync(ChatId, content);
        if (!success)
        {
            Messages = Messages.Where(m => m.Id != tempMsg.Id).ToList();
            OnPropertyChanged(nameof(Messages));
            await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("Chat_SendFailed"), L.T("Common_OK"));
            NewMessage = content;
            IsSending = false;
            return;
        }

        IsSending = false;
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task ReportMessageAsync(Message message)
    {
        if (message == null) return;
        var confirm = await Shell.Current.DisplayAlert(
            L.T("Chat_ReportTitle"), $"{message.Content}",
            L.T("Chat_ReportConfirm"), L.T("Common_Cancel"));
        if (!confirm) return;
        var success = await _chatService.ReportMessageAsync(message.Id);
        if (success)
            await Shell.Current.DisplayAlert(L.T("Chat_ReportSentTitle"), L.T("Chat_ReportSentMessage"), L.T("Common_OK"));
        else
            await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("Chat_ReportFailed"), L.T("Common_OK"));
    }

    [RelayCommand]
    private async Task BlockChatAsync()
    {
        var confirm = await Shell.Current.DisplayAlert(
            L.T("Chat_BlockTitle"), L.T("Chat_BlockMessage"),
            L.T("Chat_BlockConfirm"), L.T("Common_Cancel"));
        if (!confirm) return;
        var success = await _chatService.BlockChatAsync(ChatId);
        if (success)
        {
            await Shell.Current.DisplayAlert(L.T("Chat_BlockedTitle"), L.T("Chat_BlockedConversation"), L.T("Common_OK"));
            IsChatBlocked = true;
            await Shell.Current.GoToAsync("..");
        }
        else
            await Shell.Current.DisplayAlert(L.T("Common_Error"), L.T("Chat_BlockFailed"), L.T("Common_OK"));
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
