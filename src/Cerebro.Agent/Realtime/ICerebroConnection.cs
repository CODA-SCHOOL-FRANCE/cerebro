using Cerebro.Shared.Capture;

namespace Cerebro.Agent.Realtime;

public interface ICerebroConnection : IAsyncDisposable
{
    event Func<Task>? Reconnected;

    Task ConnectAsync(CancellationToken cancellationToken);

    Task JoinAsync(string sessionCode, string candidateId, CancellationToken cancellationToken);

    Task PingAsync(CancellationToken cancellationToken);

    Task ReportReadinessAsync(
        bool isReady, CaptureFailureReason? failureReason, string? failureDetail, CancellationToken cancellationToken);

    Task UploadScreenshotAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
