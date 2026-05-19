using Supabase;

namespace BeauOuPas.Services;

public class SupabaseService
{
    private static Client? _instance;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<Client> GetClientAsync()
    {
        if (_instance != null) return _instance;

        await _lock.WaitAsync();
        try
        {
            if (_instance != null) return _instance;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            _instance = new Client(
                Constants.SupabaseUrl,
                Constants.SupabaseKey,
                options
            );

            await _instance.InitializeAsync();
            return _instance;
        }
        finally
        {
            _lock.Release();
        }
    }

    public static Client? Instance => _instance;
}
