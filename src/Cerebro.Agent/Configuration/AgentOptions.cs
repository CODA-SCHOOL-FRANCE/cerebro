namespace Cerebro.Agent.Configuration;

public sealed record AgentOptions(
    string ServerUrl,
    string SessionCode,
    string CandidateId,
    int MinIntervalSeconds = 60,
    int MaxIntervalSeconds = 600,
    int PingIntervalSeconds = 60);