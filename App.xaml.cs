using BeauOuPas.Services;
using BeauOuPas.Localization;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas;

public partial class App : Application
{
    private readonly AuthService _authService;
    private readonly AppShell _appShell;
    private readonly PromoMessageService _promoService;
    private readonly NotificationService _notificationService;
    private readonly CreditService _creditService;
    private readonly FriendService _friendService;
    private readonly AppSettingsService _settingsService;
    private readonly PresenceService _presenceService;
    private readonly SeriesService _seriesService;

    public App(
        AuthService authService,
        AppShell appShell,
        PromoMessageService promoService,
        NotificationService notificationService,
        CreditService creditService,
        FriendService friendService,
        AppSettingsService settingsService,
        PresenceService presenceService,
        SeriesService seriesService)
    {
        InitializeComponent();
        _authService = authService;
        _appShell = appShell;
        _promoService = promoService;
        _notificationService = notificationService;
        _creditService = creditService;
        _friendService = friendService;
        _settingsService = settingsService;
        _presenceService = presenceService;
        _seriesService = seriesService;
        MainPage = _appShell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Destroying += (s, e) => _presenceService.Stop();

        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(
            nameof(IWindow), (handler, view) =>
            {
#if ANDROID
                var activity = Platform.CurrentActivity as AndroidX.Activity.ComponentActivity;
                if (activity != null)
                    activity.OnBackPressedDispatcher.AddCallback(new BackPressedCallback(activity));
#endif
            });

        return window;
    }

