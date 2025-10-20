using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers
{
    public class CaucionController : Controller
    {
        private readonly IFinancialDataService _financialDataService;

        public CaucionController(IFinancialDataService financialDataService)
        {
            _financialDataService = financialDataService;
        }

        public async Task<IActionResult> Index()
        {
            var cauctionData = await _financialDataService.GetCauctionDataAsync();
            return View(cauctionData);
        }

        [HttpGet]
        public async Task<IActionResult> GetCauctionData()
        {
            var data = await _financialDataService.GetCauctionDataAsync();
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetMarketData()
        {
            var data = await _financialDataService.GetLatestMarketDataAsync();
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetMepRate()
        {
            var mep = await _financialDataService.GetLatestMepCalculationAsync();
            if (mep is null)
                return Ok(new { message = "No MEP data available" });

            return Ok(mep);
        }
    }
}