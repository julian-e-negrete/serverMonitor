using ServerMonitor.Models;
using System.Diagnostics;

namespace ServerMonitor.Services;

public class SystemMonitorService
{
    private readonly ILogger<SystemMonitorService> _logger;

    public SystemMonitorService(ILogger<SystemMonitorService> logger)
    {
        _logger = logger;
    }

    public async Task<ServerStats> GetServerStatsAsync()
    {
        var stats = new ServerStats();

        stats.CpuUsagePercent = await GetCpuUsageAsync();

        var memoryInfo = await GetMemoryInfoAsync();
        stats.MemoryTotalGB = memoryInfo.TotalGB;
        stats.MemoryUsedGB = memoryInfo.UsedGB;
        stats.MemoryUsagePercent = memoryInfo.UsagePercent;

        var diskInfo = await GetDiskInfoAsync();
        stats.DiskTotalGB = diskInfo.TotalGB;
        stats.DiskUsedGB = diskInfo.UsedGB;
        stats.DiskUsagePercent = diskInfo.UsagePercent;

        stats.ProcessCount = Process.GetProcesses().Length;
        stats.UptimeDays = await GetSystemUptimeAsync();

        return stats;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            var start = ReadCpuStats();
            await Task.Delay(1000);
            var end = ReadCpuStats();

            var totalDiff = end.Total - start.Total;
            var idleDiff = end.Idle - start.Idle;

            return totalDiff > 0 ? (totalDiff - idleDiff) / totalDiff * 100 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading CPU usage");
            return 0;
        }
    }

    private (double Total, double Idle) ReadCpuStats()
    {
        var lines = File.ReadAllLines("/proc/stat");
        var cpuLine = lines.First(l => l.StartsWith("cpu "));
        var values = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var user = double.Parse(values[1]);
        var nice = double.Parse(values[2]);
        var system = double.Parse(values[3]);
        var idle = double.Parse(values[4]);

        var total = user + nice + system + idle;
        return (total, idle);
    }

    private async Task<(double TotalGB, double UsedGB, double UsagePercent)> GetMemoryInfoAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/meminfo");
            var memTotal = GetMemoryValue(lines, "MemTotal:");
            var memAvailable = GetMemoryValue(lines, "MemAvailable:");

            var totalGB = memTotal / 1024 / 1024;
            var usedGB = (memTotal - memAvailable) / 1024 / 1024;
            var usagePercent = memTotal > 0 ? (memTotal - memAvailable) / memTotal * 100 : 0;

            return (totalGB, usedGB, usagePercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading memory info");
            return (0, 0, 0);
        }
    }

    private double GetMemoryValue(string[] lines, string key)
    {
        var line = lines.First(l => l.StartsWith(key));
        var value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
        return double.Parse(value);
    }

    private async Task<(double TotalGB, double UsedGB, double UsagePercent)> GetDiskInfoAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "df",
                Arguments = "/ --block-size=1G",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null) return (0, 0, 0);

            var output = await process.StandardOutput.ReadToEndAsync();
            var lines = output.Split('\n');
            var rootLine = lines.FirstOrDefault(l => l.EndsWith("/"));

            if (rootLine != null)
            {
                var parts = rootLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    var total = double.Parse(parts[1]);
                    var used = double.Parse(parts[2]);
                    var usagePercent = double.Parse(parts[4].Trim('%'));
                    return (total, used, usagePercent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading disk info");
        }

        return (0, 0, 0);
    }

    private async Task<double> GetSystemUptimeAsync()
    {
        try
        {
            var uptime = await File.ReadAllTextAsync("/proc/uptime");
            var seconds = double.Parse(uptime.Split(' ')[0]);
            return seconds / 86400; // Convert to days
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading uptime");
            return 0;
        }
    }

    public async Task<List<ProcessInfo>> GetTopProcessesAsync(int count = 10)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = "aux --sort=-%cpu",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null) return processes;

            var output = await process.StandardOutput.ReadToEndAsync();
            var lines = output.Split('\n').Skip(1);

            foreach (var line in lines.Take(count))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 11)
                {
                    processes.Add(new ProcessInfo
                    {
                        User = parts[0],
                        Pid = int.Parse(parts[1]),
                        CpuUsage = double.Parse(parts[2]),
                        MemoryUsage = double.Parse(parts[3]),
                        Command = string.Join(" ", parts[10..])
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process info");
        }

        return processes;
    }

    public async Task<List<NetworkConnection>> GetNetworkConnectionsAsync()
    {
        var connections = new List<NetworkConnection>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ss",
                Arguments = "-tulnp",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null) return connections;

            var output = await process.StandardOutput.ReadToEndAsync();
            var lines = output.Split('\n').Skip(1);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 6)
                {
                    var connection = new NetworkConnection
                    {
                        Protocol = parts[0],
                        LocalAddress = parts[4],
                        RemoteAddress = parts.Length > 5 ? parts[5] : "",
                        State = parts[1]
                    };

                    var processInfo = parts.Last();
                    connection.Process = processInfo;
                    connection.Service = IdentifyServiceByPort(connection.LocalAddress);

                    connections.Add(connection);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network connections");
        }

        return connections;
    }

    private string IdentifyServiceByPort(string localAddress)
    {
        if (localAddress.Contains(":5432")) return "PostgreSQL";
        if (localAddress.Contains(":22")) return "SSH";
        if (localAddress.Contains(":3306")) return "MySQL";
        if (localAddress.Contains(":80") || localAddress.Contains(":443")) return "HTTP/HTTPS";
        if (localAddress.Contains(":8080")) return "Web App";
        return "Unknown";
    }

    public async Task<List<ServiceStatus>> GetServiceStatusAsync()
    {
        var services = new List<ServiceStatus>();
        var serviceNames = new[] { "wsclient.service", "nginx", "postgresql", "mysql", "ssh", "cloudflared" };

        foreach (var serviceName in serviceNames)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"is-active {serviceName}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var status = (await process.StandardOutput.ReadToEndAsync()).Trim();
                    services.Add(new ServiceStatus
                    {
                        Name = serviceName,
                        Status = status,
                        IsActive = status == "active"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service {Service}", serviceName);
            }
        }

        return services;
    }

    public async Task<Dictionary<string, NetworkTraffic>> GetServiceTrafficAsync()
    {
        var traffic = new Dictionary<string, NetworkTraffic>();
        var monitoredPorts = new[] { 5432, 22, 3306, 8080, 80, 443 };

        foreach (var port in monitoredPorts)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ss",
                    Arguments = $"-tn src :{port}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var connectionCount = output.Split('\n').Count() - 1;

                    traffic[port.ToString()] = new NetworkTraffic
                    {
                        Port = port,
                        Service = IdentifyServiceByPort($":{port}"),
                        ConnectionCount = connectionCount,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting traffic for port {Port}", port);
            }
        }

        return traffic;
    }
}