using Supabase;
using Supabase.Gotrue;
using Postgrest;
using BeauOuPas.Models;
using Newtonsoft.Json;
using System.Globalization;
namespace BeauOuPas.Services;

public class AuthService
{
    private readonly Supabase.Client _supabase;
    private const string SessionKey = "supabase_session";

    public Supabase.Gotrue.User? CurrentUser => _supabase.Auth.CurrentUser;
    public bool IsLoggedIn => _supabase.Auth.CurrentUser != null;
    public Profile? CurrentProfile { get; private set; }

    public AuthService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }
    public async Task<Profile?> GetProfileByIdAsync(string userId)
    {
        try
        {
            return await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Single();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] GetProfileByIdAsync: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// ⚡ NOUVEAU : Vérifie si un username est disponible (pas déjà pris).
    /// Comparaison case-insensitive (PostgreSQL ilike) pour éviter les
    /// doublons "Toto" / "toto" / "TOTO".
    ///
    /// <param name="username">Le pseudo à tester (sera trim).</param>
    /// <param name="excludeUserId">
    /// Optionnel : ignore ce userId dans la recherche. Sert quand un user
    /// modifie son propre profil et qu'il garde le même pseudo (ne pas
    /// flaguer son propre profil comme conflit).
    /// </param>
    /// <returns>true si dispo, false si déjà pris (ou en cas d'erreur, on
    /// considère dispo = true pour ne pas bloquer l'utilisateur).</returns>
    /// </summary>
    public async Task<bool> IsUsernameAvailableAsync(string username, string? excludeUserId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            var trimmed = username.Trim();

            // ilike = case-insensitive exact match (pas de wildcard ici car pas de %)
            var query = _supabase.From<Profile>()
                .Filter("username", Postgrest.Constants.Operator.ILike, trimmed);

            var result = await query.Get();

            if (result?.Models == null || result.Models.Count == 0)
                return true;

            // Si on est en train d'éditer son propre profil avec le même pseudo,
            // on ne doit pas considérer ça comme un conflit.
            if (!string.IsNullOrEmpty(excludeUserId))
            {
                // Disponible si tous les matches sont MOI
                return result.Models.All(p => p.Id == excludeUserId);
            }

            return false;
        }
        catch (Exception ex)
        {
            // En cas d'erreur réseau, on laisse passer (fail-open).
            // L'unicité au pire sera rattrapée par la contrainte BDD si elle existe.
            System.Diagnostics.Debug.WriteLine($"[AuthService] IsUsernameAvailable: {ex.Message}");
            return true;
        }
    }
    public async Task EnsureProfileExistsAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var profile = await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Single();

            if (profile != null)
            {
                CurrentProfile = profile;
                return;
            }

            var user = _supabase.Auth.CurrentUser;

            string username =
                user?.UserMetadata != null &&
                user.UserMetadata.TryGetValue("username", out var usernameValue) &&
                usernameValue != null
                    ? usernameValue.ToString() ?? $"user_{userId[..8]}"
                    : (!string.IsNullOrWhiteSpace(user?.Email)
                        ? user.Email.Split('@')[0]
                        : $"user_{userId[..8]}");

            DateTime birthDate = DateTime.Today.AddYears(-18);
            if (user?.UserMetadata != null &&
                user.UserMetadata.TryGetValue("birth_date", out var birthDateValue) &&
                birthDateValue != null &&
                DateTime.TryParse(birthDateValue.ToString(), out var parsedBirthDate))
            {
                birthDate = parsedBirthDate;
            }

            string? gender = null;
            if (user?.UserMetadata != null &&
                user.UserMetadata.TryGetValue("gender", out var genderValue) &&
                genderValue != null)
            {
                gender = genderValue.ToString();
            }

            string? phone = null;
            if (user?.UserMetadata != null &&
                user.UserMetadata.TryGetValue("phone", out var phoneValue) &&
                phoneValue != null)
            {
                phone = phoneValue.ToString();
            }

            var newProfile = new Profile
            {
                Id = userId,
                Username = username,
                BirthDate = birthDate.ToString("yyyy-MM-dd"),
                Gender = gender,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Banned = false
            };

            await _supabase.From<Profile>().Insert(newProfile);
            CurrentProfile = newProfile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] EnsureProfileExistsAsync: {ex}");
        }
    }
    public class LoginResult
    {
        public bool Success { get; set; }
        public bool IsBanned { get; set; }
        public string? BannedReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public async Task<bool> HasValidSessionAsync()
    {
        try
        {
            var json = Preferences.Default.Get(SessionKey, string.Empty);

            if (!string.IsNullOrEmpty(json))
            {
                var session = JsonConvert.DeserializeObject<Supabase.Gotrue.Session>(json);
                if (session?.AccessToken != null && session.RefreshToken != null)
                    await _supabase.Auth.SetSession(session.AccessToken, session.RefreshToken);
            }

            var currentSession = _supabase.Auth.CurrentSession;
            if (currentSession == null)
                return false;

            if (currentSession.ExpiresAt() > DateTime.UtcNow)
                return true;

            var refreshed = await _supabase.Auth.RetrieveSessionAsync();
            return refreshed != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LoginResult> CheckSessionBanAsync()
    {
        try
        {
            var user = _supabase.Auth.CurrentUser;
            if (user == null)
                return new LoginResult { Success = false, ErrorMessage = "Aucune session active." };

            var profile = await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, user.Id!)
                .Single();

            if (profile == null)
            {
                await SignOutAsync();
                return new LoginResult { Success = false, ErrorMessage = "Profil introuvable." };
            }

            if (profile.Banned)
            {
                await SignOutAsync();
                CurrentProfile = null;

                return new LoginResult
                {
                    Success = false,
                    IsBanned = true,
                    BannedReason = profile.BannedReason,
                    BannedAt = profile.BannedAt
                };
            }

            CurrentProfile = profile;
            _ = SaveLanguageAsync(user.Id!);
            return new LoginResult { Success = true };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] CheckSessionBanAsync: {ex}");
            return new LoginResult { Success = false, ErrorMessage = "Erreur de vérification session." };
        }
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var session = await _supabase.Auth.SignIn(email, password);

            if (session?.User == null)
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Email ou mot de passe incorrect."
                };

            return await PostLoginCheckAsync(session);
        }
        catch (Exception ex)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = GetFriendlyError(ex.Message)
            };
        }
    }

    public async Task<(bool Success, string ErrorMessage)> LoginWithGoogleAsync()
    {
        try
        {
            var url = await _supabase.Auth.SignIn(
                Supabase.Gotrue.Constants.Provider.Google,
                new Supabase.Gotrue.SignInOptions
                {
                    RedirectTo = "beauoupas://callback"
                });

            if (url == null)
                return (false, "Impossible d'ouvrir la connexion Google.");

            await Browser.Default.OpenAsync(url.Uri, BrowserLaunchMode.SystemPreferred);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, GetFriendlyError(ex.Message));
        }
    }

    public async Task<(bool Success, string ErrorMessage)> LoginWithAppleAsync()
    {
        try
        {
            var url = await _supabase.Auth.SignIn(
                Supabase.Gotrue.Constants.Provider.Apple,
                new Supabase.Gotrue.SignInOptions
                {
                    RedirectTo = "beauoupas://callback"
                });

            if (url == null)
                return (false, "Impossible d'ouvrir la connexion Apple.");

            await Browser.Default.OpenAsync(url.Uri, BrowserLaunchMode.SystemPreferred);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, GetFriendlyError(ex.Message));
        }
    }

    public async Task<LoginResult> SetSessionAsync(string accessToken, string refreshToken)
    {
        try
        {
            await _supabase.Auth.SetSession(accessToken, refreshToken);

            var session = _supabase.Auth.CurrentSession;
            if (session != null)
            {
                var json = JsonConvert.SerializeObject(session);
                Preferences.Default.Set(SessionKey, json);
            }

            return await CheckSessionBanAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] SetSessionAsync: {ex}");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Erreur lors de l'établissement de la session."
            };
        }
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterAsync(
        string email,
        string password,
        string username,
        DateOnly birthDate,
        string gender,
        string? phone = null)
    {
        try
        {
            if (!IsOldEnough(birthDate))
                return (false, $"Vous devez avoir au moins {Constants.AgeMinimum} ans.");

            // ⚡ Vérifier l'unicité du username AVANT de créer le compte auth.
            // Sinon on aurait un compte auth orphelin avec un pseudo en conflit.
            var available = await IsUsernameAvailableAsync(username);
            if (!available)
                return (false, BeauOuPas.Localization.L.T("Common_UsernameTaken"));

            var metadata = new Dictionary<string, object>
            {
                { "username", username },
                { "birth_date", birthDate.ToString("yyyy-MM-dd") },
                { "gender", gender }
            };

            if (!string.IsNullOrWhiteSpace(phone))
                metadata["phone"] = phone;

            var session = await _supabase.Auth.SignUp(
                email,
                password,
                new Supabase.Gotrue.SignUpOptions
                {
                    Data = metadata,
                    // ⚡ Après que l'utilisateur clique sur le lien de confirmation
                    // dans son mail, Supabase valide l'email en BDD puis redirige
                    // vers cette URL avec les tokens dans le hash.
                    // La page email-confirmed.html prend le relais pour ouvrir
                    // l'app via deep link beauoupas://email-confirmed
                    RedirectTo = "https://serialv2.github.io/beauoupas-web/email-confirmed.html"
                });

            if (session?.User == null)
                return (false, "Erreur lors de la création du compte.");

            try
            {
                await _supabase.Auth.SignOut();
            }
            catch
            {
            }

            Preferences.Default.Remove(SessionKey);
            CurrentProfile = null;

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REGISTER ERROR = " + ex);
            return (false, GetFriendlyError(ex.Message));
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(string email)
    {
        try
        {
            await _supabase.Auth.ResetPasswordForEmail(email);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, GetFriendlyError(ex.Message));
        }
    }

    public async Task<Profile?> GetCurrentProfileAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null)
                return null;

            if (CurrentProfile != null && CurrentProfile.Id == userId)
                return CurrentProfile;

            CurrentProfile = await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Single();

            return CurrentProfile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] GetCurrentProfileAsync: {ex}");
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _supabase.Auth.SignOut();
            Preferences.Default.Remove(SessionKey);
            CurrentProfile = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] SignOutAsync: {ex}");
        }
    }

    public async Task LogoutAsync() => await SignOutAsync();

    // ─── Suppression définitive du compte (RGPD + Google Play Store) ──
    // Appelle la RPC SQL `delete_my_account()` qui supprime DÉFINITIVEMENT
    // toutes les données associées à l'utilisateur connecté dans toute la
    // base, puis supprime le compte auth.users lui-même.
    //
    // ⚠️ IRRÉVERSIBLE : aucune restauration possible après appel.
    //
    // Retourne (success, errorMessage)
    public async Task<(bool Success, string Error)> DeleteAccountAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (string.IsNullOrEmpty(userId))
                return (false, "Vous devez être connecté pour supprimer votre compte.");

            // Appel de la RPC SQL qui fait le ménage côté serveur
            var response = await _supabase.Rpc("delete_my_account", null);

            // La RPC retourne un JSONB : { success: bool, error?: string }
            var json = response.Content;
            if (string.IsNullOrEmpty(json))
                return (false, "Réponse vide du serveur.");

            // Parser le résultat
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<DeleteAccountResult>(json);
            if (result == null)
                return (false, "Réponse invalide du serveur.");

            if (!result.Success)
                return (false, result.Error ?? "Erreur inconnue lors de la suppression.");

            // Suppression OK → nettoyer la session locale
            await SignOutAsync();

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] DeleteAccountAsync: {ex}");
            return (false, $"Erreur : {ex.Message}");
        }
    }

    private class DeleteAccountResult
    {
        [Newtonsoft.Json.JsonProperty("success")]
        public bool Success { get; set; }

        [Newtonsoft.Json.JsonProperty("error")]
        public string? Error { get; set; }
    }

    private async Task<LoginResult> PostLoginCheckAsync(Supabase.Gotrue.Session session)
    {
        try
        {
            var json = JsonConvert.SerializeObject(session);
            Preferences.Default.Set(SessionKey, json);

            var profile = await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, session.User!.Id!)
                .Single();

            if (profile == null)
            {
                await SignOutAsync();
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Profil introuvable. Contactez le support."
                };
            }

            if (profile.Banned)
            {
                await SignOutAsync();
                CurrentProfile = null;

                return new LoginResult
                {
                    Success = false,
                    IsBanned = true,
                    BannedReason = profile.BannedReason,
                    BannedAt = profile.BannedAt
                };
            }

            CurrentProfile = profile;
            _ = SaveLanguageAsync(session.User!.Id!);
            return new LoginResult { Success = true };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] PostLoginCheckAsync: {ex}");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Erreur lors de la vérification du profil."
            };
        }
    }

    private static bool IsOldEnough(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthDate.Year;
        if (birthDate.AddYears(age) > today)
            age--;

        return age >= Constants.AgeMinimum;
    }

    private static string GetFriendlyError(string message) => message switch
    {
        var m when m.Contains("Invalid login credentials") => "Email ou mot de passe incorrect.",
        var m when m.Contains("Email not confirmed") => "Confirmez votre email avant de vous connecter.",
        var m when m.Contains("User already registered") => "Un compte existe déjà avec cet email.",
        var m when m.Contains("signup_disabled") => "Les inscriptions sont actuellement désactivées dans Supabase.",
        var m when m.Contains("Signups not allowed for this instance") => "Les inscriptions sont actuellement désactivées dans Supabase.",
        var m when m.Contains("Network") => "Vérifiez votre connexion internet.",
        _ => "Une erreur est survenue. Réessayez."

    };
    private async Task SaveLanguageAsync(string userId)
    {
        try
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; // "fr", "en", "de"...

            // S'assurer que c'est une langue supportée
            var supported = new[] { "fr", "en", "de", "es", "it", "pt" };
            if (!supported.Contains(lang)) lang = "en";

            await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Set(x => x.Language, lang)
                .Update();

            System.Diagnostics.Debug.WriteLine($"[AuthService] Language saved: {lang}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] SaveLanguageAsync: {ex.Message}");
        }
    }
}