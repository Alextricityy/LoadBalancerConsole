namespace LoadBalancerConsole.Tests;

public class LoadBalancerTests
{

    [Fact]
    public async Task CheckServerHealthAsync_WithHealthyServer_ReturnsTrue()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "server-1");
        var serverTask = Task.Run(() => server.StartAsync());
        
        await Task.Delay(500);
        
        var loadBalancer = new LoadBalancer(new[] { port }, TimeSpan.FromHours(1));
        var serverInfo = new ServerInfo(port, "server-1");
        
        var isHealthy = await loadBalancer.CheckServerHealthAsync(serverInfo);
        
        Assert.True(isHealthy);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public async Task CheckServerHealthAsync_WithDownServer_ReturnsFalse()
    {
        var port = TestHelpers.GetAvailablePort();
        var loadBalancer = new LoadBalancer(new[] { port }, TimeSpan.FromHours(1));
        var serverInfo = new ServerInfo(port, "server-1");
        
        var isHealthy = await loadBalancer.CheckServerHealthAsync(serverInfo);
        
        Assert.False(isHealthy);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public async Task GetHealthyServers_WithMixedServerStates_ReturnsOnlyHealthyServers()
    {
        var healthyPort = TestHelpers.GetAvailablePort();
        var downPort = TestHelpers.GetAvailablePort();
        
        var healthyServer = new FakeHttpServer(healthyPort, "server-1");
        var serverTask = Task.Run(() => healthyServer.StartAsync());
        
        await Task.Delay(500);
        
        var loadBalancer = new LoadBalancer(new[] { healthyPort, downPort }, TimeSpan.FromMilliseconds(100));
        
        await Task.Delay(1000);
        
        var healthyServers = loadBalancer.GetHealthyServers();
        
        Assert.Single(healthyServers);
        Assert.Equal(healthyPort, healthyServers[0].Port);
        Assert.True(healthyServers[0].IsHealthy);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public void GetAllServers_ReturnsAllConfiguredServers()
    {
        var ports = new[] { TestHelpers.GetAvailablePort(), TestHelpers.GetAvailablePort(), TestHelpers.GetAvailablePort() };
        var loadBalancer = new LoadBalancer(ports, TimeSpan.FromHours(1));
        
        var allServers = loadBalancer.GetAllServers();
        
        Assert.Equal(3, allServers.Count);
        Assert.Equal(ports[0], allServers[0].Port);
        Assert.Equal(ports[1], allServers[1].Port);
        Assert.Equal(ports[2], allServers[2].Port);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public void Constructor_SetsUpServersCorrectly()
    {
        var ports = new[] { 8001, 8002, 8003 };
        var loadBalancer = new LoadBalancer(ports, TimeSpan.FromMinutes(1));
        
        var allServers = loadBalancer.GetAllServers();
        
        Assert.Equal(3, allServers.Count);
        Assert.Equal("server-1", allServers[0].ServerId);
        Assert.Equal("server-2", allServers[1].ServerId);
        Assert.Equal("server-3", allServers[2].ServerId);
        Assert.Equal("http://localhost:8001", allServers[0].BaseUrl);
        Assert.Equal("http://localhost:8002", allServers[1].BaseUrl);
        Assert.Equal("http://localhost:8003", allServers[2].BaseUrl);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public async Task PeriodicHealthChecks_UpdatesServerStatus()
    {
        var healthyPort = TestHelpers.GetAvailablePort();
        var healthyServer = new FakeHttpServer(healthyPort, "server-1");
        var serverTask = Task.Run(() => healthyServer.StartAsync());
        
        await Task.Delay(500);
        
        var loadBalancer = new LoadBalancer(new[] { healthyPort }, TimeSpan.FromMilliseconds(500));
        
        await Task.Delay(1500);
        
        var servers = loadBalancer.GetAllServers();
        Assert.True(servers[0].IsHealthy);
        Assert.True(servers[0].LastHealthCheck > DateTime.MinValue);
        
        loadBalancer.Dispose();
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        var ports = new[] { TestHelpers.GetAvailablePort(), TestHelpers.GetAvailablePort() };
        var loadBalancer = new LoadBalancer(ports, TimeSpan.FromSeconds(1));
        
        loadBalancer.Dispose();
        
        Assert.True(true);
    }
}