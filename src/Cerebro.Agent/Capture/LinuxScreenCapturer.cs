using System.ComponentModel;
using System.Diagnostics;
using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;

namespace Cerebro.Agent.Capture;

internal sealed class LinuxScreenCapturer : IScreenCapturer
{
    private sealed record ToolCandidate(string Command, Func<string, string> ArgsBuilder);

    private static readonly ToolCandidate[] Candidates =
    [
        new("grim", file => $"\"{file}\""),
        new("scrot", file => $"\"{file}\""),
        new("import", file => $"-window root \"{file}\""),
        new("gnome-screenshot", file => $"-f \"{file}\"")
    ];

    public Result<byte[], CaptureError> Capture()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cerebro_{Guid.NewGuid():N}.png");

        try
        {
            if (!Candidates.Any(candidate => TryCapture(candidate, tempFile)))
                return CaptureResult.Fail(
                    CaptureFailureReason.ToolMissing,
                    "Aucun outil de capture d'écran trouvé. Installez-en un selon votre environnement : " +
                    "'sudo apt install grim' (Wayland) ou 'sudo apt install scrot' (X11), puis relancez l'agent.");

            return CaptureResult.Ok(
                File.ReadAllBytes(tempFile)
            );
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static bool TryCapture(ToolCandidate candidate, string tempFile)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = candidate.Command,
                Arguments = candidate.ArgsBuilder(tempFile),
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            process.WaitForExit(10_000);
            return process.ExitCode == 0 && File.Exists(tempFile) && new FileInfo(tempFile).Length > 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}