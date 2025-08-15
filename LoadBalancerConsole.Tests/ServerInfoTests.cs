namespace LoadBalancerConsole.Tests;

public class ServerInfoTests
{
    [Fact]
    public void Constructor_WithPortAndServerId_InitializesCorrectly()
    {
        var serverInfo = new ServerInfo(8080, "test-server");
        
        Assert.Equal(8080, serverInfo.Port);
        Assert.Equal("test-server", serverInfo.ServerId);
        Assert.Equal("http://localhost:8080", serverInfo.BaseUrl);
        Assert.False(serverInfo.IsHealthy);
        Assert.Equal(DateTime.MinValue, serverInfo.LastHealthCheck);
        Assert.Equal(ServerStatus.Unknown, serverInfo.Status);
    }

    [Fact]
    public void UpdateHealth_WithHealthyStatus_UpdatesCorrectly()
    {
        var serverInfo = new ServerInfo(8080, "test-server");
        var responseTime = TimeSpan.FromMilliseconds(150);
        var beforeUpdate = DateTime.UtcNow;
        
        serverInfo.UpdateHealth(true);
        
        Assert.True(serverInfo.IsHealthy);
        Assert.Equal(ServerStatus.Healthy, serverInfo.Status);
        Assert.True(serverInfo.LastHealthCheck >= beforeUpdate);
        Assert.True(serverInfo.LastHealthCheck <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateHealth_WithUnhealthyStatus_UpdatesCorrectly()
    {
        var serverInfo = new ServerInfo(8080, "test-server");
        var responseTime = TimeSpan.FromMilliseconds(300);
        var beforeUpdate = DateTime.UtcNow;
        
        serverInfo.UpdateHealth(false);
        
        Assert.False(serverInfo.IsHealthy);
        Assert.Equal(ServerStatus.Unhealthy, serverInfo.Status);
        Assert.True(serverInfo.LastHealthCheck >= beforeUpdate);
        Assert.True(serverInfo.LastHealthCheck <= DateTime.UtcNow);
    }

    [Fact]
    public void PrintInfo_ReturnsFormattedString()
    {
        var serverInfo = new ServerInfo(8080, "test-server");
        serverInfo.UpdateHealth(true);
        
        var result = serverInfo.PrintInfo();
        
        Assert.Contains("test-server", result);
        Assert.Contains("8080", result);
        Assert.Contains("Healthy", result);
        Assert.Contains("Last checked:", result);
    }

    [Theory]
    [InlineData(ServerStatus.Unknown)]
    [InlineData(ServerStatus.Starting)]
    [InlineData(ServerStatus.Stopping)]
    [InlineData(ServerStatus.Error)]
    public void Status_CanBeSetToAnyValidValue(ServerStatus status)
    {
        var serverInfo = new ServerInfo(8080, "test-server");
        
        serverInfo.Status = status;
        
        Assert.Equal(status, serverInfo.Status);
    }
}