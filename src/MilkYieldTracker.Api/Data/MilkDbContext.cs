using Microsoft.EntityFrameworkCore;
using MilkYieldTracker.Api.Models;

namespace MilkYieldTracker.Api.Data;

public class MilkDbContext(DbContextOptions<MilkDbContext> options) : DbContext(options)
{
    public DbSet<Cow> Cows => Set<Cow>();
    public DbSet<MilkingSession> MilkingSessions => Set<MilkingSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cow>(cow =>
        {
            cow.Property(c => c.TagNumber).HasMaxLength(20).IsRequired();
            cow.Property(c => c.Name).HasMaxLength(50);

            // Two cows must never share an ear tag.
            cow.HasIndex(c => c.TagNumber).IsUnique();
        });

        modelBuilder.Entity<MilkingSession>(session =>
        {
            session.Property(s => s.Litres).HasPrecision(5, 2);

            // Store "Morning"/"Evening" as text so the table is readable in SSMS.
            session.Property(s => s.Period).HasConversion<string>().HasMaxLength(10);

            // One record per cow, per day, per milking. This stops double entry.
            session.HasIndex(s => new { s.CowId, s.Date, s.Period }).IsUnique();

            session.HasOne(s => s.Cow)
                .WithMany(c => c.Sessions)
                .HasForeignKey(s => s.CowId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
