namespace LoadBalancerConsole;

public class ServerInfo
{
    public int Port { get; }
    public string ServerId { get; }
    public string BaseUrl { get; }
    public bool IsHealthy { get; private set; }
    public DateTime LastHealthCheck { get; private set; }
    public ServerStatus Status { get; set; }

    public ServerInfo(int port, string serverId)
    {
        Port = port;
        ServerId = serverId;
        BaseUrl = $"http://localhost:{port}";
        IsHealthy = false;
        LastHealthCheck = DateTime.MinValue;
        Status = ServerStatus.Unknown;
    }

    public void UpdateHealth(bool isHealthy)
    {
        IsHealthy = isHealthy;
        LastHealthCheck = DateTime.UtcNow;
        Status = isHealthy ? ServerStatus.Healthy : ServerStatus.Unhealthy;
    }
    public string PrintInfo()
    {
        var timeStr = LastHealthCheck == DateTime.MinValue ? "Never" : LastHealthCheck.ToString("HH:mm:ss");
        return $"Server {ServerId} (Port {Port}): {Status} - Last checked: {timeStr}";
    }
}

public enum ServerStatus
{
    Unknown,
    Starting,
    Healthy,
    Unhealthy,
    Error,
    Stopping
}
