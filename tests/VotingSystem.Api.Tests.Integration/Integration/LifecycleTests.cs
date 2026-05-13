using System.Net;
using System.Net.Http.Json;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.Api.Tests.Integration.Infrastructure;

namespace VotingSystem.Api.Tests.Integration.Integration;

public class LifecycleTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public LifecycleTests(CustomWebApplicationFactory factory)
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
    public async Task FullVotingLifecycle_ShouldSucceed()
    {
        var createElectionDto = ElectionIntegrationTestData.ValidCreateElectionDto(
            title: "Presidential",
            description: "Decide the president",
            start: DateTime.UtcNow,
            end: DateTime.UtcNow.AddDays(5));

        var r1 = await _client.PostAsJsonAsync("/api/elections", createElectionDto, TestContext.Current.CancellationToken);
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        var election = await r1.Content.ReadFromJsonAsync<ElectionResponseDto>(TestContext.Current.CancellationToken);

        election.ShouldNotBeNull();
        var electionId = election!.Id;

        var c1 = new CreateCandidateDto("Alice", "D", "P", null);
        var c2 = new CreateCandidateDto("Bob", "D", "P", null);

        var rC1 = await _client.PostAsJsonAsync($"/api/elections/{electionId}/candidates", c1, TestContext.Current.CancellationToken);
        rC1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var candidate1 = await rC1.Content.ReadFromJsonAsync<CandidateResponseDto>(TestContext.Current.CancellationToken);

        var rC2 = await _client.PostAsJsonAsync($"/api/elections/{electionId}/candidates", c2, TestContext.Current.CancellationToken);
        var candidate2 = await rC2.Content.ReadFromJsonAsync<CandidateResponseDto>(TestContext.Current.CancellationToken);

        var voteRequest = new SubmitVoteDto("voter@test.com", new List<VoteItemDto> { new(candidate1!.Id, null) });
        var rVoteFail = await _client.PostAsJsonAsync($"/api/elections/{electionId}/vote", voteRequest, TestContext.Current.CancellationToken);
        rVoteFail.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
            var dbElection = await db.Elections.FindAsync([electionId], TestContext.Current.CancellationToken);
            if (dbElection != null)
            {
                dbElection.Status = ElectionStatus.Active;
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var rVoteSuccess = await _client.PostAsJsonAsync($"/api/elections/{electionId}/vote", voteRequest, TestContext.Current.CancellationToken);
        rVoteSuccess.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rResFail = await _client.GetAsync($"/api/elections/{electionId}/results", TestContext.Current.CancellationToken);
        rResFail.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var rClose = await _client.PatchAsync($"/api/elections/{electionId}/close", null, TestContext.Current.CancellationToken);
        rClose.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rResSuccess = await _client.GetAsync($"/api/elections/{electionId}/results", TestContext.Current.CancellationToken);
        rResSuccess.StatusCode.ShouldBe(HttpStatusCode.OK);

        var results = await rResSuccess.Content.ReadFromJsonAsync<ElectionResultDto>(TestContext.Current.CancellationToken);
        results.ShouldNotBeNull();
        results!.Results.First().CandidateId.ShouldBe(candidate1.Id);
        results.Results.First().Score.ShouldBe(1);
    }
}
