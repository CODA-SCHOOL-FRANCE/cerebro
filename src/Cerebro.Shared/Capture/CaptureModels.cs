using CSharpFunctionalExtensions;

namespace Cerebro.Shared.Capture;

public enum CaptureFailureReason
{
    PermissionDenied,
    ToolMissing,
    Unknown
}

public sealed record CaptureError(CaptureFailureReason Reason, string Detail);

public static class CaptureResult
{
    public static Result<byte[], CaptureError> Ok(byte[] pngBytes) =>
        Result.Success<byte[], CaptureError>(pngBytes);

    public static Result<byte[], CaptureError> Fail(CaptureFailureReason reason, string detail) =>
        Result.Failure<byte[], CaptureError>(new CaptureError(reason, detail));
}
