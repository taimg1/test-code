using AutoFixture;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.Api.Tests.Services;

public class ElectionService_CreateTests
{
    private static readonly Fixture Fixture = new();

    [Fact]
    public async Task CreateElection_WithAutoFixtureStrings_PersistsTitleAndDescription()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var title = Fixture.Create<string>();
        var description = Fixture.Create<string>();
        var dto = new CreateElectionDto(title, description, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        result.Title.ShouldBe(title);
        result.Description.ShouldBe(description);
    }

    [Fact]
    public async Task CreateElection_SetsStatusToDraft()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = ElectionServiceTestData.ValidCreateElectionDto(
            title: Guid.NewGuid().ToString("N"),
            description: Guid.NewGuid().ToString("N"));

        var result = await sut.CreateElectionAsync(dto);

        result.Status.ShouldBe(ElectionStatus.Draft);
    }

    [Fact]
    public async Task CreateElection_AssignsNonEmptyId()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateElection_ConvertsDatesToUtc()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var local = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var dto = new CreateElectionDto("T", "D", local, local.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        result.StartDate.Kind.ShouldBe(DateTimeKind.Utc);
        result.EndDate.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(ElectionType.SingleChoice)]
    [InlineData(ElectionType.RankedChoice)]
    public async Task CreateElection_PreservesType(ElectionType type)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), type);

        var result = await sut.CreateElectionAsync(dto);

        result.Type.ShouldBe(type);
    }

    [Fact]
    public async Task CreateElection_ReturnsEmptyCandidates()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateElection_PersistsToDatabase()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("Persisted", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);
        await sut.CreateElectionAsync(dto);

        ctx.Elections.Count().ShouldBe(1);
    }
}
