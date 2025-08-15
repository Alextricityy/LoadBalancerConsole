namespace LoadBalancerConsole;

public class ServerInfo
{
    public int Port { get; set; }
    public string ServerId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public ServerInfo(int port, string serverId)
    {
        Port = port;
        ServerId = serverId;
        BaseUrl = $"http://localhost:{port}";
        IsHealthy = false;
        LastHealthCheck = DateTime.MinValue;
        Status = ServerStatus.Unknown;
    }

    public void UpdateHealthStatus(bool isHealthy, TimeSpan responseTime, string? error = null)
    {
        IsHealthy = isHealthy;
        LastHealthCheck = DateTime.UtcNow;
        Status = isHealthy ? ServerStatus.Healthy : ServerStatus.Unhealthy;
    }
}

public enum ServerStatus
{
    Unknown,
    Healthy,
    Unhealthy,
    Starting,
    Stopping,
    Error
}
