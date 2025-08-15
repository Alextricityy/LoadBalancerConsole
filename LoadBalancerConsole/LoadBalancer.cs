using System.Text.Json;

namespace LoadBalancerConsole;

public class LoadBalancer : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<ServerInfo> _servers;
    private readonly Timer _healthCheckTimer;

    public LoadBalancer(IEnumerable<int> serverPorts, TimeSpan? checkInterval = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _servers = CreateServers(serverPorts);
        
        var interval = checkInterval ?? TimeSpan.FromSeconds(30);
        _healthCheckTimer = new Timer(RunHealthChecks, null, TimeSpan.Zero, interval);
    }

    public List<ServerInfo> GetHealthyServers()
    {
        return _servers.Where(s => s.IsHealthy).ToList();
    }

    public List<ServerInfo> GetAllServers()
    {
        return new List<ServerInfo>(_servers);
    }

    //used for testing purposes to force a server's health status
    public void ForceServerHealthy(int port, bool isHealthy)
    {
        var server = _servers.FirstOrDefault(s => s.Port == port);
        if (server != null)
        {
            server.UpdateHealth(isHealthy);
        }
    }

    public async Task<bool> CheckServerHealthAsync(ServerInfo server)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{server.BaseUrl}/health");
            
            if (response.IsSuccessStatusCode)
            {
                var isHealthy = await ValidateHealthResponse(response, server.ServerId);
                server.UpdateHealth(isHealthy);
                LogHealthCheck(server, isHealthy, "OK");
                return isHealthy;
            }
            
            server.UpdateHealth(false);
            LogHealthCheck(server, false, $"HTTP {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            server.UpdateHealth(false);
            LogHealthCheck(server, false, ex.GetType().Name);
            return false;
        }
    }
    private static List<ServerInfo> CreateServers(IEnumerable<int> ports)
    {
        return ports.Select((port, index) => new ServerInfo(port, $"server-{index + 1}")).ToList();
    }

    private static async Task<bool> ValidateHealthResponse(HttpResponseMessage response, string expectedServerId)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var healthData = JsonSerializer.Deserialize<JsonElement>(content);
            
            var status = healthData.GetProperty("status").GetString();
            var serverId = healthData.GetProperty("serverId").GetString();
            
            return status == "healthy" && serverId == expectedServerId;
        }
        catch
        {
            return false;
        }
    }

    private static void LogHealthCheck(ServerInfo server, bool isHealthy, string details)
    {
        var status = isHealthy ? "HEALTHY" : "FAILED";
        Console.WriteLine($"Health check {server.ServerId} (port {server.Port}): {status} ({details})");
    }

    private async void RunHealthChecks(object? state)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] Checking {_servers.Count} servers...");
        
        var tasks = _servers.Select(CheckServerHealthAsync);
        await Task.WhenAll(tasks);
        
        var healthyCount = _servers.Count(s => s.IsHealthy);
        Console.WriteLine($"[{timestamp}] Complete: {healthyCount}/{_servers.Count} healthy");
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _httpClient?.Dispose();
    }
}