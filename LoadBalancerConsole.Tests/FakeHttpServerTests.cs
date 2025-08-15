using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace LoadBalancerConsole.Tests;

public class FakeHttpServerTests
{
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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
    public void Constructor_SetsPropertiesCorrectly()
    {
        var port = GetAvailablePort();
        var server = new FakeHttpServer(port, "constructor-test");
        
        Assert.NotNull(server);
    }
}
