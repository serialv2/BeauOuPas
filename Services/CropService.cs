namespace BeauOuPas.Services;

public class CropService
{
    // ✅ Pas de recadrage - sélectionner la photo directement
    // Le recadrage causait des problèmes d'affichage plein écran
    // Les photos sont affichées en AspectFill dans les vignettes
    public Task<Stream?> CropSquareAsync(string imagePath)
        => Task.FromResult<Stream?>(File.OpenRead(imagePath));

    public Task<Stream?> CropFreeAsync(string imagePath)
        => Task.FromResult<Stream?>(File.OpenRead(imagePath));
}
