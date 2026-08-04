using Cerebro.Server.Data;
using Microsoft.Data.Sqlite;
using NFluent;

namespace Cerebro.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SqliteExamRepositoryIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteExamRepository _repository;

    public SqliteExamRepositoryIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cerebro-repo-tests-{Guid.NewGuid():N}.db");
        _repository = new SqliteExamRepository($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task CreateSession_ThenRegisterCandidate_ShouldBeRecognizedAsRegistered()
    {
        var sessionId = await _repository.CreateSessionAsync("SESSION-A", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "FFFB5AB1", "FFFB5AB1", CancellationToken.None);

        var isRegistered = await _repository.IsCandidateRegisteredAsync(
            "SESSION-A", "FFFB5AB1", CancellationToken.None);

        Check.That(isRegistered).IsTrue();
    }

    [Fact]
    public async Task IsCandidateRegistered_ShouldReturnFalse_ForUnknownCandidateId()
    {
        var sessionId = await _repository.CreateSessionAsync("SESSION-B", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "FFFB5AB1", "FFFB5AB1", CancellationToken.None);

        var isRegistered = await _repository.IsCandidateRegisteredAsync(
            "SESSION-B", "UNKNOWN-ID", CancellationToken.None);

        Check.That(isRegistered).IsFalse();
    }

    [Fact]
    public async Task IsCandidateRegistered_ShouldReturnFalse_WhenSessionUnknown()
    {
        var isRegistered = await _repository.IsCandidateRegisteredAsync(
            "UNKNOWN-SESSION", "FFFB5AB1", CancellationToken.None);

        Check.That(isRegistered).IsFalse();
    }

    [Fact]
    public async Task IsCandidateRegistered_ShouldReturnFalse_WhenCandidateBelongsToAnotherSession()
    {
        var sessionAId = await _repository.CreateSessionAsync("SESSION-C", CancellationToken.None);
        await _repository.CreateSessionAsync("SESSION-D", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionAId, "FFFB5AB1", "FFFB5AB1", CancellationToken.None);

        var isRegistered = await _repository.IsCandidateRegisteredAsync(
            "SESSION-D", "FFFB5AB1", CancellationToken.None);

        Check.That(isRegistered).IsFalse();
    }

    [Fact]
    public async Task SessionExists_ShouldReflectWhetherSessionWasCreated()
    {
        Check.That(await _repository.SessionExistsAsync("SESSION-E", CancellationToken.None)).IsFalse();

        await _repository.CreateSessionAsync("SESSION-E", CancellationToken.None);

        Check.That(await _repository.SessionExistsAsync("SESSION-E", CancellationToken.None)).IsTrue();
    }

    [Fact]
    public async Task CreateSession_ShouldRejectDuplicateSessionCode()
    {
        await _repository.CreateSessionAsync("SESSION-F", CancellationToken.None);

        await Assert.ThrowsAsync<SqliteException>(
            () => _repository.CreateSessionAsync("SESSION-F", CancellationToken.None));
    }

    [Fact]
    public async Task MarkStarted_ShouldSetStartedAtTimestamp()
    {
        await _repository.CreateSessionAsync("SESSION-G", CancellationToken.None);
        Check.That(await _repository.GetStartedAtAsync("SESSION-G", CancellationToken.None)).IsNull();

        await _repository.MarkStartedAsync("SESSION-G", CancellationToken.None);

        Check.That(await _repository.GetStartedAtAsync("SESSION-G", CancellationToken.None)).IsNotNull();
    }

    [Fact]
    public async Task MarkEnded_ShouldBeReflectedByIsSessionEnded()
    {
        await _repository.CreateSessionAsync("SESSION-H", CancellationToken.None);
        Check.That(await _repository.IsSessionEndedAsync("SESSION-H", CancellationToken.None)).IsFalse();

        await _repository.MarkEndedAsync("SESSION-H", CancellationToken.None);

        Check.That(await _repository.IsSessionEndedAsync("SESSION-H", CancellationToken.None)).IsTrue();
    }

    [Fact]
    public async Task MarkStarted_AfterEnded_ShouldClearEndedAtToAllowRestart()
    {
        await _repository.CreateSessionAsync("SESSION-H2", CancellationToken.None);
        await _repository.MarkStartedAsync("SESSION-H2", CancellationToken.None);
        await _repository.MarkEndedAsync("SESSION-H2", CancellationToken.None);
        Check.That(await _repository.IsSessionEndedAsync("SESSION-H2", CancellationToken.None)).IsTrue();

        await _repository.MarkStartedAsync("SESSION-H2", CancellationToken.None);

        Check.That(await _repository.IsSessionEndedAsync("SESSION-H2", CancellationToken.None)).IsFalse();
        Check.That(await _repository.GetStartedAtAsync("SESSION-H2", CancellationToken.None)).IsNotNull();
    }

    [Fact]
    public async Task GetSessions_ShouldReturnSummaryWithCandidateCountAndTimestamps()
    {
        var sessionId = await _repository.CreateSessionAsync("SESSION-I", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "AAAA1111", "AAAA1111", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "BBBB2222", "BBBB2222", CancellationToken.None);
        await _repository.MarkStartedAsync("SESSION-I", CancellationToken.None);

        var sessions = await _repository.GetSessionsAsync(CancellationToken.None);
        var summary = sessions.Single(s => s.SessionCode == "SESSION-I");

        Check.That(summary.CandidateCount).IsEqualTo(2);
        Check.That(summary.StartedAt).IsNotNull();
        Check.That(summary.EndedAt).IsNull();
    }

    [Fact]
    public async Task GetCandidates_ShouldReturnNameAndConnectionHistory()
    {
        var sessionId = await _repository.CreateSessionAsync("SESSION-J", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "AAAA1111", "Léa Martin", CancellationToken.None);
        await _repository.AddCandidateAsync(sessionId, "BBBB2222", "Noah Dubois", CancellationToken.None);

        await _repository.MarkCandidateConnectedAsync("SESSION-J", "AAAA1111", CancellationToken.None);

        var candidates = await _repository.GetCandidatesAsync("SESSION-J", CancellationToken.None);

        var connected = candidates.Single(c => c.CandidateId == "AAAA1111");
        Check.That(connected.Name).IsEqualTo("Léa Martin");
        Check.That(connected.HasConnectedOnce).IsTrue();

        var neverConnected = candidates.Single(c => c.CandidateId == "BBBB2222");
        Check.That(neverConnected.Name).IsEqualTo("Noah Dubois");
        Check.That(neverConnected.HasConnectedOnce).IsFalse();
    }
}
