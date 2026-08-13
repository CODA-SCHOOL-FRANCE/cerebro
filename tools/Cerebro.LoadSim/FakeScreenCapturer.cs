using Cerebro.Agent.Capture;
using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;
using SkiaSharp;

namespace Cerebro.LoadSim;

// Génère une image synthétique au lieu de capturer le vrai écran : la simulation fait tourner N
// candidats en parallèle sur une seule machine, une vraie capture d'écran (permissions OS, fenêtre
// active...) n'a pas de sens ici et ralentirait/polluerait le test. Reste PNG en sortie, comme une
// vraie capture, pour repasser par le même pipeline que l'agent réel (ScreenshotCompressor -> WebP)
// et rester représentatif de la charge réseau/disque générée.
internal sealed class FakeScreenCapturer : IScreenCapturer
{
    public Result<byte[], CaptureError> Capture()
    {
        using var bitmap = new SKBitmap(1920, 1080);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(
            (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256)));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return CaptureResult.Ok(data.ToArray());
    }
}
