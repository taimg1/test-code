using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.Api.Tests.Services;

internal static class ElectionServiceTestData
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
