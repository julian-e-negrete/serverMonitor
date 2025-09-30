
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
                          ?? "Host=localhost;Database=marketdata;Username=postgres;Password=postBlack77;Port=5432";
        _logger = logger;
    }

    public async Task<PostgresStats> GetPostgresStatsAsync()
    {
        var stats = new PostgresStats();
        _logger.LogInformation("Starting PostgreSQL stats collection");

        try
        {
            _logger.LogInformation("Attempting to connect to PostgreSQL: {ConnectionString}", 
                _connectionString.Replace("Password=postBlack77", "Password=***"));

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogInformation("Successfully connected to PostgreSQL");
            
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

            _logger.LogInformation("PostgreSQL stats collected successfully: {ActiveConnections} active connections", 
                stats.ActiveConnections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL monitoring failed: {Message}", ex.Message);
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
                    EXTRACT(EPOCH FROM (now() - query_start)) as duration_seconds,
                    wait_event_type IS NOT NULL as waiting
                FROM pg_stat_activity 
                WHERE state IS NOT NULL 
                AND datname = 'marketdata'
                ORDER BY query_start DESC";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var durationSeconds = reader["duration_seconds"] == DBNull.Value 
                    ? 0 
                    : Convert.ToDouble(reader["duration_seconds"]);

                connections.Add(new DatabaseConnection
                {
                    Pid = Convert.ToInt32(reader["pid"]),
                    Username = reader["username"].ToString() ?? "",
                    Database = reader["database"].ToString() ?? "",
                    ClientAddress = reader["client_address"]?.ToString() ?? "localhost",
                    State = reader["state"].ToString() ?? "",
                    Query = reader["query"].ToString() ?? "",
                    QueryStart = reader["query_start"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["query_start"]),
                    Duration = TimeSpan.FromSeconds(durationSeconds),
                    Waiting = reader["waiting"] != DBNull.Value && Convert.ToBoolean(reader["waiting"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot get PostgreSQL connections: {Message}", ex.Message);
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
        catch (Exception ex)
        {
            _logger.LogWarning("Database health check failed: {Message}", ex.Message);
            return false;
        }
    }

    // Helper methods
    private async Task<int> GetActiveConnectionsCountAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetMaxConnectionsAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SHOW max_connections";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 100;
        }
        catch
        {
            return 100;
        }
    }

    private async Task<int> GetIdleConnectionsCountAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT count(*) FROM pg_stat_activity WHERE state = 'idle'";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetWaitingConnectionsCountAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type IS NOT NULL";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<double> GetDatabaseSizeAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT pg_database_size('marketdata') / (1024.0 * 1024 * 1024)";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToDouble(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<(int Committed, int RolledBack)> GetTransactionStatsAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT xact_commit, xact_rollback FROM pg_stat_database WHERE datname = 'marketdata'";
            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var committed = reader["xact_commit"] == DBNull.Value ? 0 : Convert.ToInt32(reader["xact_commit"]);
                var rolledBack = reader["xact_rollback"] == DBNull.Value ? 0 : Convert.ToInt32(reader["xact_rollback"]);
                return (committed, rolledBack);
            }
        }
        catch
        {
        }
        
        return (0, 0);
    }

    private async Task<(int Inserted, int Updated, int Deleted)> GetTupleStatsAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT tup_inserted, tup_updated, tup_deleted FROM pg_stat_database WHERE datname = 'marketdata'";
            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var inserted = reader["tup_inserted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tup_inserted"]);
                var updated = reader["tup_updated"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tup_updated"]);
                var deleted = reader["tup_deleted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tup_deleted"]);
                return (inserted, updated, deleted);
            }
        }
        catch
        {
        }
        
        return (0, 0, 0);
    }

    private async Task<double> GetCacheHitRatioAsync(NpgsqlConnection connection)
    {
        try
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
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetActiveLocksCountAsync(NpgsqlConnection connection)
    {
        try
        {
            const string query = "SELECT count(*) FROM pg_locks WHERE granted = true";
            using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }
}
