using BeauOuPas.Models;
using System.Text.Json;

namespace BeauOuPas.Services;

public class GroupService
{
    private readonly Supabase.Client _supabase;

    public GroupService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : RPC qui retourne les groupes avec stats en 1 requête
    // ═════════════════════════════════════════════════════════════════
    public async Task<string?> GetMyGroupsWithStatsJsonAsync()
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return null;

            var result = await _supabase.Rpc("get_my_groups_with_stats", new Dictionary<string, object>
            {
                { "p_user_id", userId }
            });

            return result.Content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMyGroupsWithStats: {ex.Message}");
            return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : RPC qui retourne les détails complets d'un groupe
    // (group + is_admin + members enrichis + series + messages)
    // en 1 SEULE requête au lieu de plusieurs.
    // ═════════════════════════════════════════════════════════════════
    public async Task<string?> GetGroupDetailsJsonAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return null;

            var result = await _supabase.Rpc("get_group_details", new Dictionary<string, object>
            {
                { "p_group_id", groupId },
                { "p_user_id", userId }
            });

            return result.Content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetGroupDetails: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Group>> GetMyGroupsAsync()
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return new();

            var memberships = await _supabase.From<GroupMember>()
                .Where(m => m.UserId == userId).Get();

            var groupIds = memberships.Models.Select(m => m.GroupId).ToList();
            if (!groupIds.Any()) return new();

