using ServerMonitor.Data;
using ServerMonitor.Models;
using ServerMonitor.Services;
using Microsoft.EntityFrameworkCore;

namespace ServerMonitor.BackgroundTasks;

public class MonitoringBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoringBackgroundService> _logger;

    public MonitoringBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitorService = scope.ServiceProvider.GetRequiredService<SystemMonitorService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var stats = await monitorService.GetServerStatsAsync();
                dbContext.ServerStats.Add(stats);

                await CheckForAlertsAsync(stats, dbContext);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Monitoring data collected at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring background service");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CheckForAlertsAsync(ServerStats stats, ApplicationDbContext dbContext)
    {
        if (stats.CpuUsagePercent > 90)
        {
            await CreateAlertAsync("CPU", $"High CPU usage: {stats.CpuUsagePercent:F1}%", dbContext);
        }

        if (stats.MemoryUsagePercent > 90)
        {
            await CreateAlertAsync("Memory", $"High memory usage: {stats.MemoryUsagePercent:F1}%", dbContext);
        }

        if (stats.DiskUsagePercent > 90)
        {
            await CreateAlertAsync("Disk", $"High disk usage: {stats.DiskUsagePercent:F1}%", dbContext);
        }
    }

    private async Task CreateAlertAsync(string type, string message, ApplicationDbContext dbContext)
    {
        var recentAlert = await dbContext.Alerts
            .Where(a => a.Type == type && !a.IsResolved && a.CreatedAt > DateTime.UtcNow.AddHours(-1))
            .FirstOrDefaultAsync();

        if (recentAlert == null)
        {
            dbContext.Alerts.Add(new Alert
            {
                Type = type,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}