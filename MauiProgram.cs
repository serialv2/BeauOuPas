using BeauOuPas.Services;
using BeauOuPas.Services.Ads;
using BeauOuPas.ViewModels.Auth;
using BeauOuPas.ViewModels.Credits;
using BeauOuPas.ViewModels.Dashboard;
using BeauOuPas.ViewModels.Dating;
using BeauOuPas.ViewModels.Friends;
using BeauOuPas.ViewModels.Groups;
using BeauOuPas.ViewModels.Home;
using BeauOuPas.ViewModels.Profile;
using BeauOuPas.ViewModels.Projects;
using BeauOuPas.ViewModels.Vote;
using BeauOuPas.Views.Auth;
using BeauOuPas.Views.Credits;
using BeauOuPas.Views.Dashboard;
using BeauOuPas.Views.Dating;
using BeauOuPas.Views.Friends;
using BeauOuPas.Views.Groups;
using BeauOuPas.Views.Home;
using BeauOuPas.Views.Profile;
using BeauOuPas.Views.Projects;
using BeauOuPas.Views.Shared;
using BeauOuPas.Views.Vote;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using BeauOuPas.ViewModels;

namespace BeauOuPas;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CrashLogger.Initialize();

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        // ⚡ MAI 2026 — FIX P2 v4 : ShellRenderer Android custom pour
        // intercepter le retap de l'onglet Home (cas non géré par MAUI Shell).
        // Voir Platforms/Android/AppShellRenderer.cs
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(AppShell),
                typeof(BeauOuPas.Platforms.Android.AppShellRenderer));
        });
#endif

        var supabase = new Supabase.Client(
            Constants.SupabaseUrl,
            Constants.SupabaseKey,
            new Supabase.SupabaseOptions { AutoRefreshToken = true });

        builder.Services.AddSingleton(supabase);

        // ─── Services ─────────────────────────────────────────────
        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<CreditService>();
        builder.Services.AddSingleton<ProjectService>();
        builder.Services.AddSingleton<PhotoUploadService>();
        builder.Services.AddSingleton<ImageService>();
        builder.Services.AddSingleton<CropService>();
        builder.Services.AddSingleton<NetworkService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<PromoMessageService>();
        builder.Services.AddSingleton<ChatService>();
        builder.Services.AddSingleton<FriendService>();
        builder.Services.AddSingleton<VoteService>();
        builder.Services.AddSingleton<PresenceService>();
        builder.Services.AddSingleton<AvatarService>();
        builder.Services.AddSingleton<ReportService>();
        builder.Services.AddSingleton<FeedbackService>();
        builder.Services.AddSingleton<NavigationStateService>();
        builder.Services.AddSingleton<GroupService>();
        builder.Services.AddSingleton<SeriesService>();
        builder.Services.AddSingleton<SeriesVoteService>();
        builder.Services.AddSingleton<SeriesInvitationService>();
        builder.Services.AddTransient<SeriesRealtimeService>();
        builder.Services.AddSingleton<QuizService>();
        builder.Services.AddTransient<QuizPlayViewModel>();
        builder.Services.AddTransient<QuizPlayPage>();
        builder.Services.AddTransient<CreateSeriesTypeViewModel>();
        builder.Services.AddTransient<CreateSeriesTypePage>();
        builder.Services.AddSingleton<PartyService>();
        builder.Services.AddTransient<PartyPlayViewModel>();
        builder.Services.AddTransient<PartyPlayPage>();
        builder.Services.AddTransient<CreatePartyViewModel>();
        builder.Services.AddSingleton<PartyService>();
        builder.Services.AddTransient<CreatePartyPage>();
        //builder.Services.AddTransient<CreatePartyPage>();   // une fois la page XAML créée
        // ─── Ads ──────────────────────────────────────────────────
#if ANDROID
        builder.Services.AddSingleton<IAdService, AndroidAdService>();
#else
        builder.Services.AddSingleton<IAdService, MockAdService>();
#endif

        // ─── ViewModels ───────────────────────────────────────────
        builder.Services.AddTransient<CompleteProfileViewModel>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<RegisterViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddTransient<CreateContextViewModel>();
        builder.Services.AddTransient<PickGroupForCreationViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<VoteFeedViewModel>();
        builder.Services.AddSingleton<CreateProjectViewModel>();
        builder.Services.AddSingleton<MyProjectsViewModel>();
        builder.Services.AddTransient<ProfileSettingsViewModel>();
        builder.Services.AddSingleton<CreditsViewModel>();
        builder.Services.AddSingleton<MatchesViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddSingleton<FriendsViewModel>();
        builder.Services.AddTransient<FriendProfileViewModel>();
        builder.Services.AddTransient<FeedbackViewModel>();
        builder.Services.AddSingleton<ProjectDetailViewModel>();
        // ─── Groupes & Séries ViewModels ──────────────────────────
        builder.Services.AddSingleton<GroupsViewModel>();
        builder.Services.AddTransient<GroupDetailViewModel>();
        builder.Services.AddTransient<CreateGroupViewModel>();
        builder.Services.AddTransient<SeriesDetailViewModel>();
        builder.Services.AddTransient<CreateSeriesViewModel>();
        builder.Services.AddTransient<SeriesVoteViewModel>();
        builder.Services.AddTransient<TemplatesViewModel>();
        builder.Services.AddTransient<CreateSeriesProjectViewModel>();
        builder.Services.AddTransient<JoinSeriesViewModel>();
        builder.Services.AddTransient<SeriesResultsViewModel>();
        builder.Services.AddTransient<InviteToSeriesViewModel>();
        builder.Services.AddTransient<MyInvitationsViewModel>();
        builder.Services.AddTransient<CreateQuizViewModel>();
        builder.Services.AddTransient<CreateQuizPage>();
        builder.Services.AddTransient<EditQuizQuestionViewModel>();
        builder.Services.AddTransient<EditQuizQuestionPage>();
        builder.Services.AddTransient<EditQuizQuestionsViewModel>();
        builder.Services.AddTransient<Views.Groups.EditQuizQuestionsPage>();
        // ─── Views ────────────────────────────────────────────────
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<CompleteProfilePage>();
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<RegisterPage>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddTransient<CreateContextPage>();
        builder.Services.AddTransient<PickGroupForCreationPage>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<VoteFeedPage>();
        builder.Services.AddSingleton<CreateProjectPage>();
        builder.Services.AddSingleton<MyProjectsPage>();
        builder.Services.AddTransient<ProfileSettingsPage>();
        builder.Services.AddSingleton<CreditsPage>();
        builder.Services.AddSingleton<MatchesPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddSingleton<FriendsPage>();
        builder.Services.AddTransient<FriendProfilePage>();
        builder.Services.AddTransient<FeedbackPage>();
        builder.Services.AddSingleton<ProjectDetailPage>();
        // ─── Groupes & Séries Views ───────────────────────────────
        builder.Services.AddSingleton<GroupsPage>();
        builder.Services.AddTransient<GroupDetailPage>();
        builder.Services.AddTransient<CreateGroupPage>();
        builder.Services.AddTransient<SeriesDetailPage>();
        builder.Services.AddTransient<CreateSeriesPage>();
        builder.Services.AddTransient<SeriesVotePage>();
        builder.Services.AddTransient<TemplatesPage>();
        builder.Services.AddTransient<CreateSeriesProjectPage>();
        builder.Services.AddTransient<JoinSeriesPage>();
        builder.Services.AddTransient<SeriesResultsPage>();
        builder.Services.AddTransient<InviteToSeriesPage>();
        builder.Services.AddTransient<MyInvitationsPage>();

        builder.Services.AddSingleton<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}