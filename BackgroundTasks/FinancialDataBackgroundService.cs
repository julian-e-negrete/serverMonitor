using ServerMonitor.Services;
using ServerMonitor.Models;


namespace ServerMonitor.BackgroundTasks
{
    public class FinancialDataBackgroundService : BackgroundService
    {
        private readonly ILogger<FinancialDataBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // Do not inject scoped services (like IFinancialDataService) into the
        // singleton background service. Instead create a scope and resolve
        // the scoped services inside the loop.
        public FinancialDataBackgroundService(
            ILogger<FinancialDataBackgroundService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Financial Data Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var marketApi = scope.ServiceProvider.GetRequiredService<IMarketApiService>();
                    var financialService = scope.ServiceProvider.GetRequiredService<IFinancialDataService>();

                    await FetchAndStoreCauctionDataAsync(marketApi, financialService);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Financial Data Background Service");
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }
        }

        private async Task FetchAndStoreCauctionDataAsync(IMarketApiService marketApiService, IFinancialDataService financialDataService)
        {
            try
            {
                var cauctionData = await marketApiService.FetchCauctionDataAsync();

                var records = cauctionData.Select(item => new FinancialRecord
                {
                    Instrument = item.Description,
                    Rate = item.Rate,
                    Volume = (decimal)item.RawVolume,
                    Variation = item.RawVariation,
                    Type = "Cauction",
                    RecordDate = DateTime.UtcNow
                }).ToList();

                await financialDataService.SaveCauctionDataAsync(records);
                _logger.LogInformation("Successfully fetched and stored {Count} cauction records", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching and storing cauction data");
            }
        }
    }
}