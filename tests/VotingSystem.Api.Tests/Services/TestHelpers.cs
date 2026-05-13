using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.Data;
using VotingSystem.Api.Services;

namespace VotingSystem.Api.Tests.Services;

internal static class TestHelpers
{
    public static VotingDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VotingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new VotingDbContext(options);
    }

    public static IMemoryCache CreateCache() =>
        new MemoryCache(new MemoryCacheOptions());

    public static ElectionService CreateService(VotingDbContext context) =>
        new(context, CreateCache());

    public static Election BuildElection(
        ElectionStatus status = ElectionStatus.Active,
        ElectionType type = ElectionType.SingleChoice,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Test Election",
        Description = "Test Description",
        StartDate = DateTime.UtcNow.AddDays(-1),
        EndDate = DateTime.UtcNow.AddDays(1),
        Status = status,
        Type = type
    };

    public static Candidate BuildCandidate(Guid electionId, string name = "C") => new()
    {
        Id = Guid.NewGuid(),
        ElectionId = electionId,
        Name = name,
        Description = $"{name}-desc",
        Party = $"{name}-party"
    };
}
