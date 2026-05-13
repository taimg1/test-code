using System.Net;
using System.Net.Http.Json;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.Api.Tests.Integration.Infrastructure;

namespace VotingSystem.Api.Tests.Integration.Controllers;

public class ElectionsControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ElectionsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
        await ElectionDbTestHelper.ClearAllAsync(db, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateElection_ReturnsCreatedResponse()
    {
        var request = ElectionIntegrationTestData.ValidCreateElectionDto(
            title: Guid.NewGuid().ToString("N"),
            description: Guid.NewGuid().ToString("N"));

        var response = await _client.PostAsJsonAsync("/api/elections", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var returnedElection = await response.Content.ReadFromJsonAsync<ElectionResponseDto>(TestContext.Current.CancellationToken);

        returnedElection.ShouldNotBeNull();
        returnedElection!.Title.ShouldBe(request.Title);
        returnedElection.Status.ShouldBe(ElectionStatus.Draft);
    }
}