            var result = await _supabase.From<Group>()
                .Filter("id", Postgrest.Constants.Operator.In, groupIds)
                .Order(g => g.CreatedAt, Postgrest.Constants.Ordering.Descending)
                .Get();

            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMyGroups: {ex.Message}");
            return new();
        }
    }

    public async Task<Group?> CreateGroupAsync(string name, string? description)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return null;

            var group = new Group
            {
                Name = name,
                Description = description,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _supabase.From<Group>().Insert(group);
            var created = result.Models.FirstOrDefault();
            if (created == null) return null;

            await _supabase.From<GroupMember>().Insert(new GroupMember
            {
                GroupId = created.Id,
                UserId = userId,
                Role = "admin",
                JoinedAt = DateTime.UtcNow
            });

            return created;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroup: {ex.Message}");
            return null;
        }
    }

    public async Task<List<GroupMember>> GetMembersAsync(string groupId)
    {
        try
        {
            var result = await _supabase.From<GroupMember>()
                .Where(m => m.GroupId == groupId).Get();
            return result.Models;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMembers: {ex.Message}");
            return new();
        }
    }

    public async Task<bool> AddMemberAsync(string groupId, string userId)
    {
        try
        {
            await _supabase.From<GroupMember>().Insert(new GroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Role = "member",
                JoinedAt = DateTime.UtcNow
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddMember: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LeaveGroupAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;
            await _supabase.From<GroupMember>()
                .Where(m => m.GroupId == groupId)
                .Where(m => m.UserId == userId)
                .Delete();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LeaveGroup: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsMemberAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;
            var result = await _supabase.From<GroupMember>()
                .Where(m => m.GroupId == groupId)
                .Where(m => m.UserId == userId)
                .Get();
            return result.Models.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IsMember: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsAdminAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            if (userId == null) return false;
            var result = await _supabase.From<GroupMember>()
                .Where(m => m.GroupId == groupId)
                .Where(m => m.UserId == userId)
                .Where(m => m.Role == "admin")
                .Get();
            return result.Models.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IsAdmin: {ex.Message}");
            return false;
        }
    }

    public async Task<int> GetActiveSeriesCountAsync(string groupId)
    {
        try
        {
            var result = await _supabase.From<Series>()
                .Where(s => s.GroupId == groupId)
                .Where(s => s.Status == "active")
                .Get();
            return result.Models.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetActiveSeriesCount: {ex.Message}");
            return 0;
        }
    }

    public async Task<List<GroupMessage>> GetMessagesAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            // ⚡ DIAGNOSTIC : log d'entrée pour confirmer que la méthode est bien appelée
            System.Diagnostics.Debug.WriteLine(
                $"[ChatGroup-DIAG] GetMessagesAsync: groupId={groupId}, userId={userId}");

            var result = await _supabase.From<GroupMessage>()
                .Where(m => m.GroupId == groupId)
                .Order(m => m.CreatedAt, Postgrest.Constants.Ordering.Ascending)
                .Get();
            foreach (var msg in result.Models)
                msg.IsMyMessage = msg.SenderId == userId;

            // ⚡ DIAGNOSTIC : combien de messages ressortent du SELECT ?
            // Si l'INSERT a marché mais qu'on en voit 0, c'est que RLS SELECT
            // bloque la lecture. Si on en voit "anciens messages mais pas le nouveau",
            // c'est que l'INSERT a foiré silencieusement.
            System.Diagnostics.Debug.WriteLine(
                $"[ChatGroup-DIAG] GetMessagesAsync: {result.Models.Count} message(s) retourné(s)");

            return result.Models;
        }
        catch (Exception ex)
        {
            // ⚡ DIAGNOSTIC : log enrichi avec le type d'exception pour distinguer
            // une erreur RLS (PostgrestException) d'une erreur réseau/autre.
            System.Diagnostics.Debug.WriteLine(
                $"[ChatGroup-DIAG] GetMessagesAsync ÉCHEC: type={ex.GetType().Name}, message={ex.Message}");
            return new();
        }
    }

    public async Task<bool> SendMessageAsync(string groupId, string content)
    {
        var userId = CurrentUserId;
        // ⚡ DIAGNOSTIC : log d'entrée
        System.Diagnostics.Debug.WriteLine(
            $"[ChatGroup-DIAG] SendMessageAsync: groupId={groupId}, userId={userId}, contentLen={content?.Length ?? 0}");

        if (userId == null)
        {
            System.Diagnostics.Debug.WriteLine("[ChatGroup-DIAG] SendMessageAsync: userId NULL → abandon");
            return false;
        }

        try
        {
            await _supabase.From<GroupMessage>().Insert(new GroupMessage
            {
                GroupId = groupId,
                SenderId = userId,
                Content = content ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            // ⚡ DIAGNOSTIC : si on arrive ici, l'INSERT a réussi côté HTTP.
            System.Diagnostics.Debug.WriteLine(
                "[ChatGroup-DIAG] SendMessageAsync: INSERT réussi");
            return true;
        }
        catch (Exception ex)
        {
            // ⚡ DIAGNOSTIC : log enrichi. Pour une RLS bloquante, on attend un
            // message du genre "new row violates row-level security policy" ou
            // "permission denied for table group_messages".
            System.Diagnostics.Debug.WriteLine(
                $"[ChatGroup-DIAG] SendMessageAsync ÉCHEC: type={ex.GetType().Name}, message={ex.Message}");
            // L'inner exception contient parfois le détail Postgres
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ChatGroup-DIAG] SendMessageAsync inner: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    public async Task<Group?> GetGroupAsync(string groupId)
    {
        try
        {
            return await _supabase.From<Group>()
                .Where(g => g.Id == groupId).Single();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetGroup: {ex.Message}");
            return null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // ⚡ NOUVEAU : suppression sécurisée d'un groupe
    // - Seul le créateur peut supprimer (vérifié côté SQL)
    // - Bloqué si une série ACTIVE est rattachée
    // - Les séries non-actives sont détachées (deviennent standalone)
    // - Les messages partent en cascade FK
    // ═════════════════════════════════════════════════════════════════
    public async Task<DeleteGroupResult> DeleteGroupAsync(string groupId)
    {
        try
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new DeleteGroupResult
                {
                    Success = false,
                    ErrorCode = "not_authenticated",
                    Message = "Tu dois être connecté pour supprimer un groupe."
                };
            }

            var response = await _supabase.Rpc("delete_group_safe", new Dictionary<string, object>
            {
                { "p_group_id", groupId },
                { "p_user_id", userId }
            });

            return ParseDeleteGroupResult(response?.Content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteGroupAsync: {ex.Message}");
            return new DeleteGroupResult
            {
                Success = false,
                ErrorCode = "exception",
                Message = $"Erreur technique : {ex.Message}"
            };
        }
    }

    private static DeleteGroupResult ParseDeleteGroupResult(string? json)
    {
        var fallback = new DeleteGroupResult
        {
            Success = false,
            ErrorCode = "parse_error",
            Message = "Réponse serveur invalide."
        };

        if (string.IsNullOrWhiteSpace(json)) return fallback;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new DeleteGroupResult
            {
                Success = root.TryGetProperty("success", out var s)
                          && s.ValueKind == JsonValueKind.True,
                ErrorCode = root.TryGetProperty("error", out var e)
                            && e.ValueKind == JsonValueKind.String
                            ? e.GetString() : null,
                Message = root.TryGetProperty("message", out var m)
                          && m.ValueKind == JsonValueKind.String
                          ? m.GetString() ?? string.Empty : string.Empty,
                DetachedSeriesCount = root.TryGetProperty("detached_series_count", out var d)
                                      && d.ValueKind == JsonValueKind.Number
                                      ? d.GetInt32() : 0
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseDeleteGroupResult: {ex.Message}");
            return fallback;
        }
    }
}

// ═════════════════════════════════════════════════════════════════════
// ⚡ Résultat structuré d'une opération de suppression de groupe.
// Reflète exactement le JSON renvoyé par la RPC delete_group_safe.
// ═════════════════════════════════════════════════════════════════════
public class DeleteGroupResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DetachedSeriesCount { get; set; }
}