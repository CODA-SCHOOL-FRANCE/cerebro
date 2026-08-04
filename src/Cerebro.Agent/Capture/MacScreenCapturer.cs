using System.Diagnostics;
using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;

namespace Cerebro.Agent.Capture;

internal sealed class MacScreenCapturer : IScreenCapturer
{
    public Result<byte[], CaptureError> Capture()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cerebro_{Guid.NewGuid():N}.png");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                Arguments = $"-x -t png \"{tempFile}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return CaptureResult.Fail(CaptureFailureReason.Unknown, "Impossible de démarrer screencapture.");

            process.WaitForExit(10_000);

            if (process.ExitCode != 0 || !File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
            {
                return CaptureResult.Fail(
                    CaptureFailureReason.PermissionDenied,
                    "screencapture a échoué. Vérifiez que la permission 'Enregistrement de l'écran' est accordée " +
                    "dans Réglages Système > Confidentialité et sécurité, puis relancez l'agent.");
            }

            return CaptureResult.Ok(
                File.ReadAllBytes(tempFile)
            );
        }
        catch (Exception ex)
        {
            return CaptureResult.Fail(CaptureFailureReason.Unknown, ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}