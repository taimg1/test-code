using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace VotingSystem.Api.Tests.Database;

public class DatabaseConstraintsTests : IClassFixture<VotingDatabaseFixture>
{
    private readonly VotingDatabaseFixture _fixture;

    public DatabaseConstraintsTests(VotingDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Vote_UniqueIndex_ShouldPreventMultipleVotesBySameUserForSaveGivenElectionAndCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateDbContext();

        var electionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var election = new Election
        {
            Id = electionId,
            Title = "Test",
            Description = "Test",
            Status = ElectionStatus.Active,
            Type = ElectionType.SingleChoice
        };
        var candidate = new Candidate
        {
            Id = candidateId,
            ElectionId = electionId,
            Name = "Name",
            Description = "Desc",
            Party = "Party"
        };

        context.Elections.Add(election);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync(ct);

        var vote1 = new Vote
        {
            ElectionId = electionId,
            CandidateId = candidateId,
            VoterEmail = "duplicate@test.com",
            CastAt = DateTime.UtcNow
        };
        var vote2 = new Vote
        {
            ElectionId = electionId,
            CandidateId = candidateId,
            VoterEmail = "duplicate@test.com",
            CastAt = DateTime.UtcNow
        };

        context.Votes.Add(vote1);
        await context.SaveChangesAsync(ct);

        context.Votes.Add(vote2);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(ct));
        (ex.InnerException?.Message ?? ex.Message).ShouldContain("duplicate key value violates unique constraint");
    }

    [Fact]
    public async Task ElectionDelete_ShouldCascadeDeleteCandidatesAndVotes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateDbContext();

        var electionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var election = new Election
        {
            Id = electionId,
            Title = "Cascade",
            Description = "Test",
            Status = ElectionStatus.Draft,
            Type = ElectionType.SingleChoice
        };
        var candidate = new Candidate
        {
            Id = candidateId,
            ElectionId = electionId,
            Name = "Name",
            Description = "Desc",
            Party = "Party"
        };

        context.Elections.Add(election);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync(ct);

        context.Votes.Add(new Vote { ElectionId = electionId, CandidateId = candidateId, VoterEmail = "voter@test.com" });
        await context.SaveChangesAsync(ct);

        await context.Elections.Where(e => e.Id == electionId).ExecuteDeleteAsync(ct);

        var isCandidateExists = await context.Candidates.AnyAsync(c => c.Id == candidateId, ct);
        var isVoteExists = await context.Votes.AnyAsync(v => v.ElectionId == electionId, ct);

        isCandidateExists.ShouldBeFalse();
        isVoteExists.ShouldBeFalse();
    }
}
