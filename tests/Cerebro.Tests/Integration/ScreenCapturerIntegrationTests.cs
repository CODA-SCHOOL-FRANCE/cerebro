using Cerebro.Agent.Capture;
using NFluent;

namespace Cerebro.Tests.Integration;

/// <summary>
/// These tests exercise the real OS-level screenshot mechanism (GDI on Windows,
/// screencapture on macOS, grim/scrot/import/gnome-screenshot on Linux).
/// They only validate the capturer for whichever OS they run on.
/// </summary>
[Trait("Category", "Integration")]
public class ScreenCapturerIntegrationTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Create_ShouldReturnACapturerForTheCurrentOperatingSystem()
    {
        var capturer = ScreenCapturerFactory.Create();

        Check.That(capturer).IsNotNull();
    }

    [Fact]
    public void Capture_ShouldProduceAValidPngScreenshot()
    {
        var capturer = ScreenCapturerFactory.Create();

        var result = capturer.Capture();

        Check.That(result.IsSuccess).IsTrue();
        Check.That(result.Value).IsNotNull();
        Check.That(result.Value.Length).IsStrictlyGreaterThan(0);
        Check.That(result.Value.Take(PngSignature.Length)).ContainsExactly(PngSignature);
    }

    [Fact]
    public void Capture_ShouldSucceedAcrossConsecutiveCalls()
    {
        var capturer = ScreenCapturerFactory.Create();

        var firstResult = capturer.Capture();
        var secondResult = capturer.Capture();

        Check.That(firstResult.IsSuccess).IsTrue();
        Check.That(secondResult.IsSuccess).IsTrue();
    }
}
