namespace ServerMonitor.Models;

public class ServerStats
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // CPU Usage
    public double CpuUsagePercent { get; set; }

    // Memory
    public double MemoryTotalGB { get; set; }
    public double MemoryUsedGB { get; set; }
    public double MemoryUsagePercent { get; set; }

    // Disk
    public double DiskTotalGB { get; set; }
    public double DiskUsedGB { get; set; }
    public double DiskUsagePercent { get; set; }

    // Network
    public double NetworkUploadMB { get; set; }
    public double NetworkDownloadMB { get; set; }

    // System
    public int ProcessCount { get; set; }
    public double UptimeDays { get; set; }
    public string? TopProcesses { get; set; }
}