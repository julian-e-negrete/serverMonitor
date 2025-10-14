using Microsoft.EntityFrameworkCore;
using ServerMonitor.Data;
using ServerMonitor.Models;

namespace ServerMonitor.Services
{
    public class FinancialDataService : IFinancialDataService
    {
        private readonly ILogger<FinancialDataService> _logger;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public FinancialDataService(ILogger<FinancialDataService> logger, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public async Task SaveMarketDataAsync(MarketDataRecord record)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.MarketDataRecords.Add(record);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving market data for {Instrument}", record.Instrument);
                throw;
            }
        }

        public async Task<List<MarketDataRecord>> GetLatestMarketDataAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                // Get latest record for each instrument
                var instruments = new[] { "bm_MERV_AL30_24hs", "bm_MERV_AL30D_24hs", "rx_DDF_DLR_OCT25", "bm_MERV_PESOS_1D" };
                var results = new List<MarketDataRecord>();
                
                foreach (var instrument in instruments)
                {
                    var latest = await context.MarketDataRecords
                        .Where(r => r.Instrument == instrument)
                        .OrderByDescending(r => r.Time)
                        .FirstOrDefaultAsync();
                        
                    if (latest != null)
                        results.Add(latest);
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest market data");
                return new List<MarketDataRecord>();
            }
        }

        public async Task<MarketDataRecord?> GetLatestInstrumentDataAsync(string instrument)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MarketDataRecords
                    .Where(r => r.Instrument == instrument)
                    .OrderByDescending(r => r.Time)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest data for {Instrument}", instrument);
                return null;
            }
        }

        public async Task SaveMepCalculationAsync(MepCalculation mepCalculation)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.MepCalculations.Add(mepCalculation);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving MEP calculation");
                throw;
            }
        }

        public async Task<MepCalculation?> GetLatestMepCalculationAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MepCalculations
                    .OrderByDescending(m => m.Time)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest MEP calculation");
                return null;
            }
        }

        public async Task<List<MepCalculation>> GetMepHistoryAsync(DateTime fromDate, DateTime toDate, int maxRecords = 100)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MepCalculations
                    .Where(m => m.Time >= fromDate && m.Time <= toDate)
                    .OrderByDescending(m => m.Time)
                    .Take(maxRecords)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving MEP history");
                return new List<MepCalculation>();
            }
        }

        public async Task<List<CauctionData>> GetCauctionDataAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var latestRecords = await context.FinancialRecords
                    .Where(r => r.Type == "Cauction")
                    .GroupBy(r => r.Instrument)
                    .Select(g => g.OrderByDescending(r => r.RecordDate).FirstOrDefault())
                    .ToListAsync();

                return latestRecords.Where(r => r != null).Select(r => new CauctionData
                {
                    Description = r.Instrument,
                    Rate = r.Rate,
                    Volume = FormatVolume(r.Volume),
                    RawVolume = (double)r.Volume,
                    Variation = $"{r.Variation}%",
                    RawVariation = r.Variation,
                    Timestamp = r.RecordDate
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cauction data");
                return new List<CauctionData>();
            }
        }

        public async Task SaveCauctionDataAsync(List<FinancialRecord> records)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.FinancialRecords.AddRange(records);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cauction data");
                throw;
            }
        }

        public async Task<bool> IsFinancialDataAvailableAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.MarketDataRecords.AnyAsync() || 
                       await context.MepCalculations.AnyAsync() || 
                       await context.FinancialRecords.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking financial data availability");
                return false;
            }
        }

        private static string FormatVolume(decimal volume)
        {
            var vol = (double)volume;
            if (vol >= 1_000_000_000_000)
                return $"{(vol / 1_000_000_000_000):0.##} trillion";
            if (vol >= 1_000_000_000)
                return $"{(vol / 1_000_000_000):0.##} billion";
            if (vol >= 1_000_000)
                return $"{(vol / 1_000_000):0.##} million";
            
            return vol.ToString("N0");
        }
    }
}