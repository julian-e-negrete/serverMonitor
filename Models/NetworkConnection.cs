namespace ServerMonitor.Models;

public class NetworkConnection
{
    public string Protocol { get; set; } = string.Empty;
    public string LocalAddress { get; set; } = string.Empty;
    public string RemoteAddress { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
}