using Cerebro.Agent.Capture;
using NFluent;
using SkiaSharp;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class ScreenshotCompressorTests
{
    [Fact]
    public void Compress_ShouldEncodeAsWebp_AndDownscaleToMaxDimension()
    {
        var pngBytes = CreatePng(width: 2000, height: 1000);

        var compressed = ScreenshotCompressor.Compress(pngBytes);

        Check.That(IsWebp(compressed)).IsTrue();
        Check.That(compressed.Length).IsStrictlyLessThan(pngBytes.Length);

        using var decoded = SKBitmap.Decode(compressed);
        Check.That(decoded).IsNotNull();
        Check.That(Math.Max(decoded!.Width, decoded.Height) <= 1280).IsTrue();
        // Aspect ratio préservé (2:1 dans ce test).
        Check.That(decoded.Width).IsEqualTo(decoded.Height * 2);
    }

    [Fact]
    public void Compress_ShouldNotUpscale_WhenImageIsAlreadySmallerThanMaxDimension()
    {
        var pngBytes = CreatePng(width: 400, height: 300);

        var compressed = ScreenshotCompressor.Compress(pngBytes);

        using var decoded = SKBitmap.Decode(compressed);
        Check.That(decoded).IsNotNull();
        Check.That(decoded!.Width).IsEqualTo(400);
        Check.That(decoded.Height).IsEqualTo(300);
    }

    [Fact]
    public void Compress_ShouldReturnOriginalBytes_WhenInputIsNotADecodableImage()
    {
        byte[] garbage = [1, 2, 3, 4, 5];

        var result = ScreenshotCompressor.Compress(garbage);

        Check.That(result).ContainsExactly(garbage);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static bool IsWebp(byte[] bytes)
        => bytes.Length >= 12
           && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
           && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P';
}
