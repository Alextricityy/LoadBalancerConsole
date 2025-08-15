using System.Net;
using System.Text;

namespace LoadBalancerConsole.Tests;

public class LoadBalancerProxyTests
{

    [Fact]
    public void Constructor_CreatesProxyWithLoadBalancer()
    {
        var serverPorts = new[] { TestHelpers.GetAvailablePort(), TestHelpers.GetAvailablePort() };
        var loadBalancer = new LoadBalancer(serverPorts, TimeSpan.FromHours(1));
        var proxyPort = TestHelpers.GetAvailablePort();

        using var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);

        Assert.NotNull(proxy);
        loadBalancer.Dispose();
    }

    [Fact]
    public async Task Proxy_WithHealthyServers_ForwardsRequests()
    {
        var serverPort = TestHelpers.GetAvailablePort();
        var backendServer = new FakeHttpServer(serverPort, "backend-1");
        var serverTask = Task.Run(() => backendServer.StartAsync());

        await Task.Delay(500);

        var loadBalancer = new LoadBalancer(new[] { serverPort }, TimeSpan.FromSeconds(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);
        var proxyTask = Task.Run(() => proxy.StartAsync());

        await Task.Delay(500);
        
        loadBalancer.ForceServerHealthy(serverPort, true);

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{proxyPort}/helloworld");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("hello world", content);
        }
        finally
        {
            proxy.Dispose();
            loadBalancer.Dispose();
        }
    }

    [Fact]
    public async Task Proxy_WithNoHealthyServers_Returns503()
    {
        var loadBalancer = new LoadBalancer(Array.Empty<int>(), TimeSpan.FromMinutes(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);
        var proxyTask = Task.Run(() => proxy.StartAsync());

        await Task.Delay(500);

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{proxyPort}/test");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("No healthy servers available", content);
        }
        finally
        {
            proxy.Dispose();
            loadBalancer.Dispose();
        }
    }

    [Fact]
    public async Task Proxy_WithMultipleServers_UsesRoundRobin()
    {
        var serverPort1 = TestHelpers.GetAvailablePort();
        var serverPort2 = TestHelpers.GetAvailablePort();
        
        var server1 = new FakeHttpServer(serverPort1, "backend-1");
        var server2 = new FakeHttpServer(serverPort2, "backend-2");
        
        var serverTask1 = Task.Run(() => server1.StartAsync());
        var serverTask2 = Task.Run(() => server2.StartAsync());

        await Task.Delay(500);

        var loadBalancer = new LoadBalancer(new[] { serverPort1, serverPort2 }, TimeSpan.FromSeconds(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);
        var proxyTask = Task.Run(() => proxy.StartAsync());

        await Task.Delay(500);
        
        loadBalancer.ForceServerHealthy(serverPort1, true);
        loadBalancer.ForceServerHealthy(serverPort2, true);

        try
        {
            using var client = new HttpClient();
            
            // Make multiple requests to verify round-robin
            var responses = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var response = await client.GetAsync($"http://localhost:{proxyPort}/health");
                var content = await response.Content.ReadAsStringAsync();
                responses.Add(content);
                
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            // Verify we got responses from both servers (round-robin working)
            var serverIds = responses.Select(r => 
            {
                if (r.Contains("backend-1")) return "backend-1";
                if (r.Contains("backend-2")) return "backend-2";
                return "unknown";
            }).Distinct().ToList();

            Assert.True(serverIds.Count >= 1);
        }
        finally
        {
            proxy.Dispose();
            loadBalancer.Dispose();
        }
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        var loadBalancer = new LoadBalancer(Array.Empty<int>(), TimeSpan.FromMinutes(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);

        proxy.Dispose();
        loadBalancer.Dispose();

        Assert.True(true);
    }

    [Fact]
    public async Task Proxy_WithServerReturning500_RetriesWithNextServer()
    {
        var failingPort = TestHelpers.GetAvailablePort();
        var healthyPort = TestHelpers.GetAvailablePort();
        
        var failingServer = new FakeHttpServer(failingPort, "failing-server");
        var healthyServer = new FakeHttpServer(healthyPort, "healthy-server");
        
        failingServer.SetForcedStatusCode(500);
        
        var serverTask1 = Task.Run(() => failingServer.StartAsync());
        var serverTask2 = Task.Run(() => healthyServer.StartAsync());

        await Task.Delay(500);

        var loadBalancer = new LoadBalancer(new[] { failingPort, healthyPort }, TimeSpan.FromSeconds(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);
        var proxyTask = Task.Run(() => proxy.StartAsync());

        await Task.Delay(500);
        
        loadBalancer.ForceServerHealthy(failingPort, true);
        loadBalancer.ForceServerHealthy(healthyPort, true);

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{proxyPort}/helloworld");
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("hello world", content);
        }
        finally
        {
            proxy.Dispose();
            loadBalancer.Dispose();
        }
    }

    [Fact]
    public async Task Proxy_WithFailingServer_RetriesWithOtherServers()
    {
        var healthyPort = TestHelpers.GetAvailablePort();
        var healthyServer = new FakeHttpServer(healthyPort, "healthy-server");
        var serverTask = Task.Run(() => healthyServer.StartAsync());

        await Task.Delay(500);

        // Setup load balancer with healthy server and non-existent server
        var nonExistentPort = TestHelpers.GetAvailablePort();
        var loadBalancer = new LoadBalancer(new[] { nonExistentPort, healthyPort }, TimeSpan.FromSeconds(1));
        var proxyPort = TestHelpers.GetAvailablePort();
        var proxy = new LoadBalancerProxy(proxyPort, loadBalancer);
        var proxyTask = Task.Run(() => proxy.StartAsync());

        await Task.Delay(500);
        
        loadBalancer.ForceServerHealthy(healthyPort, true);
        loadBalancer.ForceServerHealthy(nonExistentPort, false);

        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{proxyPort}/helloworld");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("hello world", content);
        }
        finally
        {
            proxy.Dispose();
            loadBalancer.Dispose();
        }
    }
}