    protected override void OnStart()
    {
        base.OnStart();
        var shell = MainPage as AppShell;
        if (shell == null) return;
        shell.Loaded += async (s, e) => await InitializeSessionAsync();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _presenceService.Stop();
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_authService.CurrentUser != null)
        {
            _presenceService.Start();

            // ─── Traiter un code d'invitation en attente ─────────────
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(600);
                await HandlePendingInviteCodeAsync();
                await HandlePendingSeriesCodeAsync();  // ⚡ NOUVEAU : QR code TV
            });
        }
    }

    private async Task InitializeSessionAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void Log(string step) =>
            System.Diagnostics.Debug.WriteLine($"[STARTUP] {sw.ElapsedMilliseconds}ms - {step}");

        try
        {
            Log("Début InitializeSessionAsync");

            // ⚡ OPTI 1 : Lancer kill-switch + version EN PARALLÈLE (gain ~400ms)
            var killSwitchTask = _settingsService.IsAppEnabledAsync();
            var requiredVersionTask = _settingsService.GetRequiredVersionAsync();

            // Attendre les 2 résultats en parallèle
            await Task.WhenAll(killSwitchTask, requiredVersionTask);
            var appEnabled = killSwitchTask.Result;
            var requiredVersion = requiredVersionTask.Result;
            Log("Kill switch + Version requise chargés (parallèle)");

            // 1. Kill switch
            if (!appEnabled)
            {
                var message = await _settingsService.GetMaintenanceMessageAsync();
                MainPage = new Views.Shared.MaintenancePage(message);
                return;
            }

            // 2. Contrôle version
            var currentVersion = AppInfo.VersionString;
            if (CompareVersions(currentVersion, requiredVersion) < 0)
            {
                MainPage = new Views.Shared.UpdateRequiredPage();
                return;
            }

            // 3. Session
            var hasSession = await _authService.HasValidSessionAsync();
            Log("HasValidSession chargé");
            if (!hasSession) return;

            var result = await _authService.CheckSessionBanAsync();
            Log("CheckSessionBan chargé");

            if (result.IsBanned)
            {
                var banDate = result.BannedAt.HasValue
                    ? result.BannedAt.Value.ToString("dd/MM/yyyy")
                    : "date inconnue";
                var reason = string.IsNullOrWhiteSpace(result.BannedReason)
                    ? "Aucune raison spécifiée."
                    : result.BannedReason;

                var popup = new Views.Auth.BanPopup(reason, banDate);
                await MainPage!.ShowPopupAsync(popup);
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            if (result.Success)
            {
                // ⚡ OPTI 2 : GrantWelcomeCredits + GetCurrentProfile en parallèle
                var creditsTask = _creditService.GrantWelcomeCreditsIfNeededAsync();
                var profileTask = _authService.GetCurrentProfileAsync();

                await Task.WhenAll(creditsTask, profileTask);
                var currentProfile = profileTask.Result;
                Log("GrantWelcomeCredits + GetCurrentProfile (parallèle)");

                if (currentProfile != null && !currentProfile.ProfileCompleted)
                {
                    await Shell.Current.GoToAsync("//ProfileSettingsPage");
                    return;
                }

                if (Shell.Current is AppShell appShell)
                    appShell.GoToMainApp();
                Log("GoToMainApp fait → APP UTILISABLE");

                _presenceService.Start();

                // ⚡ OPTI 3 : SetPlatform en arrière-plan (FIRE & FORGET)
                // Pas besoin d'attendre, c'est juste pour les logs serveur
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _presenceService.SetPlatformAsync(
#if IOS
                            "ios"
#else
                            "android"
#endif
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] SetPlatform: {ex.Message}");
                    }
                });

                // ⚡ OPTI 4 : Notifications, invitations, promo en arrière-plan (FIRE & FORGET)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _notificationService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FCM init error: {ex.Message}");
                    }
                });

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandlePendingInviteCodeAsync();
                        await HandlePendingSeriesCodeAsync();  // ⚡ NOUVEAU : QR code TV
                        await ShowPromoMessageIfNeededAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] Background tasks: {ex.Message}");
                    }
                });

                // ⚡ NOUVEAU : Nettoyage des sessions TV orphelines
                // Si l'app a été tuée pendant qu'une série/quiz était en mode TV,
                // tv_active reste à true en BDD. Au démarrage, on désactive
                // automatiquement toutes les sessions TV dont l'user est créateur.
                // Fire-and-forget : ne bloque jamais le démarrage.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _seriesService.DeactivateAllMyActiveTvSessionsAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] DeactivateAllTv: {ex.Message}");
                    }
                });

                // ⚡ OPTI 5 : Plus de Task.Delay(500) inutile
                Log("FIN InitializeSessionAsync (le reste est en arrière-plan)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] InitializeSessionAsync: {ex.Message}");
        }
    }

    private static int CompareVersions(string current, string required)
    {
        try
        {
            var c = Version.Parse(current);
            var r = Version.Parse(required);
            return c.CompareTo(r);
        }
        catch { return 0; }
    }

    public async Task HandlePendingInviteCodeAsync()
    {
        try
        {
            var code = Preferences.Get("pending_invite_code", "");
            if (string.IsNullOrEmpty(code)) return;

            Preferences.Remove("pending_invite_code");

            if (_authService.CurrentUser == null) return;

            var (success, error) = await _friendService.AcceptInviteCodeAsync(code);

            if (success)
            {
                var credits = await _settingsService.GetCreditsGainFriendAsync();
                var message = credits > 0
                    ? $"{L.T("Friends_InviteAccepted")}\n\n{string.Format(L.T("Friends_CreditsEarned"), credits)}"
                    : L.T("Friends_InviteAccepted");

                // ⚡ Important : DisplayAlert doit être sur le MainThread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await MainPage!.DisplayAlert(
                        "👥 " + L.T("Friends_NewFriend"),
                        message,
                        L.T("Common_OK"));
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[App] Invite code failed: {error}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] HandlePendingInviteCode: {ex.Message}");
        }
    }

    /// <summary>
    /// ⚡ NOUVEAU : Gère le code de série scanné via QR code TV.
    /// Stocké par MainActivity dans Preferences "pending_series_code".
    /// Appelé au démarrage de l'app (après login) ET via OnNewIntent quand
    /// l'utilisateur scanne le QR alors que l'app est déjà ouverte.
    /// </summary>
    public async Task HandlePendingSeriesCodeAsync()
    {
        try
        {
            var code = Preferences.Get("pending_series_code", "");
            if (string.IsNullOrEmpty(code)) return;

            Preferences.Remove("pending_series_code");

            // Si pas connecté : on garde le code dans Preferences pour
            // le traiter après le login. On le remet.
            if (_authService.CurrentUser == null)
            {
                Preferences.Set("pending_series_code", code);
                System.Diagnostics.Debug.WriteLine(
                    "[App] Series code en attente (user non connecté)");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[App] Traitement series code : {code}");

            // Appel direct au service pour rejoindre + récupérer la série
            var (success, error, series) = await _seriesService.JoinSeriesByCodeAsync(code);

            if (!success || series == null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await MainPage!.DisplayAlert(
                        "Code invalide",
                        error ?? "Impossible de rejoindre cette série.",
                        "OK");
                });
                return;
            }

            // Routage selon le statut de la série (même logique que JoinSeriesViewModel)
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
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
                        await MainPage!.DisplayAlert(
                            "Série terminée",
                            $"La série \"{series.Title}\" est terminée.",
                            "OK");
                    }
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[App] Navigation series code: {navEx.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[App] HandlePendingSeriesCode: {ex.Message}");
        }
    }

    public async Task ShowPromoMessageIfNeededAsync()
    {
        try
        {
            var message = await _promoService.GetActiveUnseenMessageAsync();
            if (message == null) return;

            // ⚡ Important : ShowPopupAsync doit être sur le MainThread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var popup = new Views.Shared.PromoMessagePopup(message.Title, message.Message);
                await MainPage!.ShowPopupAsync(popup);
            });
            _promoService.MarkMessageAsSeen(message.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] ShowPromoMessage: {ex.Message}");
        }
    }
}