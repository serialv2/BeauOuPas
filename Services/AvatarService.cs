namespace BeauOuPas.Services;

public class AvatarService
{
    private readonly Supabase.Client _supabase;

    public AvatarService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadAvatarAsync(
        Stream imageStream, string fileName)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id;
            if (userId == null)
                return (false, null, "Non connecté");

            // ✅ Chemin: userId/avatar.jpg (userId comme dossier = match RLS policy)
            var ext = Path.GetExtension(fileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var filePath = $"{userId}/avatar{ext}";

            // Convertir en bytes
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            // Supprimer l'ancien avatar si existe
            try
            {
                await _supabase.Storage.From("avatars").Remove(
                    new List<string> { filePath });
            }
            catch { }

            // Upload
            await _supabase.Storage.From("avatars").Upload(
                bytes, filePath,
                new Supabase.Storage.FileOptions
                {
                    ContentType = $"image/{ext.TrimStart('.')}",
                    Upsert = true
                });

            // Récupérer URL publique
            var baseUrl = _supabase.Storage.From("avatars").GetPublicUrl(filePath);

            // ⚡ FIX cache : MAUI/Image met en cache par URL. Comme le filename est
            // toujours "avatar.jpg", l'URL serait identique après un changement
            // d'avatar et l'image affichée resterait l'ancienne version cachée.
            // → On ajoute un query param ?v=<timestamp> pour forcer un rechargement.
            // Le serveur ignore le param, mais MAUI traite ça comme une URL différente.
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var url = baseUrl.Contains('?')
                ? $"{baseUrl}&v={cacheBuster}"
                : $"{baseUrl}?v={cacheBuster}";

            // Mettre à jour le profil (avec le param de cache busting persisté en DB)
            await _supabase
                .From<BeauOuPas.Models.Profile>()
                .Where(p => p.Id == userId)
                .Set(p => p.AvatarUrl, url)
                .Update();

            return (true, url, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Avatar] Upload error: {ex.Message}");
            return (false, null, ex.Message);
        }
    }
}
