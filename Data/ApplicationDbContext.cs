using Microsoft.EntityFrameworkCore;
using ServerMonitor.Models;

namespace ServerMonitor.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServerStats> ServerStats => Set<ServerStats>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<PostgresStats> PostgresStats => Set<PostgresStats>(); // Add this if you want to store PostgreSQL stats

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure PostgreSQL-specific settings if needed
        modelBuilder.Entity<ServerStats>()
            .Property(s => s.Timestamp)
            .HasDefaultValueSql("NOW()");

        modelBuilder.Entity<Alert>()
            .Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()");
    }
}