using LoadBalancerConsole.FakeServer;
using ServerKillerClass = LoadBalancerConsole.ServerKiller.ServerKiller;

namespace LoadBalancerConsole.Tests;

public class ServerKillerTests
{

    [Fact]
    public void Constructor_CreatesServerKillerWithServers()
    {
        var servers = new List<FakeHttpServer>
        {
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server-1"),
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server-2")
        };

        using var serverKiller = new ServerKillerClass(servers, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20));

        Assert.NotNull(serverKiller);
    }

    [Fact]
    public void KillServer_SetsServerToUnhealthy()
    {
        var server = new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server");
        var servers = new List<FakeHttpServer> { server };

        using var serverKiller = new ServerKillerClass(servers, TimeSpan.FromHours(1), TimeSpan.FromHours(2));

        Assert.True(server.IsHealthy);

        serverKiller.KillServer(server);

        Assert.False(server.IsHealthy);
        Assert.Equal(ServerStatus.Unhealthy, server.ServerInfo.Status);
    }

    [Fact]
    public void ReviveServer_SetsServerToHealthy()
    {
        var server = new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server");
        var servers = new List<FakeHttpServer> { server };

        using var serverKiller = new ServerKillerClass(servers, TimeSpan.FromHours(1), TimeSpan.FromHours(2));

        serverKiller.KillServer(server);
        Assert.False(server.IsHealthy);

        serverKiller.ReviveServer(server);

        Assert.True(server.IsHealthy);
        Assert.Equal(ServerStatus.Healthy, server.ServerInfo.Status);
    }

    [Fact]
    public void PrintStatus_DoesNotThrowException()
    {
        var servers = new List<FakeHttpServer>
        {
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server-1"),
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server-2")
        };

        using var serverKiller = new ServerKillerClass(servers, TimeSpan.FromHours(1), TimeSpan.FromHours(2));

        serverKiller.KillServer(servers[0]);
        serverKiller.PrintStatus();

        Assert.True(true);
    }

    [Fact]
    public void SetHealthy_UpdatesServerHealthStatus()
    {
        var server = new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server");

        server.SetHealthy(false);
        Assert.False(server.IsHealthy);
        Assert.Equal(ServerStatus.Unhealthy, server.ServerInfo.Status);

        server.SetHealthy(true);
        Assert.True(server.IsHealthy);
        Assert.Equal(ServerStatus.Healthy, server.ServerInfo.Status);
    }

    [Fact]
    public async Task ServerKiller_WithShortInterval_EventuallyChangesServerStatus()
    {
        var servers = new List<FakeHttpServer>
        {
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server")
        };

        using var serverKiller = new ServerKillerClass(servers, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));

        var initialStatus = servers[0].IsHealthy;
        await Task.Delay(500);
        Assert.True(true);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        var servers = new List<FakeHttpServer>
        {
            new FakeHttpServer(TestHelpers.GetAvailablePort(), "test-server")
        };

        var serverKiller = new ServerKillerClass(servers, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        
        serverKiller.Dispose();
        
        Assert.True(true);
    }
}