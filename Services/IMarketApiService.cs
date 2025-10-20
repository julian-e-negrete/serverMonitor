using ServerMonitor.Models;

namespace ServerMonitor.Services
{
    public interface IMarketApiService
    {
        Task<List<CauctionData>> FetchCauctionDataAsync();
        Task<bool> TestConnectionAsync();
    }
}