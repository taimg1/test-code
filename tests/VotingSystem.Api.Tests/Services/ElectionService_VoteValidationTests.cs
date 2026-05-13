using System.Linq;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_VoteValidationTests
{
    [Fact]
    public async Task Vote_NonExistentElection_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(Guid.NewGuid(), null) });

        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(Guid.NewGuid(), dto));
    }

    [Theory]
    [InlineData(ElectionStatus.Draft)]
    [InlineData(ElectionStatus.Closed)]
    public async Task Vote_ElectionNotActive_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(election.Candidates.First().Id, null) });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_SameVoterTwice_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var sut = TestHelpers.CreateService(ctx);
        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(election.Candidates.First().Id, null) });

        await sut.VoteAsync(election.Id, dto);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.VoteAsync(election.Id, dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Vote_SingleChoice_WrongVoteCount_Throws(int voteCount)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.SingleChoice);
        for (var i = 0; i < 3; i++)
            election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, $"C{i}"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var votes = election.Candidates.Take(voteCount)
            .Select(c => new VoteItemDto(c.Id, null)).ToList();
        var dto = new SubmitVoteDto("voter@x.com", votes);

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_SingleChoice_UnknownCandidate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.SingleChoice);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(Guid.NewGuid(), null) });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_SingleChoice_Valid_PersistsOneVote()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.SingleChoice);
        var candidate = TestHelpers.BuildCandidate(election.Id, "A");
        election.Candidates.Add(candidate);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(candidate.Id, null) });
        await TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto);

        ctx.Votes.Count(v => v.ElectionId == election.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Vote_RankedChoice_WrongCandidateCount_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(c1.Id, 1) });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_RankedChoice_DuplicateCandidates_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new()
        {
            new VoteItemDto(c1.Id, 1),
            new VoteItemDto(c1.Id, 2)
        });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Vote_RankedChoice_RankOutOfRange_Throws(int badRank)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        var c3 = TestHelpers.BuildCandidate(election.Id, "C3");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        election.Candidates.Add(c3);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new()
        {
            new VoteItemDto(c1.Id, 1),
            new VoteItemDto(c2.Id, 2),
            new VoteItemDto(c3.Id, badRank)
        });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_RankedChoice_DuplicateRanks_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new()
        {
            new VoteItemDto(c1.Id, 1),
            new VoteItemDto(c2.Id, 1)
        });

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_RankedChoice_Valid_PersistsAllRankedVotes()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        var c3 = TestHelpers.BuildCandidate(election.Id, "C3");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        election.Candidates.Add(c3);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new()
        {
            new VoteItemDto(c1.Id, 1),
            new VoteItemDto(c2.Id, 2),
            new VoteItemDto(c3.Id, 3)
        });

        await TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto);

        ctx.Votes.Count(v => v.ElectionId == election.Id).ShouldBe(3);
    }

    [Fact]
    public async Task Vote_ActiveElection_BeforeStartDate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.SingleChoice);
        election.StartDate = DateTime.UtcNow.AddDays(1);
        election.EndDate = DateTime.UtcNow.AddDays(2);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(election.Candidates.First().Id, null) });

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
        ex.Message.ShouldContain("UTC");
    }

    [Fact]
    public async Task Vote_ActiveElection_AfterEndDate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active, ElectionType.SingleChoice);
        election.StartDate = DateTime.UtcNow.AddDays(-10);
        election.EndDate = DateTime.UtcNow.AddDays(-1);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(election.Candidates.First().Id, null) });

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
        ex.Message.ShouldContain("UTC");
    }
}
