using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class ChatService
{
    private readonly Supabase.Client _supabase;

    public ChatService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    // ─── Propriété CurrentUserId ─────────────────────────────────────
    public string? CurrentUserId => _supabase.Auth.CurrentUser?.Id;

    // ─── Récupérer mes matches ───────────────────────────────────────
    public async Task<List<MatchItem>> GetMatchesAsync()
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return new List<MatchItem>();

            System.Diagnostics.Debug.WriteLine($"GetMatches for user: {userId}");

            var allRequests = await _supabase
                .From<MeetRequest>()
                .Get();

            System.Diagnostics.Debug.WriteLine($"Total requests: {allRequests.Models.Count}");

            var matches = allRequests.Models
                .Where(m => m.IsMatch == true &&
                           (m.FromUserId == userId ||
                            m.ToUserId == userId))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Matches found: {matches.Count}");

            var items = new List<MatchItem>();
            var chatSeen = new HashSet<string>();

            foreach (var match in matches)
            {
                var otherId = match.FromUserId == userId
                    ? match.ToUserId
                    : match.FromUserId;

                if (chatSeen.Contains(otherId)) continue;
                chatSeen.Add(otherId);

                var otherProfile = await _supabase
                    .From<Profile>()
                    .Where(p => p.Id == otherId)
                    .Single();

                if (otherProfile == null) continue;

                var chat = await GetOrCreateChatAsync(userId, otherId);
                var lastMessage = await GetLastMessageAsync(chat.Id);

                items.Add(new MatchItem
                {
                    MatchId = match.Id,
                    ChatId = chat.Id,
                    OtherUserId = otherId,
                    OtherUsername = otherProfile.Username,
                    OtherAvatarUrl = otherProfile.AvatarUrl ?? string.Empty,
                    MatchDate = match.CreatedAt,
                    LastMessage = lastMessage?.Content ?? "Dites bonjour ! 👋",
                    LastMessageTime = lastMessage?.CreatedAt ?? match.CreatedAt,
                    IsBlocked = chat.IsBlocked
                });
            }

            return items
                .OrderByDescending(i => i.LastMessageTime)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMatches error: {ex.Message}");
            return new List<MatchItem>();
        }
    }

    // ─── Récupérer les messages d'un chat ────────────────────────────
    public async Task<List<Message>> GetMessagesAsync(string chatId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;

            var result = await _supabase
                .From<Message>()
                .Where(m => m.ChatId == chatId)
                .Get();

            var messages = result.Models
                .OrderBy(m => m.CreatedAt)
                .ToList();

            foreach (var msg in messages)
                msg.IsMyMessage = msg.SenderId == userId;

            System.Diagnostics.Debug.WriteLine($"Messages loaded: {messages.Count}");
            return messages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMessages error: {ex.Message}");
            return new List<Message>();
        }
    }

    // ─── Envoyer un message ──────────────────────────────────────────
    public async Task<bool> SendMessageAsync(string chatId, string content)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;

            var result = await _supabase.Rpc<string>(
                "insert_message",
                new Dictionary<string, object>
                {
                    { "p_chat_id",   chatId },
                    { "p_sender_id", userId },
                    { "p_content",   content }
                });

            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendMessage error: {ex.Message}");
            return false;
        }
    }

    // ─── Marquer les messages comme lus ─────────────────────────────
    public async Task MarkMessagesAsReadAsync(string chatId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return;

            await _supabase
                .From<Message>()
                .Where(m => m.ChatId == chatId &&
                            m.SenderId != userId &&
                            m.IsRead == false)
                .Set(m => m.IsRead, true)
                .Update();
        }
        catch { }
    }

    // ─── Signaler un message ─────────────────────────────────────────
    public async Task<bool> ReportMessageAsync(string messageId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;

            await _supabase
                .From<Message>()
                .Where(m => m.Id == messageId)
                .Set(m => m.IsReported, true)
                .Set(m => m.ReportedBy, userId)
                .Set(m => m.ReportedAt, DateTime.UtcNow)
                .Update();

            System.Diagnostics.Debug.WriteLine($"Message {messageId} signalé");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReportMessage error: {ex.Message}");
            return false;
        }
    }

    // ─── Bloquer un chat ─────────────────────────────────────────────
    public async Task<bool> BlockChatAsync(string chatId)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;

            await _supabase
                .From<Chat>()
                .Where(c => c.Id == chatId)
                .Set(c => c.IsBlocked, true)
                .Set(c => c.BlockedBy, userId)
                .Update();

            System.Diagnostics.Debug.WriteLine($"Chat {chatId} bloqué");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BlockChat error: {ex.Message}");
            return false;
        }
    }

    // ─── Récupérer l'état du chat ────────────────────────────────────
    public async Task<Chat?> GetChatAsync(string chatId)
    {
        try
        {
            var result = await _supabase
                .From<Chat>()
                .Where(c => c.Id == chatId)
                .Single();
            return result;
        }
        catch { return null; }
    }
    public async Task<string?> GetOrCreateChatIdAsync(string friendUserId)
    {
        try
        {
            var currentUserId = _supabase.Auth.CurrentUser?.Id;

            if (string.IsNullOrWhiteSpace(currentUserId) ||
                string.IsNullOrWhiteSpace(friendUserId))
                return null;

            var chat = await GetOrCreateChatAsync(currentUserId, friendUserId);
            return chat.Id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetOrCreateChatIdAsync error: {ex.Message}");
            return null;
        }
    }
    // ─── Helpers privés ──────────────────────────────────────────────
    public async Task<Chat> GetOrCreateChatAsync(string userId1, string userId2)
    {
        var user1 = string.Compare(userId1, userId2) < 0 ? userId1 : userId2;
        var user2 = string.Compare(userId1, userId2) < 0 ? userId2 : userId1;

        try
        {
            var allChats = await _supabase.From<Chat>().Get();
            var existing = allChats.Models.FirstOrDefault(
                c => (c.User1Id == user1 && c.User2Id == user2) ||
                     (c.User1Id == user2 && c.User2Id == user1));

            if (existing != null) return existing;
        }
        catch { }

        var newChat = new Chat
        {
            User1Id = user1,
            User2Id = user2,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _supabase.From<Chat>().Insert(newChat);
        return result.Models.First();
    }

    private async Task<Message?> GetLastMessageAsync(string chatId)
    {
        try
        {
            var result = await _supabase
                .From<Message>()
                .Where(m => m.ChatId == chatId)
                .Get();

            return result.Models
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();
        }
        catch { return null; }
    }
}

// ─── Modèle pour l'affichage des matches ─────────────────────────────
public class MatchItem
{
    public string MatchId { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUsername { get; set; } = string.Empty;
    public string OtherAvatarUrl { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }
    public DateTime LastMessageTime { get; set; }
    public bool IsBlocked { get; set; } = false;

    public string MatchDateLabel => MatchDate.ToString("dd/MM/yyyy");
    public string LastTimeLabel => LastMessageTime.ToString("HH:mm");
}