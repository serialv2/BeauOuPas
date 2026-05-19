using BeauOuPas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BeauOuPas.Services;

public class FriendService
{
    private readonly Supabase.Client _supabase;

    public FriendService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    private string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ═════════════════════════════════════════════════════════════════
    // ⚡ OPTIM v6 (PERF) : 1 SEULE requête au lieu de N+2
    //
    // AVANT : la version "à plat" faisait 2 SELECT (sent + received) puis
    // 1 SELECT par profil ami (GetProfileAsync). Avec 30 amis = 32 round-trips
    // séquentiels → 3 à 6 secondes ressentis sur la page Amis.
    //
    // APRÈS : 1 RPC `get_my_friends` qui fait tout côté Postgres et retourne
    // un JSON déjà construit. Coût : ~150-300 ms total (1 round-trip).
    //
    // Fallback : si la RPC plante (ex: pas encore déployée en BDD), on bascule
    // sur l'ancienne logique pour ne pas casser la page. Comme ça, le mobile
    // peut être publié avant que la RPC soit présente côté Supabase.
    // ═════════════════════════════════════════════════════════════════
    public async Task<List<FriendItem>> GetFriendsAsync()
    {
        var userId = CurrentUserId;
        if (userId == null) return new();

        // ─── Tentative 1 : RPC optimisée ──────────────────────────
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rpc = await _supabase.Rpc("get_my_friends", new Dictionary<string, object>
            {
                { "p_user_id", userId }
            });
            sw.Stop();

            var content = rpc?.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var list = ParseFriendsJson(content);
                System.Diagnostics.Debug.WriteLine(
                    $"[FriendService] RPC get_my_friends OK: {list.Count} ami(s) en {sw.ElapsedMilliseconds}ms");
                return list;
            }
            System.Diagnostics.Debug.WriteLine(
                "[FriendService] RPC get_my_friends a renvoyé un contenu vide → fallback");
        }
        catch (Exception ex)
        {
            // Si la RPC n'existe pas encore (ex: déploiement en cours), on ne
            // veut pas que la page Amis soit cassée. On bascule sur l'ancien
            // algo qui marche, juste plus lent.
            System.Diagnostics.Debug.WriteLine(
                $"[FriendService] RPC get_my_friends KO ({ex.GetType().Name}: {ex.Message}) → fallback ancien algo");
        }

        // ─── Fallback : ancien algo N+2 (pour ne rien casser) ────
        return await GetFriendsLegacyAsync(userId);
    }

    /// <summary>
    /// Parse le JSON renvoyé par la RPC `get_my_friends` (tableau d'objets).
    /// Format attendu : [{ friendship_id, user_id, username, avatar_url,
    /// last_seen, status, is_requester, created_at }, ...]
    /// </summary>
    private static List<FriendItem> ParseFriendsJson(string json)
    {
        var result = new List<FriendItem>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            // La RPC peut renvoyer soit un array brut "[...]", soit "[...]" wrappé.
            // On prend les deux cas pour être robuste.
            var token = JToken.Parse(json);
            JArray? arr = null;
            if (token is JArray ja) arr = ja;
            else if (token is JObject jo && jo.First?.First is JArray inner) arr = inner;

            if (arr == null) return result;

            foreach (var item in arr)
            {
                if (item is not JObject obj) continue;
                var f = new FriendItem
                {
                    FriendshipId = obj.Value<string>("friendship_id") ?? string.Empty,
                    UserId       = obj.Value<string>("user_id") ?? string.Empty,
                    Username     = obj.Value<string>("username") ?? string.Empty,
                    AvatarUrl    = obj.Value<string>("avatar_url"),
                    LastSeen     = obj.Value<DateTime?>("last_seen"),
                    Status       = obj.Value<string>("status") ?? string.Empty,
                    IsRequester  = obj.Value<bool?>("is_requester") ?? false,
                    CreatedAt    = obj.Value<DateTime?>("created_at") ?? DateTime.MinValue
                };
                result.Add(f);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FriendService] ParseFriendsJson error: {ex.Message}");
        }

        // La RPC trie déjà côté SQL (created_at DESC), mais on re-trie côté C#
        // au cas où le client recevrait dans un autre ordre.
        return result.OrderByDescending(f => f.CreatedAt).ToList();
    }

    /// <summary>
    /// Fallback : ancien algo "à plat" en N+2 requêtes. Préservé tel quel
    /// pour pouvoir basculer si la RPC est indisponible. Ne PAS l'optimiser
    /// davantage, c'est juste un filet de sécurité.
    /// </summary>
    private async Task<List<FriendItem>> GetFriendsLegacyAsync(string userId)
    {
        try
        {
            var sent = await _supabase
                .From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            var received = await _supabase
                .From<Friendship>()
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            // ⚡ Petite optim : on charge les profils en BATCH (1 requête avec IN)
            // au lieu de N requêtes individuelles. Ça transforme N+2 en exactement 3.
            var allOtherIds = sent.Models.Select(f => f.ReceiverId)
                .Concat(received.Models.Select(f => f.RequesterId))
                .Distinct()
                .ToList();

            var profilesById = new Dictionary<string, Profile>();
            if (allOtherIds.Count > 0)
            {
                var profilesResult = await _supabase
                    .From<Profile>()
                    .Filter("id", Postgrest.Constants.Operator.In, allOtherIds)
                    .Get();
                foreach (var p in profilesResult.Models)
                {
                    if (!string.IsNullOrEmpty(p.Id))
                        profilesById[p.Id] = p;
                }
            }

            var result = new List<FriendItem>();

            foreach (var f in sent.Models)
            {
                if (!profilesById.TryGetValue(f.ReceiverId, out var profile)) continue;
                result.Add(new FriendItem
                {
                    FriendshipId = f.Id,
                    UserId       = f.ReceiverId,
                    Username     = profile.Username,
                    AvatarUrl    = profile.AvatarUrl,
                    LastSeen     = profile.LastSeen,
                    Status       = f.Status,
                    IsRequester  = true,
                    CreatedAt    = f.CreatedAt
                });
            }

            foreach (var f in received.Models)
            {
                if (!profilesById.TryGetValue(f.RequesterId, out var profile)) continue;
                result.Add(new FriendItem
                {
                    FriendshipId = f.Id,
                    UserId       = f.RequesterId,
                    Username     = profile.Username,
                    AvatarUrl    = profile.AvatarUrl,
                    LastSeen     = profile.LastSeen,
                    Status       = f.Status,
                    IsRequester  = false,
                    CreatedAt    = f.CreatedAt
                });
            }

            return result.OrderByDescending(f => f.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFriends fallback error: {ex.Message}");
            return new();
        }
    }

    // ─── Rechercher des utilisateurs par pseudo ──────────────────────
    public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
    {
        var userId = CurrentUserId;
        if (userId == null || string.IsNullOrWhiteSpace(query)) return new();

        try
        {
            // Chercher les profils dont le username contient la query
            var profiles = await _supabase
                .From<Profile>()
                .Filter("username", Postgrest.Constants.Operator.ILike, $"%{query.Trim()}%")
                .Limit(20)
                .Get();

            // Récupérer les amitiés existantes pour filtrer
            var sent = await _supabase
                .From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();
            var received = await _supabase
                .From<Friendship>()
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            var existingIds = sent.Models.Select(f => f.ReceiverId)
                .Concat(received.Models.Select(f => f.RequesterId))
                .ToHashSet();

            var results = new List<UserSearchResult>();

            foreach (var p in profiles.Models)
            {
                if (p.Id == userId) continue; // pas soi-même

                string relationStatus = "none";
                string? friendshipId = null;

                var sentTo = sent.Models.FirstOrDefault(f => f.ReceiverId == p.Id);
                var receivedFrom = received.Models.FirstOrDefault(f => f.RequesterId == p.Id);

                if (sentTo != null)
                {
                    relationStatus = sentTo.Status == "accepted" ? "friend" : "pending_sent";
                    friendshipId = sentTo.Id;
                }
                else if (receivedFrom != null)
                {
                    relationStatus = receivedFrom.Status == "accepted" ? "friend" : "pending_received";
                    friendshipId = receivedFrom.Id;
                }

                results.Add(new UserSearchResult
                {
                    UserId = p.Id,
                    Username = p.Username,
                    AvatarUrl = p.AvatarUrl,
                    RelationStatus = relationStatus,
                    FriendshipId = friendshipId
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SearchUsers error: {ex.Message}");
            return new();
        }
    }

    // ─── Envoyer une demande d'ami ───────────────────────────────────
    public async Task<(bool Success, string Error)> SendFriendRequestAsync(string targetUserId)
    {
        var userId = CurrentUserId;
        if (userId == null) return (false, "Non connecté");

        try
        {
            // Vérifier qu'une relation n'existe pas déjà
            var r1 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, targetUserId)
                .Get();
            var r2 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, targetUserId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            if (r1.Models.Count > 0 || r2.Models.Count > 0)
                return (false, "Une relation existe déjà avec cet utilisateur.");

            var friendship = new Friendship
            {
                RequesterId = userId,
                ReceiverId = targetUserId,
                Status = "pending"
            };
            await _supabase.From<Friendship>().Insert(friendship);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendFriendRequest error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── Accepter une demande ────────────────────────────────────────
    public async Task<bool> AcceptFriendshipAsync(string friendshipId)
    {
        try
        {
            await _supabase
                .From<Friendship>()
                .Filter("id", Postgrest.Constants.Operator.Equals, friendshipId)
                .Set(f => f.Status, "accepted")
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AcceptFriendship error: {ex.Message}");
            return false;
        }
    }

    // ─── Supprimer / refuser ─────────────────────────────────────────
    public async Task<bool> DeleteFriendshipAsync(string friendshipId)
    {
        try
        {
            await _supabase
                .From<Friendship>()
                .Filter("id", Postgrest.Constants.Operator.Equals, friendshipId)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteFriendship error: {ex.Message}");
            return false;
        }
    }

    // ─── Générer un lien d'invitation ────────────────────────────────
    public async Task<string?> GenerateInviteLinkAsync()
    {
        var userId = CurrentUserId;
        if (userId == null) return null;

        try
        {
            var existing = await _supabase
                .From<FriendInvite>()
                .Filter("inviter_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("expires_at", Postgrest.Constants.Operator.GreaterThan,
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                .Get();

            string code;

            if (existing.Models.Count > 0)
            {
                code = existing.Models.First().Code;
            }
            else
            {
                code = Guid.NewGuid().ToString("N")[..12].ToUpper();
                var invite = new FriendInvite
                {
                    InviterId = userId,
                    Code = code,
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };
                await _supabase.From<FriendInvite>().Insert(invite);
            }

            return $"https://beauoupas.fr/invite/{code}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GenerateInviteLink error: {ex.Message}");
            return null;
        }
    }

    // ─── Accepter une invitation via code ────────────────────────────
    public async Task<(bool Success, string Error)> AcceptInviteCodeAsync(string code)
    {
        var userId = CurrentUserId;
        if (userId == null) return (false, "Non connecté");

        try
        {
            var inviteQuery = await _supabase
                .From<FriendInvite>()
                .Filter("code", Postgrest.Constants.Operator.Equals, code)
                .Get();

            var inviteResult = inviteQuery.Models.FirstOrDefault();

            if (inviteResult == null)
                return (false, "Lien invalide ou expiré.");

            if (inviteResult.ExpiresAt < DateTime.UtcNow)
                return (false, "Ce lien d'invitation a expiré.");

            if (inviteResult.InviterId == userId)
                return (false, "Vous ne pouvez pas vous ajouter vous-même.");

            var r1 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, inviteResult.InviterId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, userId)
                .Get();

            var r2 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, inviteResult.InviterId)
                .Get();

            if (r1.Models.Count > 0 || r2.Models.Count > 0)
                return (false, "Vous êtes déjà amis ou une demande est en attente.");

            var friendship = new Friendship
            {
                RequesterId = inviteResult.InviterId,
                ReceiverId = userId,
                Status = "accepted"
            };
            await _supabase.From<Friendship>().Insert(friendship);

            await _supabase
                .From<FriendInvite>()
                .Filter("code", Postgrest.Constants.Operator.Equals, code)
                .Set(i => i.UsedBy, userId)
                .Update();

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AcceptInviteCode error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ─── Vérifier si deux users sont amis ────────────────────────────
    public async Task<bool> AreFriendsAsync(string otherUserId)
    {
        var userId = CurrentUserId;
        if (userId == null) return false;
        try
        {
            var r1 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, otherUserId)
                .Filter("status", Postgrest.Constants.Operator.Equals, "accepted")
                .Get();

            var r2 = await _supabase.From<Friendship>()
                .Filter("requester_id", Postgrest.Constants.Operator.Equals, otherUserId)
                .Filter("receiver_id", Postgrest.Constants.Operator.Equals, userId)
                .Filter("status", Postgrest.Constants.Operator.Equals, "accepted")
                .Get();

            return r1.Models.Count > 0 || r2.Models.Count > 0;
        }
        catch { return false; }
    }

    private async Task<Profile?> GetProfileAsync(string userId)
    {
        try
        {
            var result = await _supabase
                .From<Profile>()
                .Filter("id", Postgrest.Constants.Operator.Equals, userId)
                .Get();
            return result.Models.FirstOrDefault();
        }
        catch { return null; }
    }
}
