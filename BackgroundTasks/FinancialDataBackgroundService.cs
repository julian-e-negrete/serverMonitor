using ServerMonitor.Services;
using ServerMonitor.Models;


namespace ServerMonitor.BackgroundTasks
{
    public class FinancialDataBackgroundService : BackgroundService
    {
        private readonly IFinancialDataService _financialDataService;
        private readonly IMarketApiService _marketApiService;
        private readonly ILogger<FinancialDataBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public FinancialDataBackgroundService(
            IFinancialDataService financialDataService,
            IMarketApiService marketApiService,
            ILogger<FinancialDataBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _financialDataService = financialDataService;
            _marketApiService = marketApiService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Financial Data Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FetchAndStoreCauctionDataAsync();
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Financial Data Background Service");
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }
        }

        private async Task FetchAndStoreCauctionDataAsync()
        {
            try
            {
                var cauctionData = await _marketApiService.FetchCauctionDataAsync();

                var records = cauctionData.Select(item => new FinancialRecord
                {
                    Instrument = item.Description,
                    Rate = item.Rate,
                    Volume = (decimal)item.RawVolume,
                    Variation = item.RawVariation,
                    Type = "Cauction",
                    RecordDate = DateTime.UtcNow
                }).ToList();

                await _financialDataService.SaveCauctionDataAsync(records);
                _logger.LogInformation("Successfully fetched and stored {Count} cauction records", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching and storing cauction data");
            }
        }
    }
}