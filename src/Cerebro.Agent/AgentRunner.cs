using Cerebro.Agent.Capture;
using Cerebro.Agent.Configuration;
using Cerebro.Agent.Realtime;
using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;

namespace Cerebro.Agent;

public sealed class AgentRunner
{
    private readonly IScreenCapturer _capturer;
    private readonly ICerebroConnection _connection;
    private readonly AgentOptions _options;
    private readonly Random _random;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    private bool? _lastReadiness;
    private CaptureFailureReason? _lastFailureReason;
    private string? _lastFailureDetail;

    // AgentRunner reste agnostique de la console pour rester testable indépendamment de l'affichage.
    // (la couche console) s'abonne à ces évènements pour afficher un retour visuel au candidat ;
    public event Action? Connected;
    public event Action<string>? Activity;

    public AgentRunner(
        IScreenCapturer capturer,
        ICerebroConnection connection,
        AgentOptions options,
        Random? random = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _capturer = capturer;
        _connection = connection;
        _options = options;
        _random = random ?? Random.Shared;
        _delay = delay ?? Task.Delay;

        _connection.Reconnected += HandleReconnectedAsync;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _connection.ConnectAsync(cancellationToken);
        await _connection.JoinAsync(_options.SessionCode, _options.CandidateId, cancellationToken);
        Connected?.Invoke();

        await RunCaptureAttemptAsync(cancellationToken);

        await Task.WhenAll(
            RunPingLoopAsync(cancellationToken),
            RunCaptureLoopAsync(cancellationToken));
    }

    private async Task RunPingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _delay(TimeSpan.FromSeconds(_options.PingIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await _connection.PingAsync(cancellationToken);
                Activity?.Invoke("Ping envoyé au serveur.");
            }
            catch (Exception)
            {
                // Le ping est un signal de vie best-effort : une coupure réseau ponctuelle (connexion en cours
                // de reconnexion automatique) ne doit pas interrompre la boucle, juste sauter ce battement.
            }
        }
    }

    private async Task RunCaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _delay(NextInterval(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await RunCaptureAttemptAsync(cancellationToken);
        }
    }

    private async Task RunCaptureAttemptAsync(CancellationToken cancellationToken)
        => await _capturer.Capture()
            .Tap(bytes => CompressUploadAndReportReadyAsync(bytes, cancellationToken))
            .TapError(error => ReportReadinessIfChangedAsync(
                isReady: false, error.Reason, error.Detail, cancellationToken));

    private async Task CompressUploadAndReportReadyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var payload = ScreenshotCompressor.Compress(bytes);
        await _connection.UploadScreenshotAsync(payload, cancellationToken);
        Activity?.Invoke($"Screenshot envoyé au serveur ({payload.Length:N0} octets).");

        await ReportReadinessIfChangedAsync(isReady: true, reason: null, detail: null, cancellationToken);
    }

    private async Task ReportReadinessIfChangedAsync(
        bool isReady,
        CaptureFailureReason? reason,
        string? detail,
        CancellationToken cancellationToken)
    {
        if (_lastReadiness == isReady)
        {
            return;
        }

        _lastReadiness = isReady;
        _lastFailureReason = reason;
        _lastFailureDetail = detail;

        await _connection.ReportReadinessAsync(isReady, reason, detail, cancellationToken);

        Activity?.Invoke(isReady
            ? "Statut de préparation envoyé : prêt."
            : $"Statut de préparation envoyé : échec ({reason}).");
    }

    private async Task HandleReconnectedAsync()
    {
        await _connection.JoinAsync(_options.SessionCode, _options.CandidateId, CancellationToken.None);
        Connected?.Invoke();

        if (_lastReadiness.HasValue)
        {
            await _connection.ReportReadinessAsync(
                _lastReadiness.Value, _lastFailureReason, _lastFailureDetail, CancellationToken.None);
        }
    }

    private TimeSpan NextInterval()
    {
        var seconds = _random.Next(_options.MinIntervalSeconds, _options.MaxIntervalSeconds + 1);
        return TimeSpan.FromSeconds(seconds);
    }
}