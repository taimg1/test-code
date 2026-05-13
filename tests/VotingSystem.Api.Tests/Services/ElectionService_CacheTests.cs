using Microsoft.Extensions.Caching.Memory;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Services;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_CacheTests
{
    [Fact]
    public async Task GetElection_SecondCall_HitsCache_NotDb()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection();
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        await sut.GetElectionAsync(election.Id);

        ctx.Elections.Remove(election);
        await ctx.SaveChangesAsync();

        var second = await sut.GetElectionAsync(election.Id);

        second.Id.ShouldBe(election.Id);
    }

    [Fact]
    public async Task CreateElection_InvalidatesListCache()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        var before = await sut.GetElectionsAsync(null);
        before.ShouldBeEmpty();

        await sut.CreateElectionAsync(ElectionServiceTestData.ValidCreateElectionDto());

        var after = await sut.GetElectionsAsync(null);
        after.Count().ShouldBe(1);
    }

    [Fact]
    public async Task GetResults_SecondCall_ReturnsCachedScore()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var first = await sut.GetResultsAsync(election.Id);
        first.Results.Single().Score.ShouldBe(0);

        ctx.Votes.Add(new Vote
        {
            ElectionId = election.Id,
            CandidateId = candidate.Id,
            VoterEmail = "x@x.com",
            CastAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var second = await sut.GetResultsAsync(election.Id);
        second.Results.Single().Score.ShouldBe(0);
    }
}
