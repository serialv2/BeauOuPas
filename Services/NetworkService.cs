namespace BeauOuPas.Services;

public class NetworkService
{
    // ─── Récupérer l'IP publique de l'utilisateur ────────────────────
    public async Task<string> GetPublicIpAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = await client.GetStringAsync(
                "https://api.ipify.org");
            return ip.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
