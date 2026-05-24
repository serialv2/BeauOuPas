namespace BeauOuPas.Services;

public class PhotoUploadService
{
    private readonly Supabase.Client _supabase;
    private readonly ImageService _imageService;

    public PhotoUploadService(
        Supabase.Client supabase,
        ImageService imageService)
    {
        _supabase = supabase;
        _imageService = imageService;
    }

    // ─── Upload une photo complète (display + thumbnail) ────────────
    public async Task<(string DisplayUrl, string ThumbUrl)> UploadPhotoAsync(
        Stream imageStream,
        string fileName)
    {
        var userId = _supabase.Auth.CurrentUser?.Id ?? "unknown";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var baseName = $"{userId}/{timestamp}";

        var imageBytes = await StreamToBytes(imageStream);

        // Upload version display (1080px)
        using var displayStream = new MemoryStream(imageBytes);
        var compressedDisplay = await _imageService
            .CompressImageAsync(displayStream);
        var displayPath = $"display/{baseName}.jpg";
        var displayUrl = await UploadToStorageAsync(
            compressedDisplay, displayPath, "photos");

        // Upload version thumbnail (300px)
        using var thumbStream = new MemoryStream(imageBytes);
        var compressedThumb = await _imageService
            .CreateThumbnailAsync(thumbStream);
        var thumbPath = $"thumbnails/{baseName}.jpg";
        var thumbUrl = await UploadToStorageAsync(
            compressedThumb, thumbPath, "photos");

        return (displayUrl, thumbUrl);
    }

    // ─── Upload selfie léger pour le flow de vote ────────────────────
    // Une seule version compressée (taille thumb), bucket "photos",
    // dossier dédié selfies/{seriesId}/{seriesProjectId}/{userId}_{timestamp}.jpg
    // Optimisé pour rester sous 1s sur connexion correcte.
    public async Task<string> UploadSelfieFastAsync(
        Stream imageStream,
        string seriesId,
        string seriesProjectId,
        string userId)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var path = $"selfies/{seriesId}/{seriesProjectId}/{userId}_{timestamp}.jpg";

            var compressed = await _imageService.CreateThumbnailAsync(imageStream);
            return await UploadToStorageAsync(compressed, path, "photos");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UploadSelfieFast: {ex.Message}");
            return string.Empty;
        }
    }

    // ─── Upload selfie "session" pour quiz / partie rapide ───────────
    // Pas de seriesProjectId (les selfies quiz/party sont attachés à la
    // série uniquement, pas à une question particulière).
    // Path : selfies/{seriesId}/_session/{userId}_{timestamp}.jpg
    public async Task<string> UploadSessionSelfieFastAsync(
        Stream imageStream,
        string seriesId,
        string userId)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var path = $"selfies/{seriesId}/_session/{userId}_{timestamp}.jpg";

            var compressed = await _imageService.CreateThumbnailAsync(imageStream);
            return await UploadToStorageAsync(compressed, path, "photos");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UploadSessionSelfieFast: {ex.Message}");
            return string.Empty;
        }
    }

    // ─── ⚡ NOUVEAU : Upload photo pour une question de quiz ─────────
    // Bucket "quiz_photos", une seule version compressée (1080px display).
    // Le fichier est nommé avec un GUID temporaire, on n'a pas encore l'ID
    // de la question (créé seulement quand la RPC est appelée en E2d).
    // Path : {userId}/{guid}.jpg
    public async Task<string> UploadQuizPhotoAsync(Stream imageStream)
    {
        try
        {
            var userId = _supabase.Auth.CurrentUser?.Id ?? "unknown";
            var guid = Guid.NewGuid().ToString("N");
            var path = $"{userId}/{guid}.jpg";

            // Compression 1080px (taille display, pas thumb : on veut la voir
            // en grand sur la TV)
            var compressed = await _imageService.CompressImageAsync(imageStream);
            return await UploadToStorageAsync(compressed, path, "quiz_photos");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UploadQuizPhoto: {ex.Message}");
            return string.Empty;
        }
    }

    // ─── ⚡ NOUVEAU : Supprimer une photo de quiz ────────────────────
    // Utilisé quand l'utilisateur change la photo d'une question
    // (on supprime l'ancienne pour ne pas accumuler de fichiers orphelins).
    public async Task DeleteQuizPhotoAsync(string photoUrl)
    {
        if (string.IsNullOrEmpty(photoUrl)) return;

        try
        {
            var path = ExtractPathFromUrl(photoUrl, "quiz_photos");
            if (string.IsNullOrEmpty(path)) return;

            await _supabase.Storage
                .From("quiz_photos")
                .Remove(new List<string> { path });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteQuizPhoto: {ex.Message}");
        }
    }

    // ─── Supprimer une photo ─────────────────────────────────────────
    public async Task DeletePhotoAsync(string displayUrl, string thumbUrl)
    {
        try
        {
            var displayPath = ExtractPathFromUrl(displayUrl);
            var thumbPath = ExtractPathFromUrl(thumbUrl);

            await _supabase.Storage
                .From("photos")
                .Remove(new List<string> { displayPath, thumbPath });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Delete error: {ex.Message}");
        }
    }

    // ─── Helpers privés ──────────────────────────────────────────────
    private async Task<string> UploadToStorageAsync(
        Stream stream, string path, string bucket)
    {
        var bytes = await StreamToBytes(stream);

        await _supabase.Storage
            .From(bucket)
            .Upload(bytes, path, new Supabase.Storage.FileOptions
            {
                ContentType = "image/jpeg",
                Upsert = true
            });

        return _supabase.Storage
            .From(bucket)
            .GetPublicUrl(path);
    }

    private async Task<byte[]> StreamToBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private string ExtractPathFromUrl(string url)
    {
        return ExtractPathFromUrl(url, "photos");
    }

    private string ExtractPathFromUrl(string url, string bucket)
    {
        try
        {
            var uri = new Uri(url);
            var marker = $"/{bucket}/";
            var segments = uri.AbsolutePath.Split(marker);
            return segments.Length > 1 ? segments[1] : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
