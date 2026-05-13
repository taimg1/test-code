using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
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

        await VotingPerformanceSeed.SeedAsync(db, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
