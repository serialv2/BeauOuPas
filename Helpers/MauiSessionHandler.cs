using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue;

namespace BeauOuPas.Helpers;

public class MauiSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string SessionKey = "supabase_session";

    public void SaveSession(Session session)
    {
        try
        {
            var json = Newtonsoft.Json.JsonConvert
                .SerializeObject(session);
            Preferences.Default.Set(SessionKey, json);
            System.Diagnostics.Debug.WriteLine(
                $"=== SESSION SAVED ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"SaveSession error: {ex.Message}");
        }
    }

    public Session? LoadSession()
    {
        try
        {
            var json = Preferences.Default
                .Get(SessionKey, string.Empty);
            System.Diagnostics.Debug.WriteLine(
                $"=== LOAD SESSION json empty:{string.IsNullOrEmpty(json)} ===");
            if (string.IsNullOrEmpty(json)) return null;
            return Newtonsoft.Json.JsonConvert
                .DeserializeObject<Session>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"LoadSession error: {ex.Message}");
            return null;
        }
    }
    public void DestroySession()
    {
        Preferences.Default.Remove(SessionKey);
    }
}
