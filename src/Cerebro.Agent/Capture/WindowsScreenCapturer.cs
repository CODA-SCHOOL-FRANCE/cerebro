using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;

namespace Cerebro.Agent.Capture;

[SupportedOSPlatform("windows")]
internal sealed class WindowsScreenCapturer : IScreenCapturer
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public Result<byte[], CaptureError> Capture()
    {
        try
        {
            using var bitmap = new Bitmap(GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));
            using var graphics = Graphics.FromImage(bitmap);

            var screenSize = new Size(GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));

            graphics.CopyFromScreen(
                GetSystemMetrics(SmXVirtualScreen),
                GetSystemMetrics(SmYVirtualScreen),
                0,
                0,
                screenSize
            );

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);

            return CaptureResult.Ok(stream.ToArray());
        }
        catch (Exception ex)
        {
            return CaptureResult.Fail(CaptureFailureReason.Unknown, ex.Message);
        }
    }
}