using Npgsql;
using ServerMonitor.Models;
using System.Data;

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

            // Get connection statistics
            stats.ActiveConnections = await GetActiveConnectionsCountAsync(connection);
            stats.MaxConnections = await GetMaxConnectionsAsync(connection);
            stats.IdleConnections = await GetIdleConnectionsCountAsync(connection);
            stats.WaitingConnections = await GetWaitingConnectionsCountAsync(connection);

            // Get database size
            stats.DatabaseSizeGB = await GetDatabaseSizeAsync(connection);

            // Get transaction statistics
            var transactionStats = await GetTransactionStatsAsync(connection);
            stats.TransactionsCommitted = transactionStats.Committed;
            stats.TransactionsRolledBack = transactionStats.RolledBack;

            // Get tuple statistics
            var tupleStats = await GetTupleStatsAsync(connection);
            stats.TuplesInserted = tupleStats.Inserted;
            stats.TuplesUpdated = tupleStats.Updated;
            stats.TuplesDeleted = tupleStats.Deleted;

            // Get cache hit ratio
            stats.CacheHitRatio = await GetCacheHitRatioAsync(connection);

            // Get active locks
            stats.ActiveLocks = await GetActiveLocksCountAsync(connection);

            _logger.LogDebug("PostgreSQL stats collected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting PostgreSQL statistics");
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

            const string query = @"
            SELECT 
                pid,
                usename as username,
                datname as database,
                client_addr as client_address,
                state,
                query,
                query_start,
                (now() - query_start) as duration,
                wait_event_type IS NOT NULL as waiting
            FROM pg_stat_activity 
            WHERE state IS NOT NULL 
            AND datname = 'marketdata'
            ORDER BY query_start DESC";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // Handle the duration as a TimeSpan
                TimeSpan duration;
                if (reader["duration"] != DBNull.Value)
                {
                    // Convert the interval to TimeSpan
                    var interval = (TimeSpan)reader["duration"];
                    duration = interval;
                }
                else
                {
                    duration = TimeSpan.Zero;
                }

                connections.Add(new DatabaseConnection
                {
                    Pid = reader.GetInt32("pid"),
                    Username = reader.GetString("username"),
                    Database = reader.GetString("database"),
                    ClientAddress = reader["client_address"]?.ToString() ?? "localhost",
                    State = reader.GetString("state"),
                    Query = reader.GetString("query"),
                    QueryStart = reader.GetDateTime("query_start"),
                    Duration = duration,
                    Waiting = reader.GetBoolean("waiting")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active connections");
        }

        return connections;
    }
    public async Task<List<(string Table, long SizeBytes, long RowCount)>> GetTableSizesAsync()
    {
        var tableSizes = new List<(string, long, long)>();

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT 
                    schemaname,
                    tablename,
                    pg_total_relation_size(schemaname || '.' || tablename) as size_bytes,
                    n_live_tup as row_count
                FROM pg_stat_user_tables 
                ORDER BY size_bytes DESC";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tableSizes.Add((
                    $"{reader.GetString("schemaname")}.{reader.GetString("tablename")}",
                    reader.GetInt64("size_bytes"),
                    reader.GetInt64("row_count")
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting table sizes");
        }

        return tableSizes;
    }

    public async Task<bool> IsDatabaseHealthyAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync();

            return result?.ToString() == "1";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return false;
        }
    }

    // Helper methods for individual statistics
    private async Task<int> GetActiveConnectionsCountAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'";
        using var command = new NpgsqlCommand(query, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetMaxConnectionsAsync(NpgsqlConnection connection)
    {
        const string query = "SHOW max_connections";
        using var command = new NpgsqlCommand(query, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetIdleConnectionsCountAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT count(*) FROM pg_stat_activity WHERE state = 'idle'";
        using var command = new NpgsqlCommand(query, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetWaitingConnectionsCountAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type IS NOT NULL";
        using var command = new NpgsqlCommand(query, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<double> GetDatabaseSizeAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT pg_database_size('marketdata') / (1024.0 * 1024 * 1024)";
        using var command = new NpgsqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync();
        return result != DBNull.Value ? Convert.ToDouble(result) : 0;
    }

    private async Task<(int Committed, int RolledBack)> GetTransactionStatsAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT xact_commit, xact_rollback FROM pg_stat_database WHERE datname = 'marketdata'";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return (reader.GetInt32(0), reader.GetInt32(1));
        }

        return (0, 0);
    }

    private async Task<(int Inserted, int Updated, int Deleted)> GetTupleStatsAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT tup_inserted, tup_updated, tup_deleted FROM pg_stat_database WHERE datname = 'marketdata'";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
        }

        return (0, 0, 0);
    }

    private async Task<double> GetCacheHitRatioAsync(NpgsqlConnection connection)
    {
        const string query = @"
            SELECT 
                CASE 
                    WHEN sum(heap_blks_hit) + sum(heap_blks_read) = 0 THEN 0
                    ELSE sum(heap_blks_hit) / (sum(heap_blks_hit) + sum(heap_blks_read)) 
                END as ratio
            FROM pg_statio_user_tables";

        using var command = new NpgsqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        return result != DBNull.Value ? Convert.ToDouble(result) * 100 : 0;
    }

    private async Task<int> GetActiveLocksCountAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT count(*) FROM pg_locks WHERE granted = true";
        using var command = new NpgsqlCommand(query, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}