using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_QueryTests
{
    [Fact]
    public async Task GetElection_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).GetElectionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetElection_Existing_ReturnsCorrectId()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection();
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetElectionAsync(election.Id);

        result.Id.ShouldBe(election.Id);
    }

    [Fact]
    public async Task GetElections_NoFilter_ReturnsAll()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Draft));
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Active));
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Closed));
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetElectionsAsync(null);

        result.Count().ShouldBe(3);
    }

    [Theory]
    [InlineData(ElectionStatus.Draft, 2)]
    [InlineData(ElectionStatus.Active, 1)]
    [InlineData(ElectionStatus.Closed, 0)]
    public async Task GetElections_FilterByStatus_ReturnsMatching(ElectionStatus filter, int expected)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Draft));
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Draft));
        ctx.Elections.Add(TestHelpers.BuildElection(ElectionStatus.Active));
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetElectionsAsync(filter);

        result.Count().ShouldBe(expected);
    }

    [Fact]
    public async Task GetTurnout_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).GetTurnoutAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTurnout_NoVotes_ReturnsZero()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection();
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetTurnoutAsync(election.Id);

        result.TotalVoters.ShouldBe(0);
    }

    [Fact]
    public async Task GetTurnout_ReturnsDistinctVoterCount()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection();
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = Guid.NewGuid(), VoterEmail = "a@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = Guid.NewGuid(), VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = Guid.NewGuid(), VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetTurnoutAsync(election.Id);

        result.TotalVoters.ShouldBe(2);
    }
}
