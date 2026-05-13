using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Data;
using VotingSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddMemoryCache();

// Add services to the container.
builder.Services.AddScoped<IElectionService, ElectionService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<VotingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<VotingDbContext>("database");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
    if (app.Environment.EnvironmentName != "Testing")
        await context.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// k6/CI часто ходять лише по HTTP; редірект на HTTPS дає зірвані клієнти без TLS на тому ж порту.
var disableHttpsRedirect = string.Equals(
    Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT"),
    "true",
    StringComparison.OrdinalIgnoreCase);
if (!disableHttpsRedirect)
    app.UseHttpsRedirection();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
});

app.Run();

public partial class Program { }

