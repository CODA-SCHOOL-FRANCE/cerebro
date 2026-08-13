using SkiaSharp;

namespace Cerebro.Agent.Capture;

public static class ScreenshotCompressor
{
    // Redimensionnement + qualité choisis pour rester lisibles comme preuve anti-fraude (texte à
    // l'écran, contenu de navigateur/IDE) tout en réduisant nettement le poids d'un screenshot
    // 4K/Retina brut.
    private const int MaxDimension = 1280;
    private const int WebpQuality = 75;

    public static byte[] Compress(byte[] pngBytes)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(pngBytes);
            if (bitmap is null)
            {
                return pngBytes;
            }

            using var resized = Resize(bitmap);
            using var image = SKImage.FromBitmap(resized ?? bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
            return data.ToArray();
        }
        catch (Exception)
        {
            // Une image inattendue (format non supporté, capture corrompue) ne doit pas faire
            // échouer toute la tentative de capture : on retombe sur le PNG d'origine, toujours
            // valide, plutôt que de perdre le screenshot.
            return pngBytes;
        }
    }

    private static SKBitmap? Resize(SKBitmap bitmap)
    {
        var longestSide = Math.Max(bitmap.Width, bitmap.Height);
        if (longestSide <= MaxDimension)
        {
            return null;
        }

        var scale = (double)MaxDimension / longestSide;
        var targetWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        return bitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
    }
}
