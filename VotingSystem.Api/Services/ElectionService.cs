using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Data;

namespace VotingSystem.Api.Services;

public class ElectionService : IElectionService
{
    private readonly VotingDbContext _context;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private static readonly ElectionStatus?[] StatusVariants =
    {
        null,
        ElectionStatus.Draft,
        ElectionStatus.Active,
        ElectionStatus.Closed
    };

    public ElectionService(VotingDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private static string ListKey(ElectionStatus? status) =>
        $"elections:list:{status?.ToString() ?? "all"}";

    private static string ElectionKey(Guid id) => $"election:{id}";

    private static string ResultsKey(Guid id) => $"results:{id}";

    private void InvalidateListCache()
    {
        foreach (var s in StatusVariants)
            _cache.Remove(ListKey(s));
    }

    private void InvalidateElectionCache(Guid id)
    {
        _cache.Remove(ElectionKey(id));
        _cache.Remove(ResultsKey(id));
        InvalidateListCache();
    }

    public async Task<IEnumerable<ElectionResponseDto>> GetElectionsAsync(ElectionStatus? status)
    {
        var key = ListKey(status);
        var cached = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var query = _context.Elections.AsQueryable();
            if (status.HasValue) query = query.Where(e => e.Status == status);
            return await query
                .Select(e => new ElectionResponseDto(
                    e.Id, e.Title, e.Description, e.StartDate, e.EndDate, e.Status, e.Type,
                    e.Candidates.Select(c => new CandidateResponseDto(c.Id, c.Name, c.Description, c.Party, c.PhotoUrl))))
                .ToListAsync();
        });
        return cached!;
    }

    public async Task<ElectionResponseDto> CreateElectionAsync(CreateElectionDto request)
    {
        var election = new Election
        {
            Title = request.Title,
            Description = request.Description,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Type = request.Type,
            Status = ElectionStatus.Draft
        };

        _context.Elections.Add(election);
        await _context.SaveChangesAsync();

        InvalidateListCache();

        return new ElectionResponseDto(
            election.Id, election.Title, election.Description, election.StartDate, election.EndDate, election.Status, election.Type,
            new List<CandidateResponseDto>());
    }

    public async Task<ElectionResponseDto> GetElectionAsync(Guid id)
    {
        var key = ElectionKey(id);
        if (_cache.TryGetValue(key, out ElectionResponseDto? cached) && cached is not null)
            return cached;

        var election = await _context.Elections
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        var dto = new ElectionResponseDto(
            election.Id, election.Title, election.Description, election.StartDate, election.EndDate, election.Status, election.Type,
            election.Candidates.Select(c => new CandidateResponseDto(c.Id, c.Name, c.Description, c.Party, c.PhotoUrl)));

        _cache.Set(key, dto, CacheTtl);
        return dto;
    }

    public async Task<CandidateResponseDto> AddCandidateAsync(Guid electionId, CreateCandidateDto request)
    {
        var election = await _context.Elections.FindAsync(electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        if (election.Status != ElectionStatus.Draft)
            throw new InvalidOperationException("Кандидатів можна додавати лише до виборів у статусі Draft.");

        var candidate = new Candidate
        {
            ElectionId = electionId,
            Name = request.Name,
            Description = request.Description,
            Party = request.Party,
            PhotoUrl = request.PhotoUrl
        };

        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync();

        InvalidateElectionCache(electionId);

        return new CandidateResponseDto(candidate.Id, candidate.Name, candidate.Description, candidate.Party, candidate.PhotoUrl);
    }

    public async Task OpenElectionAsync(Guid electionId)
    {
        var election = await _context.Elections
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.Id == electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        if (election.Status != ElectionStatus.Draft)
            throw new InvalidOperationException("Відкрити можна лише вибори у статусі Draft.");

        if (election.Candidates.Count < 2)
            throw new InvalidOperationException("Щоб відкрити вибори, потрібно щонайменше двох кандидатів.");

        election.Status = ElectionStatus.Active;
        await _context.SaveChangesAsync();

        InvalidateElectionCache(electionId);
    }

    public async Task CloseElectionAsync(Guid electionId)
    {
        var election = await _context.Elections.FindAsync(electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        if (election.Status == ElectionStatus.Closed)
            throw new InvalidOperationException("Вибори вже закриті.");

        election.Status = ElectionStatus.Closed;
        await _context.SaveChangesAsync();

        InvalidateElectionCache(electionId);
    }

    public async Task VoteAsync(Guid electionId, SubmitVoteDto request)
    {
        var election = await _context.Elections.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.Id == electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        if (election.Status != ElectionStatus.Active)
            throw new InvalidOperationException("Голосувати можна тільки під час активного періоду виборів.");

        var now = DateTime.UtcNow;
        if (now < election.StartDate || now > election.EndDate)
            throw new InvalidOperationException("Голосування можливе лише в межах періоду виборів (UTC).");

        var hasVoted = await _context.Votes.AnyAsync(v => v.ElectionId == electionId && v.VoterEmail == request.VoterEmail);
        if (hasVoted)
            throw new InvalidOperationException("Ви вже проголосували на цих виборах.");

        if (election.Type == ElectionType.SingleChoice)
        {
            if (request.Votes.Count != 1) throw new InvalidOperationException("Для цих виборів потрібно обрати рівно одного кандидата.");
            var candidateId = request.Votes.First().CandidateId;
            if (!election.Candidates.Any(c => c.Id == candidateId)) throw new InvalidOperationException("Кандидата не знайдено.");

            _context.Votes.Add(new Vote { ElectionId = electionId, VoterEmail = request.VoterEmail, CandidateId = candidateId, CastAt = DateTime.UtcNow });
        }
        else if (election.Type == ElectionType.RankedChoice)
        {
            var candidateIds = election.Candidates.Select(c => c.Id).ToList();
            if (request.Votes.Count != candidateIds.Count) throw new InvalidOperationException("Потрібно проранжувати всіх кандидатів.");

            var submittedCandidateIds = request.Votes.Select(v => v.CandidateId).ToList();
            if (submittedCandidateIds.Distinct().Count() != candidateIds.Count || !submittedCandidateIds.All(cid => candidateIds.Contains(cid)))
                throw new InvalidOperationException("Неправильні кандидати для ранжування.");

            var allRanks = request.Votes.Select(v => v.Rank ?? 0).ToList();
            if (allRanks.Distinct().Count() != candidateIds.Count || allRanks.Any(r => r <= 0 || r > candidateIds.Count))
                throw new InvalidOperationException("Недійсні ранги: ранги повинні бути унікальними та від 1 до N.");

            foreach (var voteItem in request.Votes)
            {
                _context.Votes.Add(new Vote { ElectionId = electionId, VoterEmail = request.VoterEmail, CandidateId = voteItem.CandidateId, Rank = voteItem.Rank!.Value, CastAt = DateTime.UtcNow });
            }
        }

        await _context.SaveChangesAsync();

        _cache.Remove(ResultsKey(electionId));
    }

    public async Task<ElectionResultDto> GetResultsAsync(Guid electionId)
    {
        var key = ResultsKey(electionId);
        if (_cache.TryGetValue(key, out ElectionResultDto? cached) && cached is not null)
            return cached;

        var election = await _context.Elections.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.Id == electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        if (election.Status != ElectionStatus.Closed)
            throw new InvalidOperationException("Результати видимі тільки після закриття виборів.");

        var votes = await _context.Votes.Where(v => v.ElectionId == electionId).ToListAsync();
        var results = new List<CandidateResultDto>();

        if (election.Type == ElectionType.SingleChoice)
        {
            var grouped = votes.GroupBy(v => v.CandidateId).ToDictionary(g => g.Key, g => g.Count());
            results = election.Candidates.Select(c => new CandidateResultDto(c.Id, c.Name, grouped.GetValueOrDefault(c.Id, 0))).ToList();
        }
        else if (election.Type == ElectionType.RankedChoice)
        {
            var n = election.Candidates.Count;
            var grouped = votes.GroupBy(v => v.CandidateId).ToDictionary(g => g.Key, g => g.Sum(v => n - (v.Rank ?? 0) + 1));
            results = election.Candidates.Select(c => new CandidateResultDto(c.Id, c.Name, grouped.GetValueOrDefault(c.Id, 0))).ToList();
        }

        var dto = new ElectionResultDto(electionId, results.OrderByDescending(r => r.Score).ToList());
        _cache.Set(key, dto, CacheTtl);
        return dto;
    }

    public async Task<TurnoutResponseDto> GetTurnoutAsync(Guid electionId)
    {
        var election = await _context.Elections.FindAsync(electionId);
        if (election == null) throw new KeyNotFoundException("Вибори не знайдено.");

        var totalVoters = await _context.Votes
            .Where(v => v.ElectionId == electionId)
            .Select(v => v.VoterEmail)
            .Distinct()
            .CountAsync();

        return new TurnoutResponseDto(electionId, totalVoters);
    }
}
