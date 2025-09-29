namespace ServerMonitor.Models;

public class NetworkTraffic
{
    public int Port { get; set; }
    public string Service { get; set; } = string.Empty;
    public int ConnectionCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}