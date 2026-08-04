using System.Runtime.InteropServices;

namespace Cerebro.Agent.Capture;

public static class ScreenCapturerFactory
{
    public static IScreenCapturer Create() => true switch
    {
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => new WindowsScreenCapturer(),
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => new MacScreenCapturer(),
        _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => new LinuxScreenCapturer(),
        _ => throw new PlatformNotSupportedException($"OS non supporté : {RuntimeInformation.OSDescription}")
    };
}