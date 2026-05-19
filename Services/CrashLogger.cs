using System.Text;

namespace BeauOuPas.Services;

/// <summary>
/// 🚨 Service de capture des crashes en mode Release.
///
/// En mode Debug attaché à Visual Studio, les exceptions non observées
/// (async qui throw sans await, etc.) sont absorbées par le debugger.
/// En mode Release sans debugger, ces mêmes exceptions font crasher
/// l'app silencieusement, sans aucun feedback à l'utilisateur.
///
/// Cette classe :
/// 1. Branche AppDomain.UnhandledException (exceptions sync non gérées)
/// 2. Branche TaskScheduler.UnobservedTaskException (Tasks async non observées)
/// 3. Sur Android, branche aussi AndroidEnvironment.UnhandledExceptionRaiser
///    (exceptions Java natives qui remontent au runtime)
///
/// Au moment du crash :
/// - Écrit le stack trace complet dans un fichier persistant sur le téléphone
/// - Tente d'afficher une popup à l'utilisateur (best effort, peut échouer si
///   le crash est déjà fatal)
///
/// Fichier de log : FileSystem.AppDataDirectory / crashes.log
/// Sur Android, c'est typiquement :
///   /data/data/fr.beauoupas.app/files/crashes.log
/// (accessible via Android Studio Device File Explorer, ou via une commande
///  Files.OpenRead côté code)
/// </summary>
public static class CrashLogger
{
    private static readonly object _lock = new();
    private static string LogFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "crashes.log");

    public static void Initialize()
    {
        // 1. Exceptions sync non gérées dans le domaine d'app
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogCrash("AppDomain.UnhandledException", ex, e.IsTerminating);
            TryShowAlert(ex);
        };

        // 2. Tasks async non observées (le grand classique du crash MAUI Release)
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception, terminating: false);
            TryShowAlert(e.Exception);
            // ⚠️ NE PAS faire e.SetObserved() : on veut que l'exception remonte
            // pour qu'on voie qu'elle s'est produite.
        };

#if ANDROID
        // 3. Exceptions Java/Kotlin qui remontent au runtime .NET via Xamarin
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, e) =>
        {
            LogCrash("Android.UnhandledExceptionRaiser", e.Exception, terminating: true);
            TryShowAlert(e.Exception);
            // e.Handled = true permettrait de ne pas crasher, mais c'est
            // souvent pire que mieux. On laisse à false pour respecter
            // le comportement standard.
        };
#endif

        LogInfo("CrashLogger initialisé");
    }

    /// <summary>
    /// Logue un crash avec son stack trace complet dans le fichier persistant.
    /// Synchronisé pour éviter les écritures concurrentes en cas de crashes
    /// multiples simultanés.
    /// </summary>
    public static void LogCrash(string source, Exception? ex, bool terminating)
    {
        try
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("═══════════════════════════════════════════════════════");
                sb.AppendLine($"💥 CRASH @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source     : {source}");
                sb.AppendLine($"Terminating: {terminating}");
                sb.AppendLine($"Type       : {ex?.GetType().FullName ?? "(null)"}");
                sb.AppendLine($"Message    : {ex?.Message ?? "(null)"}");
                sb.AppendLine();
                sb.AppendLine("Stack trace :");
                sb.AppendLine(ex?.StackTrace ?? "(no stack trace)");

                // Inner exceptions
                var inner = ex?.InnerException;
                int depth = 1;
                while (inner != null && depth < 5)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- Inner exception #{depth} ---");
                    sb.AppendLine($"Type   : {inner.GetType().FullName}");
                    sb.AppendLine($"Message: {inner.Message}");
                    sb.AppendLine(inner.StackTrace);
                    inner = inner.InnerException;
                    depth++;
                }
                sb.AppendLine("═══════════════════════════════════════════════════════");

                File.AppendAllText(LogFilePath, sb.ToString());
                System.Diagnostics.Debug.WriteLine($"[CrashLogger] Crash logged to {LogFilePath}");
                System.Diagnostics.Debug.WriteLine(sb.ToString());
            }
        }
        catch
        {
            // Si même le logging plante, on ne peut rien faire de plus
        }
    }

    public static void LogInfo(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    /// <summary>
    /// Tente d'afficher une popup à l'utilisateur. Peut échouer silencieusement
    /// si le crash est déjà fatal (pas de MainPage, pas de UI thread, etc.).
    /// </summary>
    private static void TryShowAlert(Exception? ex)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var page = Application.Current?.MainPage;
                    if (page == null) return;

                    // On limite la longueur pour ne pas exploser la popup
                    var msg = ex?.Message ?? "(unknown error)";
                    var stack = ex?.StackTrace ?? string.Empty;
                    if (stack.Length > 800) stack = stack.Substring(0, 800) + "...";

                    await page.DisplayAlert(
                        "💥 Erreur inattendue",
                        $"{ex?.GetType().Name}: {msg}\n\n" +
                        $"Stack:\n{stack}\n\n" +
                        $"Log complet sauvegardé dans :\n{LogFilePath}",
                        "OK");
                }
                catch
                {
                    // best effort
                }
            });
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>
    /// Lit le contenu du fichier de log pour l'afficher (utile pour debug).
    /// </summary>
    public static string ReadLog()
    {
        try
        {
            if (!File.Exists(LogFilePath)) return "(no log file yet)";
            return File.ReadAllText(LogFilePath);
        }
        catch (Exception ex)
        {
            return $"(error reading log: {ex.Message})";
        }
    }

    /// <summary>
    /// Vide le fichier de log.
    /// </summary>
    public static void ClearLog()
    {
        try
        {
            if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
        }
        catch { }
    }
}
