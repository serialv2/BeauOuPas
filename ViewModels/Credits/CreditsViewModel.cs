using BeauOuPas.Models;
using BeauOuPas.Resources.Strings;
using BeauOuPas.Services;
using BeauOuPas.Services.Ads;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeauOuPas.ViewModels.Credits;

public partial class CreditsViewModel : ObservableObject
{
    private readonly CreditService _creditService;
    private readonly AppSettingsService _settingsService;
    private readonly IAdService _adService;

    public CreditsViewModel(
        CreditService creditService,
        AppSettingsService settingsService,
        IAdService adService)
    {
        _creditService = creditService;
        _settingsService = settingsService;
        _adService = adService;
    }

    // ─── Propriétés ──────────────────────────────────────────────────
    [ObservableProperty] private int _credits = 0;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError = false;

    // ─── Visibilité conditionnelle des coûts ─────────────────────────
    // ⚡ NOUVEAU : masque les coûts "Rencontre" et "Super Meet" si les
    // rencontres sont désactivées globalement (app_settings.meet_enabled)
    [ObservableProperty] private bool _meetVisible = true;

    // ─── Prix des actions ────────────────────────────────────────────
    [ObservableProperty] private int _costProject = 10;
    [ObservableProperty] private int _costBoost = 20;
    [ObservableProperty] private int _costMeet = 3;
    [ObservableProperty] private int _costRewind = 5;

    // ⚡ NOUVEAUX coûts (manquants dans la liste précédente)
    [ObservableProperty] private int _costSuperMeet = 10;
    [ObservableProperty] private int _costSeriesTemplate = 2;
    [ObservableProperty] private int _costSeriesProject = 3;
    [ObservableProperty] private int _costVisualTheme = 5;
    [ObservableProperty] private int _costVoteReceived = 1;

    // ─── Gains ───────────────────────────────────────────────────────
    [ObservableProperty] private int _gainVote = 1;
    [ObservableProperty] private int _gainAdBanner = 1;
    [ObservableProperty] private int _gainAdReward = 5;
    [ObservableProperty] private int _gainWelcome = 10;

    // ⚡ NOUVEAU : crédits gagnés en ajoutant un ami
    [ObservableProperty] private int _gainFriend = 5;

    // ─── Historique transactions ─────────────────────────────────────
    public List<CreditTransaction> Transactions { get; private set; }
        = new();

    // ─── Charger les données ─────────────────────────────────────────
    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;

        try
        {
            // Crédits actuels
            Credits = await _creditService.GetCreditsAsync();

            // Visibilité Meet
            MeetVisible = await _settingsService.IsMeetEnabledAsync();

            // ─── Prix depuis app_settings ───
            CostProject = await _settingsService
                .GetCreditsCostProjectAsync();
            CostBoost = await _settingsService
                .GetCreditsCostBoostAsync();
            CostMeet = await _settingsService
                .GetCreditsCostMeetAsync();
            CostRewind = await _settingsService
                .GetCreditsCostRewindAsync();

            // ⚡ Nouveaux coûts
            CostSeriesTemplate = await _settingsService
                .GetCreditsCostSeriesTemplateAsync();
            CostSeriesProject = await _settingsService
                .GetCreditsCostSeriesProjectAsync();
            CostVisualTheme = await _settingsService
                .GetCreditsCostVisualThemeAsync();
            CostVoteReceived = await _settingsService
                .GetCreditsCostVoteReceivedAsync();

            // ⚡ Super Meet : nouvelle méthode dans AppSettingsService
            CostSuperMeet = await _settingsService.GetMeetSuperCostAsync();

            // ─── Gains ───
            GainVote = await _settingsService.GetCreditsVoteAsync();
            GainAdBanner = await _settingsService.GetCreditsAdBannerAsync();
            GainAdReward = await _settingsService.GetCreditsAdRewardAsync();
            GainWelcome = await _settingsService.GetCreditsWelcomeAsync();
            GainFriend = await _settingsService.GetCreditsGainFriendAsync();

            // Historique
            Transactions = await _creditService
                .GetTransactionsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Transactions));
        }
    }

    // ─── Gagner des crédits via pub rewarded ─────────────────────────
    [RelayCommand]
    private async Task WatchAdAsync()
    {
        IsLoading = true;
        HasError = false;

        try
        {
            var watched = await _adService.ShowRewardedAsync();
            if (watched)
            {
                await _creditService.AddAdRewardCreditsAsync();
                await LoadAsync();

                var rm = AppResources.ResourceManager;
                var culture = AppResources.Culture;

                var alertTitle = rm.GetString("Credits_AdWatchedTitle", culture)
                                 ?? "Bravo ! 🎉";
                var alertBody = string.Format(
                    rm.GetString("Credits_AdWatchedMessage", culture)
                        ?? "+{0} crédits gagnés !",
                    GainAdReward);
                var alertOk = rm.GetString("Common_OK", culture) ?? "OK";

                await Shell.Current.DisplayAlert(alertTitle, alertBody, alertOk);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}