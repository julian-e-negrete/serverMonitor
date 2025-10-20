using System.Text.Json;
using ServerMonitor.Models;

namespace ServerMonitor.Services
{
    public class MarketApiService : IMarketApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MarketApiService> _logger;

        public MarketApiService(HttpClient httpClient, ILogger<MarketApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:140.0) Gecko/20100101 Firefox/140.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<CauctionData>> FetchCauctionDataAsync()
        {
            try
            {
                var url = "https://api.marketdata.mae.com.ar/api/mercado/datos/CAU";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<List<CauctionDataResponse>>(json);

                if (data == null || data.Count == 0)
                {
                    _logger.LogWarning("No cauction data received from API");
                    return new List<CauctionData>();
                }

                return data.Select(item => new CauctionData
                {
                    Description = item.descripcion,
                    Rate = item.ultimaTasa,
                    Volume = FormatVolume(item.volumen),
                    RawVolume = item.volumen,
                    Variation = $"{item.variacion}%",
                    RawVariation = item.variacion,
                    Timestamp = DateTime.Now
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cauction data from API");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.marketdata.mae.com.ar/api/mercado/datos/CAU");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Market API connection test failed");
                return false;
            }
        }

        private static string FormatVolume(double volume)
        {
            if (volume >= 1_000_000_000_000)
                return $"{(volume / 1_000_000_000_000):0.##} trillion";
            if (volume >= 1_000_000_000)
                return $"{(volume / 1_000_000_000):0.##} billion";
            if (volume >= 1_000_000)
                return $"{(volume / 1_000_000):0.##} million";
            
            return volume.ToString("N0");
        }

        private class CauctionDataResponse
        {
            public string descripcion { get; set; } = string.Empty;
            public decimal ultimaTasa { get; set; }
            public double volumen { get; set; }
            public decimal variacion { get; set; }
        }
    }
}