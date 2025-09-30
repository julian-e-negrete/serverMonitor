
using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Models;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly SystemMonitorService _monitorService;
    private readonly PostgresMonitorService _postgresMonitorService;

    public MonitoringController(SystemMonitorService monitorService, PostgresMonitorService postgresMonitorService)
    {
        _monitorService = monitorService;
        _postgresMonitorService = postgresMonitorService;
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

    [HttpGet("services")]
    public async Task<ActionResult<List<ServiceStatus>>> GetServiceStatus()
    {
        return await _monitorService.GetServiceStatusAsync();
    }

    [HttpGet("connections/by-service")]
    public async Task<ActionResult<Dictionary<string, int>>> GetConnectionsByService()
    {
        var connections = await _monitorService.GetNetworkConnectionsAsync();
        return connections
            .GroupBy(c => c.Service)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<List<Alert>>> GetAlerts([FromQuery] bool includeResolved = false)
    {
        return new List<Alert>();
    }

    // PostgreSQL Monitoring Endpoints
    [HttpGet("postgres/stats")]
    public async Task<ActionResult<PostgresStats>> GetPostgresStats()
    {
        return await _postgresMonitorService.GetPostgresStatsAsync();
    }

    [HttpGet("postgres/connections")]
    public async Task<ActionResult<List<DatabaseConnection>>> GetPostgresConnections()
    {
        return await _postgresMonitorService.GetActiveConnectionsAsync();
    }

    [HttpGet("postgres/health")]
    public async Task<ActionResult<bool>> GetPostgresHealth()
    {
        return await _postgresMonitorService.IsDatabaseHealthyAsync();
    }
}
