using Npgsql;
using ServerMonitor.Models;

namespace ServerMonitor.Services;

public class PostgresMonitorService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresMonitorService> _logger;

    public PostgresMonitorService(IConfiguration configuration, ILogger<PostgresMonitorService> logger)
    {
        _connectionString = configuration.GetConnectionString("MarketDataConnection") 
                          ?? "Host=100.112.16.115;Database=marketdata;Username=postgres;Password=postBlack77;Port=5432";
        _logger = logger;
    }

    public async Task<PostgresStats> GetPostgresStatsAsync()
    {
        var stats = new PostgresStats();

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // Your PostgreSQL monitoring code here
            // This will only run if connection succeeds
            
            _logger.LogInformation("PostgreSQL monitoring successful");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PostgreSQL monitoring unavailable: {Message}", ex.Message);
            // Return empty stats - don't throw!
        }

        return stats;
    }

    public async Task<List<DatabaseConnection>> GetActiveConnectionsAsync()
    {
        var connections = new List<DatabaseConnection>();

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // Your connection monitoring code here
            
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot get PostgreSQL connections: {Message}", ex.Message);
            // Return empty list - don't throw!
        }

        return connections;
    }

    public async Task<bool> IsDatabaseHealthyAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
