using SkiaSharp;

namespace BeauOuPas.Services;

public class ImageService
{
    private const int MaxWidth = 1080;
    private const int ThumbWidth = 300;
    private const int JpegQuality = 80;
    private const long MaxFileSizeBytes = 30L * 1024 * 1024; // 30 MB

    // ═════════════════════════════════════════════════════════════════
    // Version FINALE + gestion EXIF orientation
    //
    // Pipeline :
    //   1. SKBitmap.Decode(bytes)         ← marche ✓
    //   2. bitmap.Resize(...)             ← marche ✓ (réduit la taille
    //                                        pour rotation rapide ensuite)
    //   3. Lecture EXIF orientation       ← parser binaire JPEG
    //   4. Si orientation != 1 → rotation pixel par pixel
    //   5. SKImage.FromBitmap.Encode      ← marche ✓
    //
    // La rotation pixel-par-pixel se fait APRÈS le resize, donc sur une
    // image de 1080×1440 max (1.5M pixels), pas sur la photo originale
    // 3000×4000 (12M pixels). C'est ~10x plus rapide.
    // ═════════════════════════════════════════════════════════════════

    public async Task<Stream> CompressImageAsync(Stream inputStream)
    {
        return await Task.Run(() =>
        {
            var bytes = StreamToBytes(inputStream);
            System.Diagnostics.Debug.WriteLine(
                $"[ImageService] Compress: input={bytes.Length / 1024} KB");

            try
            {
                var compressed = TryCompress(bytes, MaxWidth, JpegQuality);
                if (compressed != null && compressed.Length > 1000)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ImageService] Compress: ✓ output={compressed.Length / 1024} KB");
                    return (Stream)new MemoryStream(compressed);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ImageService] Compress error: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ImageService] Compress: fallback to original ({bytes.Length / 1024} KB)");
            return (Stream)new MemoryStream(bytes);
        });
    }

    public async Task<Stream> CreateThumbnailAsync(Stream inputStream)
    {
        return await Task.Run(() =>
        {
            var bytes = StreamToBytes(inputStream);
            System.Diagnostics.Debug.WriteLine(
                $"[ImageService] Thumb: input={bytes.Length / 1024} KB");

            try
            {
                var thumb = TryCompress(bytes, ThumbWidth, 75);
                if (thumb != null && thumb.Length > 500)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ImageService] Thumb: ✓ output={thumb.Length / 1024} KB");
                    return (Stream)new MemoryStream(thumb);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ImageService] Thumb error: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ImageService] Thumb: fallback to original ({bytes.Length / 1024} KB)");
            return (Stream)new MemoryStream(bytes);
        });
    }

    private byte[]? TryCompress(byte[] bytes, int maxWidth, int quality)
    {
        // Lecture EXIF AVANT décodage (sur les bytes originaux)
        int orientation = GetExifOrientation(bytes);
        System.Diagnostics.Debug.WriteLine(
            $"[ImageService] EXIF orientation: {orientation}");

        // Décodage
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap == null)
        {
            System.Diagnostics.Debug.WriteLine("[ImageService] Decode null");
            return null;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[ImageService] Decoded: {bitmap.Width}x{bitmap.Height}");

        // Resize si trop large
        SKBitmap working = bitmap;
        SKBitmap? resized = null;
        try
        {
            if (bitmap.Width > maxWidth)
            {
                int newWidth = maxWidth;
                int newHeight = (int)(bitmap.Height * (double)maxWidth / bitmap.Width);

                resized = bitmap.Resize(
                    new SKImageInfo(newWidth, newHeight, bitmap.ColorType, bitmap.AlphaType),
                    new SKSamplingOptions(SKFilterMode.Linear));

                if (resized != null)
                {
                    working = resized;
                    System.Diagnostics.Debug.WriteLine(
                        $"[ImageService] Resized to {newWidth}x{newHeight}");
                }
            }

            // Rotation EXIF après resize (rapide sur petit bitmap)
            SKBitmap? rotated = null;
            if (orientation > 1 && orientation <= 8)
            {
                rotated = ApplyExifRotation(working, orientation);
                if (rotated != null)
                {
                    working = rotated;
                    System.Diagnostics.Debug.WriteLine(
                        $"[ImageService] Rotated for EXIF {orientation} → {working.Width}x{working.Height}");
                }
            }

            try
            {
                using var image = SKImage.FromBitmap(working);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

                if (data == null || data.Size == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ImageService] Encode empty");
                    return null;
                }

                return data.ToArray();
            }
            finally
            {
                rotated?.Dispose();
            }
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /// <summary>
    /// Applique la rotation EXIF en manipulant les pixels directement
    /// (pas de SKCanvas qui est buggé en SkiaSharp 3.x sur Android).
    /// </summary>
    private SKBitmap? ApplyExifRotation(SKBitmap source, int orientation)
    {
        int srcW = source.Width;
        int srcH = source.Height;

        // Orientations 5,6,7,8 = rotation 90° ou 270° → swap width/height
        bool swap = orientation == 5 || orientation == 6
                 || orientation == 7 || orientation == 8;

        int dstW = swap ? srcH : srcW;
        int dstH = swap ? srcW : srcH;

        var dst = new SKBitmap(new SKImageInfo(dstW, dstH,
            source.ColorType, source.AlphaType));

        try
        {
            for (int y = 0; y < srcH; y++)
            {
                for (int x = 0; x < srcW; x++)
                {
                    var color = source.GetPixel(x, y);

                    int dx, dy;
                    switch (orientation)
                    {
                        case 2: dx = srcW - 1 - x; dy = y; break;            // miroir horizontal
                        case 3: dx = srcW - 1 - x; dy = srcH - 1 - y; break; // 180°
                        case 4: dx = x; dy = srcH - 1 - y; break;            // miroir vertical
                        case 5: dx = y; dy = x; break;                       // 90° + miroir
                        case 6: dx = srcH - 1 - y; dy = x; break;            // 90° horaire
                        case 7: dx = srcH - 1 - y; dy = srcW - 1 - x; break; // 270° + miroir
                        case 8: dx = y; dy = srcW - 1 - x; break;            // 270° horaire
                        default: dx = x; dy = y; break;
                    }

                    if (dx >= 0 && dx < dstW && dy >= 0 && dy < dstH)
                        dst.SetPixel(dx, dy, color);
                }
            }
            return dst;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ImageService] Rotation error: {ex.Message}");
            dst.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Parse le tag EXIF "Orientation" (0x0112) du JPEG.
    /// Retourne 1 si pas de tag trouvé (= orientation normale).
    /// </summary>
    private int GetExifOrientation(byte[] bytes)
    {
        try
        {
            // Header JPEG : FF D8
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
                return 1;

            int offset = 2;
            while (offset < bytes.Length - 2)
            {
                if (bytes[offset] != 0xFF) break;
                var marker = bytes[offset + 1];
                offset += 2;

                if (marker == 0xE1) // APP1 = EXIF
                {
                    var segmentLength = (bytes[offset] << 8) | bytes[offset + 1];
                    offset += 2;

                    // Vérifier "Exif\0\0"
                    if (offset + 6 < bytes.Length &&
                        bytes[offset] == 'E' && bytes[offset + 1] == 'x' &&
                        bytes[offset + 2] == 'i' && bytes[offset + 3] == 'f')
                    {
                        return ParseTiffOrientation(bytes, offset + 6);
                    }
                    offset += segmentLength - 2;
                }
                else if (marker == 0xDA) // SOS = début image, on arrête
                {
                    break;
                }
                else
                {
                    var segmentLength = (bytes[offset] << 8) | bytes[offset + 1];
                    offset += segmentLength;
                }
            }
        }
        catch { }
        return 1;
    }

    private int ParseTiffOrientation(byte[] bytes, int tiffStart)
    {
        try
        {
            if (tiffStart + 8 >= bytes.Length) return 1;

            // Détection endianness : "II" = little, "MM" = big
            bool littleEndian = bytes[tiffStart] == 'I' && bytes[tiffStart + 1] == 'I';

            int ReadUInt16(int pos) => littleEndian
                ? (bytes[pos] | (bytes[pos + 1] << 8))
                : ((bytes[pos] << 8) | bytes[pos + 1]);

            int ReadInt32(int pos) => littleEndian
                ? (bytes[pos] | (bytes[pos + 1] << 8) | (bytes[pos + 2] << 16) | (bytes[pos + 3] << 24))
                : ((bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3]);

            var ifdOffset = ReadInt32(tiffStart + 4);
            var ifdPos = tiffStart + ifdOffset;
            if (ifdPos + 2 >= bytes.Length) return 1;

            var entryCount = ReadUInt16(ifdPos);
            ifdPos += 2;

            for (int i = 0; i < entryCount && ifdPos + 12 <= bytes.Length; i++)
            {
                var tag = ReadUInt16(ifdPos);
                if (tag == 0x0112) // Orientation
                {
                    return ReadUInt16(ifdPos + 8);
                }
                ifdPos += 12;
            }
        }
        catch { }
        return 1;
    }

    public bool IsValidImage(string fileName, long fileSize)
    {
        var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(fileName).ToLower();
        if (!validExtensions.Contains(ext)) return false;
        if (fileSize > MaxFileSizeBytes) return false;
        return true;
    }

    private byte[] StreamToBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
