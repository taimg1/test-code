using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_ResultsTests
{
    [Fact]
    public async Task GetResults_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).GetResultsAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ElectionStatus.Draft)]
    [InlineData(ElectionStatus.Active)]
    public async Task GetResults_NotClosed_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).GetResultsAsync(election.Id));
    }

    [Fact]
    public async Task GetResults_SingleChoice_CountsVotesPerCandidate()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "a@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "c@x.com", CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        result.Results.Single(r => r.CandidateId == c2.Id).Score.ShouldBe(2);
        result.Results.Single(r => r.CandidateId == c1.Id).Score.ShouldBe(1);
    }

    [Fact]
    public async Task GetResults_OrdersByScoreDescending()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "a@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "c@x.com", CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        result.Results.First().CandidateId.ShouldBe(c2.Id);
    }

    [Fact]
    public async Task GetResults_RankedChoice_AppliesBordaCount()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        var c3 = TestHelpers.BuildCandidate(election.Id, "C3");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        election.Candidates.Add(c3);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "v@x.com", Rank = 1, CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "v@x.com", Rank = 2, CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c3.Id, VoterEmail = "v@x.com", Rank = 3, CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        result.Results.Single(r => r.CandidateId == c1.Id).Score.ShouldBe(3);
        result.Results.Single(r => r.CandidateId == c2.Id).Score.ShouldBe(2);
        result.Results.Single(r => r.CandidateId == c3.Id).Score.ShouldBe(1);
    }

    [Fact]
    public async Task GetResults_NoVotes_ReturnsZeroScoreForAllCandidates()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "C1"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "C2"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        foreach (var r in result.Results)
            r.Score.ShouldBe(0);
    }
}
