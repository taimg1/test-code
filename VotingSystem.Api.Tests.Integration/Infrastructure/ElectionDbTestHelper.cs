using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Data;

namespace VotingSystem.Api.Tests.Integration.Infrastructure;

internal static class ElectionDbTestHelper
{
    public static async Task ClearAllAsync(VotingDbContext db, CancellationToken cancellationToken = default)
    {
        db.Votes.RemoveRange(db.Votes);
        db.Candidates.RemoveRange(db.Candidates);
        db.Elections.RemoveRange(db.Elections);
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class ElectionIntegrationTestData
{
    public static CreateElectionDto ValidCreateElectionDto(
        string? title = null,
        string? description = null,
        DateTime? start = null,
        DateTime? end = null,
        ElectionType type = ElectionType.SingleChoice) =>
        new(
            title ?? "Election title",
            description ?? "Election description",
            start ?? DateTime.UtcNow,
            end ?? DateTime.UtcNow.AddDays(7),
            type);
}
