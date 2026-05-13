using Bogus;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.Data;

namespace VotingSystem.Api.Tests.Database;

public class VotingDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .Build();

    public VotingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<VotingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new VotingDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync(CancellationToken.None);

        await SeedDataAsync(db, CancellationToken.None);
    }

    private static async Task SeedDataAsync(VotingDbContext db, CancellationToken cancellationToken)
    {
        var faker = new Faker();
        var random = new Random(42);
        const int batchSize = 500;
        const int totalElections = 20;
        const int targetVoteRows = 9850;

        var electionFaker = new Faker<Election>()
            .RuleFor(e => e.Id, _ => Guid.NewGuid())
            .RuleFor(e => e.Title, f => f.Company.CatchPhrase())
            .RuleFor(e => e.Description, f => f.Lorem.Paragraph())
            .RuleFor(e => e.StartDate, f => f.Date.Past(1).ToUniversalTime())
            .RuleFor(e => e.EndDate, f => f.Date.Future(1).ToUniversalTime())
            .RuleFor(e => e.Status, f => f.PickRandom<ElectionStatus>())
            .RuleFor(e => e.Type, f => f.PickRandom<ElectionType>());

        var elections = electionFaker.Generate(totalElections);

        elections[0].Status = ElectionStatus.Active;
        elections[0].StartDate = DateTime.UtcNow.AddDays(-1);
        elections[0].EndDate = DateTime.UtcNow.AddDays(5);
        elections[0].Type = ElectionType.SingleChoice;

        elections[1].Status = ElectionStatus.Closed;
        elections[1].Type = ElectionType.RankedChoice;

        db.Elections.AddRange(elections);
        await db.SaveChangesAsync(cancellationToken);

        var candidateFaker = new Faker<Candidate>()
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Name, f => f.Name.FullName())
            .RuleFor(c => c.Description, f => f.Lorem.Sentence())
            .RuleFor(c => c.Party, f => f.Company.CompanyName())
            .RuleFor(c => c.PhotoUrl, f => f.Image.PicsumUrl());

        var allCandidates = new List<Candidate>();
        foreach (var election in elections)
        {
            var count = random.Next(4, 11);
            var electionCandidates = candidateFaker.Generate(count);
            foreach (var c in electionCandidates)
                c.ElectionId = election.Id;
            allCandidates.AddRange(electionCandidates);
        }

        for (var batch = 0; batch < allCandidates.Count; batch += batchSize)
        {
            var chunk = allCandidates.Skip(batch).Take(batchSize).ToList();
            db.Candidates.AddRange(chunk);
            await db.SaveChangesAsync(cancellationToken);
        }

        var candidatesByElection = allCandidates
            .GroupBy(c => c.ElectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var votes = new List<Vote>(targetVoteRows);
        var voteKeys = new HashSet<(Guid ElectionId, string EmailLower, Guid CandidateId)>();

        for (var i = 0; i < targetVoteRows;)
        {
            var election = elections[random.Next(0, elections.Count)];
            if (!candidatesByElection.TryGetValue(election.Id, out var electionCands) || electionCands.Count == 0)
                continue;

            var canDoRankedBlock = election.Type == ElectionType.RankedChoice
                && i + electionCands.Count <= targetVoteRows;

            if (canDoRankedBlock)
            {
                var shuffled = electionCands.OrderBy(_ => random.Next()).ToList();
                var email = $"voter-{Guid.NewGuid():N}@example.com";
                for (var rank = 1; rank <= shuffled.Count; rank++)
                {
                    var cand = shuffled[rank - 1];
                    voteKeys.Add((election.Id, email.ToLowerInvariant(), cand.Id));
                    votes.Add(new Vote
                    {
                        Id = Guid.NewGuid(),
                        ElectionId = election.Id,
                        CandidateId = cand.Id,
                        VoterEmail = email,
                        CastAt = faker.Date.Between(election.StartDate, election.EndDate).ToUniversalTime(),
                        Rank = rank
                    });
                }

                i += shuffled.Count;
            }
            else
            {
                var candidate = electionCands[random.Next(electionCands.Count)];
                var email = $"voter-{Guid.NewGuid():N}@example.com";
                var key = (election.Id, email.ToLowerInvariant(), candidate.Id);
                if (!voteKeys.Add(key))
                    continue;

                votes.Add(new Vote
                {
                    Id = Guid.NewGuid(),
                    ElectionId = election.Id,
                    CandidateId = candidate.Id,
                    VoterEmail = email,
                    CastAt = faker.Date.Between(election.StartDate, election.EndDate).ToUniversalTime(),
                    Rank = null
                });
                i++;
            }

            if (votes.Count >= batchSize)
            {
                db.Votes.AddRange(votes);
                await db.SaveChangesAsync(cancellationToken);
                votes.Clear();
            }
        }

        if (votes.Count > 0)
        {
            db.Votes.AddRange(votes);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
