using ServerMonitor.Models;

namespace ServerMonitor.Services
{
    public interface IFinancialDataService
    {
        // Market Data operations
        Task SaveMarketDataAsync(MarketDataRecord record);
        Task<List<MarketDataRecord>> GetLatestMarketDataAsync();
        Task<MarketDataRecord?> GetLatestInstrumentDataAsync(string instrument);
        
        // MEP Calculations
        Task SaveMepCalculationAsync(MepCalculation mepCalculation);
        Task<MepCalculation?> GetLatestMepCalculationAsync();
        Task<List<MepCalculation>> GetMepHistoryAsync(DateTime fromDate, DateTime toDate, int maxRecords = 100);
        
        // Cauction Data
        Task<List<CauctionData>> GetCauctionDataAsync();
        Task SaveCauctionDataAsync(List<FinancialRecord> records);
        
        // Health check
        Task<bool> IsFinancialDataAvailableAsync();
    }
}