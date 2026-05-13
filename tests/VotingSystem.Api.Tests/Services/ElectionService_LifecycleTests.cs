using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_LifecycleTests
{
    [Fact]
    public async Task OpenElection_FromDraft_ChangesStatusToActive()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "A"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "B"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id);

        var updated = await ctx.Elections.FindAsync(election.Id);
        updated!.Status.ShouldBe(ElectionStatus.Active);
    }

    [Fact]
    public async Task OpenElection_WithZeroCandidates_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
    }

    [Fact]
    public async Task OpenElection_WithOneCandidate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
    }

    [Theory]
    [InlineData(ElectionStatus.Active)]
    [InlineData(ElectionStatus.Closed)]
    public async Task OpenElection_NotDraft_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
    }

    [Fact]
    public async Task OpenElection_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ElectionStatus.Draft)]
    [InlineData(ElectionStatus.Active)]
    public async Task CloseElection_NotClosed_ChangesStatusToClosed(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateService(ctx).CloseElectionAsync(election.Id);

        var updated = await ctx.Elections.FindAsync(election.Id);
        updated!.Status.ShouldBe(ElectionStatus.Closed);
    }

    [Fact]
    public async Task CloseElection_AlreadyClosed_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).CloseElectionAsync(election.Id));
    }

    [Fact]
    public async Task CloseElection_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).CloseElectionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddCandidate_DraftElection_PersistsCandidate()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new CreateCandidateDto("Name", "Desc", "Party", null);
        var result = await TestHelpers.CreateService(ctx).AddCandidateAsync(election.Id, dto);

        result.Id.ShouldNotBe(Guid.Empty);
        ctx.Candidates.Count(c => c.ElectionId == election.Id).ShouldBe(1);
    }

    [Theory]
    [InlineData(ElectionStatus.Active)]
    [InlineData(ElectionStatus.Closed)]
    public async Task AddCandidate_NotDraft_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new CreateCandidateDto("N", "D", "P", null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).AddCandidateAsync(election.Id, dto));
    }

    [Fact]
    public async Task AddCandidate_NonExistentElection_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var dto = new CreateCandidateDto("N", "D", "P", null);

        await Should.ThrowAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).AddCandidateAsync(Guid.NewGuid(), dto));
    }
}
