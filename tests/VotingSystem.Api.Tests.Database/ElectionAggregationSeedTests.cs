using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.Data;

namespace VotingSystem.Api.Tests.Database;

public class ElectionAggregationSeedTests : IClassFixture<VotingDatabaseFixture>
{
    private readonly VotingDatabaseFixture _fixture;

    public ElectionAggregationSeedTests(VotingDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Seed_ShouldInsertAtLeastTargetVoteRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateDbContext();
        var count = await context.Votes.CountAsync(ct);
        count.ShouldBeGreaterThanOrEqualTo(VotingPerformanceSeed.TargetVoteRows);
    }

    [Fact]
    public async Task ActiveSingleChoiceElection_ShouldHaveAggregatedSingleChoiceVotes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateDbContext();

        var activeSingleId = await context.Elections
            .AsNoTracking()
            .Where(e => e.Status == ElectionStatus.Active && e.Type == ElectionType.SingleChoice)
            .Select(e => e.Id)
            .FirstAsync(ct);

        var perCandidate = await context.Votes
            .AsNoTracking()
            .Where(v => v.ElectionId == activeSingleId && v.Rank == null)
            .GroupBy(v => v.CandidateId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        perCandidate.Count.ShouldBeGreaterThan(0);
        perCandidate.Sum(x => x.Count).ShouldBeGreaterThan(0);

        var distinctVoters = await context.Votes
            .AsNoTracking()
            .Where(v => v.ElectionId == activeSingleId)
            .Select(v => v.VoterEmail)
            .Distinct()
            .CountAsync(ct);

        distinctVoters.ShouldBeGreaterThan(0);
    }
}
