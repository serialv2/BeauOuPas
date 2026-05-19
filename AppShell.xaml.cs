using BeauOuPas.Services;
using BeauOuPas.Views.Auth;
using BeauOuPas.Views.Credits;
using BeauOuPas.Views.Dating;
using BeauOuPas.Views.Friends;
using BeauOuPas.Views.Groups;
using BeauOuPas.Views.Home;
using BeauOuPas.Views.Profile;
using BeauOuPas.Views.Projects;
using BeauOuPas.Views.Shared;
using BeauOuPas.Views.Vote;

namespace BeauOuPas;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;
    private static bool _routesRegistered = false;

    public AppShell(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;

        if (!_routesRegistered)
        {
            RegisterRoutes();
            _routesRegistered = true;
        }
    }

    // ⚡ MAI 2026 — FIX P2 v4 : appelée depuis le ShellRenderer Android
    // (Platforms/Android/AppShellRenderer.cs) quand l'utilisateur retap
    // l'onglet Home alors qu'il est déjà sur cet onglet. Reset la stack
    // à la racine.
    public async Task ForceGoHomeAsync()
    {
        try
        {
            await GoToAsync("//HomePage");
            System.Diagnostics.Debug.WriteLine("[Shell] ForceGoHome OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shell] ForceGoHome error: {ex.Message}");
        }
    }

    private void RegisterRoutes()
    {
        // ─── Auth ─────────────────────────────────────────────────
        Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
        Routing.RegisterRoute("CompleteProfilePage", typeof(CompleteProfilePage));

        // ─── Pages retirées de la TabBar (mais navigables) ────────
        // ⚡ MyProjectsPage et GroupsPage ne sont plus dans la TabBar.
        // FriendsPage est maintenant DANS la TabBar (a remplacé GroupsPage).
        Routing.RegisterRoute("MyProjectsPage", typeof(MyProjectsPage));
        Routing.RegisterRoute("GroupsPage", typeof(GroupsPage));

        Routing.RegisterRoute("ProjectDetailPage", typeof(ProjectDetailPage));
        Routing.RegisterRoute("PhotoVotePage", typeof(PhotoVotePage));
        Routing.RegisterRoute("DuelVotePage", typeof(DuelVotePage));
        Routing.RegisterRoute("ProfileSettingsPage", typeof(ProfileSettingsPage));
        Routing.RegisterRoute("CreditsPage", typeof(CreditsPage));
        Routing.RegisterRoute("MatchesPage", typeof(MatchesPage));
        Routing.RegisterRoute("ChatPage", typeof(ChatPage));
        Routing.RegisterRoute("FeedbackPage", typeof(FeedbackPage));
        Routing.RegisterRoute("FriendProfileDetail", typeof(FriendProfilePage));

        // ─── Groupes & Séries ─────────────────────────────────────
        Routing.RegisterRoute("CreateGroupPage", typeof(CreateGroupPage));
        Routing.RegisterRoute("GroupDetailPage", typeof(GroupDetailPage));
        Routing.RegisterRoute("CreateSeriesPage", typeof(CreateSeriesPage));
        Routing.RegisterRoute("SeriesDetailPage", typeof(SeriesDetailPage));
        Routing.RegisterRoute("SeriesVotePage", typeof(SeriesVotePage));
        Routing.RegisterRoute("TemplatesPage", typeof(TemplatesPage));
        Routing.RegisterRoute("CreateSeriesProjectPage", typeof(CreateSeriesProjectPage));
        Routing.RegisterRoute("JoinSeriesPage", typeof(JoinSeriesPage));
        Routing.RegisterRoute("SeriesResultsPage", typeof(SeriesResultsPage));
        Routing.RegisterRoute("CreateSeriesTypePage", typeof(CreateSeriesTypePage));
        Routing.RegisterRoute("EditQuizQuestionPage", typeof(EditQuizQuestionPage));
        Routing.RegisterRoute("EditQuizQuestionsPage", typeof(Views.Groups.EditQuizQuestionsPage));
        Routing.RegisterRoute("CreatePartyPage", typeof(CreatePartyPage));
        Routing.RegisterRoute("CreateQuizPage", typeof(CreateQuizPage));
        Routing.RegisterRoute("QuizPlayPage", typeof(QuizPlayPage));
        // 🎉 PARTIE RAPIDE : écran de vote joueur (Most Likely To).
        // Indispensable : sans cette ligne, GoToAsync("PartyPlayPage")
        // lève "unable to figure out route for: PartyPlayPage".
        Routing.RegisterRoute("PartyPlayPage", typeof(PartyPlayPage));
        // ─── Invitations ──────────────────────────────────────────
        Routing.RegisterRoute("InviteToSeriesPage", typeof(InviteToSeriesPage));
        Routing.RegisterRoute("MyInvitationsPage", typeof(MyInvitationsPage));

        // ─── ⚡ NOUVEAU : flow de création depuis HomePage ────────
        Routing.RegisterRoute("CreateContextPage", typeof(CreateContextPage));
        Routing.RegisterRoute("PickGroupForCreationPage", typeof(PickGroupForCreationPage));
    }

    public void GoToAuth()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                MainTabBar.IsVisible = false;
                await GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shell] GoToAuth: {ex.Message}");
            }
        });
    }

    public void GoToMainApp()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                MainTabBar.IsVisible = true;
                CurrentItem = MainTabBar;
                // ⚡ Arrivée sur HomePage (vrai accueil) au lieu de DashboardPage.
                await GoToAsync("//HomePage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shell] GoToMainApp: {ex.Message}");
            }
        });
    }
}
