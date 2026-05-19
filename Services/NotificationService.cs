namespace BeauOuPas.Services;

// ═══════════════════════════════════════════════════════════════════
// NotificationService — version SANS Plugin.Firebase
// -------------------------------------------------------------------
// Le package Plugin.Firebase.* est désactivé dans le csproj (mur de
// compilation iOS + Android). Ce service garde sa structure (DI,
// signature InitializeAsync) pour ne RIEN casser côté MauiProgram.cs
// ni chez les appelants, mais ne fait plus d'appel Firebase.
//
// Ce qui est conservé (utile, ne dépend pas de Firebase) :
//   - demande de permission notifications Android 13+
//   - création du canal de notification Android 8+
//
// Ce qui est neutralisé (dépendait de Firebase) :
//   - récupération du token FCM
//   - écoute TokenChanged / NotificationReceived
//   - upsert du token dans Supabase
//
// → À réintégrer plus tard via une stratégie push native dédiée.
//   Non bloquant pour la publication iOS/Android.
// ═══════════════════════════════════════════════════════════════════
public class NotificationService
{
    private readonly Supabase.Client _supabase;

    public NotificationService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    // ─── Init notifications (sans Firebase) ──────────────────────────
    public async Task InitializeAsync()
    {
        try
        {
            // Android 13+ : demande la permission POST_NOTIFICATIONS
            await RequestNotificationPermissionAsync();

            // Crée le canal de notification Android (obligatoire Android 8+)
            CreateNotificationChannel();

            System.Diagnostics.Debug.WriteLine(
                "=== NotificationService: init OK (push Firebase désactivé) ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification init error: {ex.Message}");
        }
    }

    // ─── Demande permission Android 13+ ─────────────────────────────
    private async Task RequestNotificationPermissionAsync()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                System.Diagnostics.Debug.WriteLine(
                    $"=== NOTIFICATION PERMISSION: {status} ===");
            }
        }
#else
        await Task.CompletedTask;
#endif
    }

    // ─── Créer le canal Android 8+ ──────────────────────────────────
    private void CreateNotificationChannel()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new Android.App.NotificationChannel(
                "beauoupas_default",
                "BeauOuPas",
                Android.App.NotificationImportance.High)
            {
                Description = "Notifications BeauOuPas"
            };

            var manager = Android.App.Application.Context
                .GetSystemService(Android.Content.Context.NotificationService)
                as Android.App.NotificationManager;

            manager?.CreateNotificationChannel(channel);
            System.Diagnostics.Debug.WriteLine("=== NOTIFICATION CHANNEL CREATED ===");
        }
#endif
    }

    // ─── Sauvegarder un token push (réservé usage futur) ─────────────
    // Conservé pour ne pas casser d'éventuels appelants ; inactif tant
    // qu'aucune source de token (FCM/APNs natif) n'est branchée.
    private async Task SaveTokenToSupabaseAsync(string token)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "=== Notification: No user logged in, token not saved ===");
                return;
            }

            await _supabase.Rpc("upsert_fcm_token",
                new Dictionary<string, object>
                {
                    { "p_user_id", userId },
                    { "p_token",   token  }
                });

            System.Diagnostics.Debug.WriteLine("=== Notification TOKEN SAVED TO SUPABASE ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveToken error: {ex.Message}");
        }
    }
}