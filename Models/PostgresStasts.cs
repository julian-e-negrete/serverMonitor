namespace ServerMonitor.Models;

public class PostgresStats
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Connection info
    public int ActiveConnections { get; set; }
    public int MaxConnections { get; set; }
    public int IdleConnections { get; set; }
    public int WaitingConnections { get; set; }

    // Database size
    public double DatabaseSizeGB { get; set; }

    // Query performance
    public int TransactionsCommitted { get; set; }
    public int TransactionsRolledBack { get; set; }
    public int TuplesInserted { get; set; }
    public int TuplesUpdated { get; set; }
    public int TuplesDeleted { get; set; }

    // Cache performance
    public double CacheHitRatio { get; set; }

    // Locks
    public int ActiveLocks { get; set; }

    // Replication (if applicable)
    public bool IsReplica { get; set; }
    public long ReplicationLagBytes { get; set; }
}

public class DatabaseConnection
{
    public int Pid { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public DateTime QueryStart { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Waiting { get; set; }
}