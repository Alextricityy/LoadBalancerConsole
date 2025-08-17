using LoadBalancerConsole;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var serverPorts = configuration.GetSection("ServerConfiguration:ServerPorts").Get<int[]>() ?? new[] { 8001, 8002, 8003, 8004, 8005 };
var loadBalancerPort = configuration.GetValue<int>("ServerConfiguration:LoadBalancerPort", 9000);
var servers = new List<FakeHttpServer>();


try
{
    // Start all servers concurrently
    var serverTasks = StartServers(serverPorts, servers, configuration);
    
    // Wait a moment for servers to start
    var startupDelay = configuration.GetValue<int>("ServerConfiguration:ApplicationStartupDelayMs", 1000);
    await Task.Delay(startupDelay);
    
    // Start the LoadBalancer for health monitoring
    var healthCheckInterval = configuration.GetValue<int>("HealthCheck:IntervalSeconds", 5);
    var loadBalancer = new LoadBalancer(serverPorts, TimeSpan.FromSeconds(healthCheckInterval), configuration);
    
    // Start the LoadBalancerProxy
    var proxy = new LoadBalancerProxy(loadBalancerPort, loadBalancer, configuration);
    var proxyTask = Task.Run(() => proxy.StartAsync());
    
    Console.WriteLine("All systems started!");
    Console.WriteLine($"Access your load-balanced service at: http://localhost:{loadBalancerPort}/");
    Console.WriteLine($"Try endpoints: /health, /helloworld, or any other path");
    Console.WriteLine($"Individual server health: {string.Join(", ", serverPorts.Select(p => $"http://localhost:{p}/health"))}");
    
    // Start the ServerKiller with random intervals from configuration
    var minKillInterval = configuration.GetValue<int>("ServerKiller:MinIntervalSeconds", 5);
    var maxKillInterval = configuration.GetValue<int>("ServerKiller:MaxIntervalSeconds", 15);
    using var serverKiller = new ServerKiller(servers, TimeSpan.FromSeconds(minKillInterval), TimeSpan.FromSeconds(maxKillInterval), configuration);
    
    Console.WriteLine("Press Ctrl+C to stop.");
    
    // Wait for all tasks
    var allTasks = serverTasks.Concat(new[] { proxyTask }).ToArray();
    await Task.WhenAll(allTasks);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    Console.WriteLine("Shutting down...");
}

static Task[] StartServers(int[] ports, List<FakeHttpServer> servers, IConfiguration configuration)
{
    var tasks = new Task[ports.Length];
    var startupDelay = configuration.GetValue<int>("ServerConfiguration:ServerStartupDelayMs", 50);
    
    for (int i = 0; i < ports.Length; i++)
    {
        var port = ports[i];
        var serverId = $"server-{i + 1}";
        
        Console.WriteLine($"Starting {serverId} on port {port}");
        
        tasks[i] = Task.Run(async () =>
        {
            try
            {
                var server = new FakeHttpServer(port, serverId);
                servers.Add(server); // Add to the servers list for ServerKiller
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{serverId} (port {port}) failed: {ex.Message}");
            }
        });
        
        // Small delay to stagger startup
        Thread.Sleep(startupDelay);
    }
    
    return tasks;
}