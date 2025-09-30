using Microsoft.EntityFrameworkCore;
using ServerMonitor.Models;

namespace ServerMonitor.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServerStats> ServerStats => Set<ServerStats>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Simple configuration that works with any database
        modelBuilder.Entity<ServerStats>()
            .Property(s => s.Timestamp)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}