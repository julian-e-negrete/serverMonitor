using Microsoft.EntityFrameworkCore;
using ServerMonitor.Models;

namespace ServerMonitor.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Existing DbSets
        public DbSet<ServerStats> ServerStats { get; set; }
        public DbSet<NetworkConnection> NetworkConnections { get; set; }
        public DbSet<ProcessInfo> ProcessInfos { get; set; }
        public DbSet<ServiceStatus> ServiceStatuses { get; set; }
        public DbSet<PostgresStats> PostgresStats { get; set; }

        // New Financial Data DbSets
        public DbSet<MarketDataRecord> MarketDataRecords { get; set; }
        public DbSet<MepCalculation> MepCalculations { get; set; }
        public DbSet<FinancialRecord> FinancialRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Existing configurations...

            // MarketDataRecord configuration
            modelBuilder.Entity<MarketDataRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Instrument, e.Time });
                entity.HasIndex(e => e.Time);
                entity.HasIndex(e => e.Instrument);
                
                entity.Property(e => e.Instrument)
                    .HasMaxLength(100)
                    .IsRequired();
                    
                entity.Property(e => e.BidPrice)
                    .HasPrecision(18, 6);
                    
                entity.Property(e => e.AskPrice)
                    .HasPrecision(18, 6);
                    
                entity.Property(e => e.LastPrice)
                    .HasPrecision(18, 6);
                    
                entity.Property(e => e.TotalVolume)
                    .HasPrecision(18, 2);
            });

            // MepCalculation configuration
            modelBuilder.Entity<MepCalculation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Time);
                
                entity.Property(e => e.Al30Price)
                    .HasPrecision(18, 6);
                    
                entity.Property(e => e.Al30DPrice)
                    .HasPrecision(18, 6);
                    
                entity.Property(e => e.MepRate)
                    .HasPrecision(18, 4);
            });

            // FinancialRecord configuration
            modelBuilder.Entity<FinancialRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Type, e.RecordDate });
                entity.HasIndex(e => e.Instrument);
                entity.HasIndex(e => e.RecordDate);
                
                entity.Property(e => e.Instrument)
                    .HasMaxLength(100)
                    .IsRequired();
                    
                entity.Property(e => e.Type)
                    .HasMaxLength(50)
                    .IsRequired();
                    
                entity.Property(e => e.Value)
                    .HasPrecision(18, 4);
                    
                entity.Property(e => e.Rate)
                    .HasPrecision(18, 4);
                    
                entity.Property(e => e.Volume)
                    .HasPrecision(18, 4);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}