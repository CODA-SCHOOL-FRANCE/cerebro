namespace Cerebro.Server.Telemetry;

public static class SessionActivityEventType
{
    public const string CandidateJoined = "CandidateJoined";
    public const string CandidateDisconnected = "CandidateDisconnected";
    public const string ScreenshotReceived = "ScreenshotReceived";
    public const string ReadinessReported = "ReadinessReported";
    public const string SessionStarted = "SessionStarted";
    public const string SessionEnded = "SessionEnded";
}
