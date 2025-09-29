namespace ServerMonitor.Models;

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public string User { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}