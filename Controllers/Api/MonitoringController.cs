using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Models;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly SystemMonitorService _monitorService;

    public MonitoringController(SystemMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ServerStats>> GetCurrentStats()
    {
        return await _monitorService.GetServerStatsAsync();
    }

    [HttpGet("processes")]
    public async Task<ActionResult<List<ProcessInfo>>> GetProcesses([FromQuery] int count = 15)
    {
        return await _monitorService.GetTopProcessesAsync(count);
    }

    [HttpGet("network")]
    public async Task<ActionResult<List<NetworkConnection>>> GetNetworkConnections()
    {
        return await _monitorService.GetNetworkConnectionsAsync();
    }

    [HttpGet("services")]
    public async Task<ActionResult<List<ServiceStatus>>> GetServiceStatus()
    {
        return await _monitorService.GetServiceStatusAsync();
    }

    [HttpGet("traffic")]
    public async Task<ActionResult<Dictionary<string, NetworkTraffic>>> GetServiceTraffic()
    {
        return await _monitorService.GetServiceTrafficAsync();
    }

    [HttpGet("connections/by-service")]
    public async Task<ActionResult<Dictionary<string, int>>> GetConnectionsByService()
    {
        var connections = await _monitorService.GetNetworkConnectionsAsync();
        return connections
            .GroupBy(c => c.Service)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}