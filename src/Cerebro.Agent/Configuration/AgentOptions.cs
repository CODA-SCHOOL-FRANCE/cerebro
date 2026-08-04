namespace Cerebro.Agent.Configuration;

public sealed record AgentOptions(
    string ServerUrl,
    string SessionCode,
    string CandidateId,
    int MinIntervalSeconds = 480,
    int MaxIntervalSeconds = 720,
    int PingIntervalSeconds = 60);
