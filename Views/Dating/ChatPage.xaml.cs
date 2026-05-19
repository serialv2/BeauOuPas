using BeauOuPas.ViewModels.Dating;

namespace BeauOuPas.Views.Dating;

public partial class ChatPage : ContentPage
{
    private System.Timers.Timer? _refreshTimer;
    private ChatViewModel? _vm;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm == null) return;

        await _vm.LoadMessagesAsync();

        _refreshTimer = new System.Timers.Timer(3000);
        _refreshTimer.Elapsed += async (s, e) =>
        {
            if (_vm != null && !_vm.IsSending)
                await MainThread.InvokeOnMainThreadAsync(
                    async () => await _vm.LoadMessagesAsync());
        };
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
    }

    // ✅ Bouton retour toolbar
    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        StopTimer();
        await Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        StopTimer();
        MainThread.BeginInvokeOnMainThread(async () =>
            await Shell.Current.GoToAsync(".."));
        return true;
    }

    private void StopTimer()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }
}
