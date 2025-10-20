using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using ServerMonitor.Models;
using System.Data;

namespace ServerMonitor.Services
{
    public class FinancialDataService : IFinancialDataService
    {
        private readonly ILogger<FinancialDataService> _logger;
        private readonly string _connectionString;

        public FinancialDataService(ILogger<FinancialDataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException("DefaultConnection");
        }

        public async Task SaveMarketDataAsync(MarketDataRecord record)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"INSERT INTO ""MarketDataRecords"" (""Time"",""Instrument"",""BidVolume"",""BidPrice"",""AskPrice"",""AskVolume"",""LastPrice"",""TotalVolume"",""Low"",""High"",""PrevClose"",""CreatedAt"")
                            VALUES (@Time,@Instrument,@BidVolume,@BidPrice,@AskPrice,@AskVolume,@LastPrice,@TotalVolume,@Low,@High,@PrevClose,@CreatedAt);";
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                await conn.ExecuteAsync(sql, record);
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
                var instruments = new[] { "bm_MERV_AL30_24hs", "bm_MERV_AL30D_24hs", "rx_DDF_DLR_OCT25", "bm_MERV_PESOS_1D" };
                var results = new List<MarketDataRecord>();

                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                foreach (var instrument in instruments)
                {
                    var sql = @"SELECT * FROM ""MarketDataRecords"" WHERE ""Instrument"" = @Instrument ORDER BY ""Time"" DESC LIMIT 1";
                    var latest = await conn.QueryFirstOrDefaultAsync<MarketDataRecord>(sql, new { Instrument = instrument });
                    if (latest != null) results.Add(latest);
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                var sql = @"SELECT * FROM ""MarketDataRecords"" WHERE ""Instrument"" = @Instrument ORDER BY ""Time"" DESC LIMIT 1";
                return await conn.QueryFirstOrDefaultAsync<MarketDataRecord>(sql, new { Instrument = instrument });
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
                await using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"INSERT INTO ""MepCalculations"" (""Time"",""Al30Price"",""Al30DPrice"",""MepRate"",""CreatedAt"")
                            VALUES (@Time,@Al30Price,@Al30DPrice,@MepRate,@CreatedAt);";
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                await conn.ExecuteAsync(sql, mepCalculation);
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                var sql = @"SELECT * FROM ""MepCalculations"" ORDER BY ""Time"" DESC LIMIT 1";
                return await conn.QueryFirstOrDefaultAsync<MepCalculation>(sql);
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                var sql = @"SELECT * FROM ""MepCalculations"" WHERE ""Time"" >= @FromDate AND ""Time"" <= @ToDate ORDER BY ""Time"" DESC LIMIT @MaxRecords";
                return (await conn.QueryAsync<MepCalculation>(sql, new { FromDate = fromDate, ToDate = toDate, MaxRecords = maxRecords })).ToList();
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                // Read latest tick per instrument and map to CauctionData
                var sql = @"SELECT DISTINCT ON (instrument) instrument, last_price AS rate, total_volume AS volume, time AS recorddate FROM ticks ORDER BY instrument, time DESC";
                var latestRecords = (await conn.QueryAsync(sql)).ToList();

                // Map dynamic rows to CauctionData
                var result = new List<CauctionData>();
                foreach (var row in latestRecords)
                {
                    try
                    {
                        string instrument = row.instrument;
                        decimal rate = row.rate;
                        long volume = Convert.ToInt64(row.volume);
                        DateTime timestamp = row.recorddate;

                        result.Add(new CauctionData
                        {
                            Description = instrument,
                            Rate = rate,
                            Volume = FormatVolume((decimal)volume),
                            RawVolume = (double)volume,
                            Variation = "0%",
                            RawVariation = 0,
                            Timestamp = timestamp
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skipping malformed tick row while mapping to CauctionData");
                    }
                }

                return result;
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                // Map incoming FinancialRecord list into ticks table rows
                var sql = @"INSERT INTO ticks (""time"", instrument, bid_volume, bid_price, ask_price, ask_volume, last_price, total_volume, low, high, prev_close)
                            VALUES (@RecordDate, @Instrument, @BidVolume, @BidPrice, @AskPrice, @AskVolume, @LastPrice, @TotalVolume, @Low, @High, @PrevClose)";

                // Convert FinancialRecord to anonymous objects matching the ticks columns
                var tickRows = records.Select(r => new
                {
                    RecordDate = r.RecordDate,
                    Instrument = r.Instrument,
                    BidVolume = 0L,
                    BidPrice = r.Rate,
                    AskPrice = r.Rate,
                    AskVolume = 0L,
                    LastPrice = r.Rate,
                    TotalVolume = Convert.ToInt64(Math.Floor((double)r.Volume)),
                    Low = r.Rate,
                    High = r.Rate,
                    PrevClose = r.Rate
                });

                await conn.ExecuteAsync(sql, tickRows);
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
                await using var conn = new NpgsqlConnection(_connectionString);
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                var marketSql = @"SELECT EXISTS(SELECT 1 FROM orders LIMIT 1)";
                var marketExists = await conn.ExecuteScalarAsync<bool>(marketSql);

                // MEP is computed on demand; don't require a DB table
                var mepExists = false;

                var finSql = @"SELECT EXISTS(SELECT 1 FROM ticks LIMIT 1)";
                var finExists = await conn.ExecuteScalarAsync<bool>(finSql);

                return marketExists || mepExists || finExists;
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