using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinancialDataController : ControllerBase
    {
        private readonly IFinancialDataService _financialDataService;

        public FinancialDataController(IFinancialDataService financialDataService)
        {
            _financialDataService = financialDataService;
        }

        [HttpGet("market-data")]
        public async Task<IActionResult> GetMarketData()
        {
            var data = await _financialDataService.GetLatestMarketDataAsync();
            return Ok(data);
        }

        [HttpGet("mep-rate")]
        public async Task<IActionResult> GetMepRate()
        {
            var mep = await _financialDataService.GetLatestMepCalculationAsync();
            if (mep is null)
                return Ok(new { message = "No MEP data available" });

            return Ok(mep);
        }

        [HttpGet("cauction")]
        public async Task<IActionResult> GetCauctionData()
        {
            var data = await _financialDataService.GetCauctionDataAsync();
            return Ok(data);
        }

        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            var isHealthy = await _financialDataService.IsFinancialDataAvailableAsync();
            return Ok(new { healthy = isHealthy });
        }
    }
}