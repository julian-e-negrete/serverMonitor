namespace ServerMonitor.Models;

public class ServiceStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? Uptime { get; set; }
}