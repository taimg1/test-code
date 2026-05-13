using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Entities;

namespace VotingSystem.Api.Data;

public class VotingDbContext : DbContext
{
    public VotingDbContext(DbContextOptions<VotingDbContext> options) : base(options)
    {
    }

    public DbSet<Election> Elections { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Vote> Votes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Election>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            
            entity.HasMany(e => e.Candidates)
                  .WithOne(c => c.Election)
                  .HasForeignKey(c => c.ElectionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Votes)
                  .WithOne(v => v.Election)
                  .HasForeignKey(v => v.ElectionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Candidate Configuration
        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(500);
            entity.Property(c => c.Party).IsRequired().HasMaxLength(100);
            
            entity.HasMany(c => c.Votes)
                  .WithOne(v => v.Candidate)
                  .HasForeignKey(v => v.CandidateId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.VoterEmail).IsRequired().HasMaxLength(150);
            entity.HasIndex(v => new { v.ElectionId, v.VoterEmail, v.CandidateId }).IsUnique();
        });
    }
}
