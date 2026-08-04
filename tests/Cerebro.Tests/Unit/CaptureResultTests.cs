using Cerebro.Shared.Capture;
using NFluent;

namespace Cerebro.Tests.Unit;

[Trait("Category", "Unit")]
public class CaptureResultTests
{
    [Fact]
    public void Ok_ShouldReturnSuccessfulResultWithPngBytes()
    {
        byte[] pngBytes = [1, 2, 3];

        var result = CaptureResult.Ok(pngBytes);

        Check.That(result.IsSuccess).IsTrue();
        Check.That(result.Value).ContainsExactly(pngBytes);
    }

    [Theory]
    [InlineData(CaptureFailureReason.PermissionDenied)]
    [InlineData(CaptureFailureReason.ToolMissing)]
    [InlineData(CaptureFailureReason.Unknown)]
    public void Fail_ShouldReturnFailedResultWithReasonAndDetail(CaptureFailureReason reason)
    {
        const string detail = "quelque chose a échoué";

        var result = CaptureResult.Fail(reason, detail);

        Check.That(result.IsSuccess).IsFalse();
        Check.That(result.Error.Reason).IsEqualTo(reason);
        Check.That(result.Error.Detail).IsEqualTo(detail);
    }
}
