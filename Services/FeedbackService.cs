using BeauOuPas.Models;

namespace BeauOuPas.Services;

public class FeedbackService
{
    private readonly Supabase.Client _supabase;

    public FeedbackService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<bool> SendFeedbackAsync(
        int rating, string message)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null) return false;

            var feedback = new UserFeedback
            {
                UserId = userId,
                Rating = rating,
                Message = message,
                Platform = "android",
                AppVersion = AppInfo.VersionString
            };

            await _supabase.From<UserFeedback>().Insert(feedback);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Feedback] error: {ex.Message}");
            return false;
        }
    }
}
