using LoadBalancerConsole.FakeServer;
using System.Net;
using System.Text.Json;

namespace LoadBalancerConsole.Tests;

public class FakeHttpServerTests
{
    
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "test-server");
        var serverTask = Task.Run(() => server.StartAsync());
        
        await Task.Delay(500);
        
        using var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/health");
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        
        var healthData = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.Equal("test-server", healthData.GetProperty("serverId").GetString());
        Assert.Equal("healthy", healthData.GetProperty("status").GetString());
        Assert.True(healthData.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task HelloWorldEndpoint_ReturnsPlainText()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "test-server");
        var serverTask = Task.Run(() => server.StartAsync());
        
        await Task.Delay(500);
        
        using var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/helloworld");
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello world", content);
    }

    [Fact]
    public async Task DefaultEndpoint_ReturnsDefaultResponse()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "test-server");
        var serverTask = Task.Run(() => server.StartAsync());
        
        await Task.Delay(500);
        
        using var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/unknown");
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("default response", content);
    }

    [Fact]
    public void Constructor_WithPortAndServerId_SetsPropertiesCorrectly()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "constructor-test");
        
        Assert.NotNull(server);
        Assert.NotNull(server.ServerInfo);
        Assert.Equal(port, server.ServerInfo.Port);
        Assert.Equal("constructor-test", server.ServerInfo.ServerId);
        Assert.Equal($"http://localhost:{port}", server.ServerInfo.BaseUrl);
        Assert.Equal(ServerStatus.Unknown, server.ServerInfo.Status);
    }

    [Fact]
    public void Constructor_WithServerInfo_SetsPropertiesCorrectly()
    {
        var serverInfo = new ServerInfo(9000, "test-server-info");
        var server = new FakeHttpServer(serverInfo);
        
        Assert.NotNull(server);
        Assert.Same(serverInfo, server.ServerInfo);
        Assert.Equal(9000, server.ServerInfo.Port);
        Assert.Equal("test-server-info", server.ServerInfo.ServerId);
    }

    [Fact]
    public async Task HealthEndpoint_IncludesPortInResponse()
    {
        var port = TestHelpers.GetAvailablePort();
        var server = new FakeHttpServer(port, "port-test-server");
        var serverTask = Task.Run(() => server.StartAsync());
        
        await Task.Delay(500);
        
        using var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/health");
        var content = await response.Content.ReadAsStringAsync();
        
        var healthData = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.Equal(port, healthData.GetProperty("port").GetInt32());
        Assert.Equal("port-test-server", healthData.GetProperty("serverId").GetString());
    }

    [Fact]
    public void ServerInfo_StatusUpdates_WorkCorrectly()
    {
        var serverInfo = new ServerInfo(8000, "status-test");
        var server = new FakeHttpServer(serverInfo);
        
        Assert.Equal(ServerStatus.Unknown, server.ServerInfo.Status);
        
        server.ServerInfo.UpdateHealth(true);
        
        Assert.Equal(ServerStatus.Healthy, server.ServerInfo.Status);
        Assert.True(server.ServerInfo.IsHealthy);
        Assert.True(server.ServerInfo.LastHealthCheck > DateTime.MinValue);
    }
}
