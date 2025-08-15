using LoadBalancerConsole;

var serverPorts = new[] { 8001, 8002, 8003, 8004, 8005 };
var loadBalancerPort = 9000;
var servers = new List<FakeHttpServer>();


try
{
    // Start all servers concurrently
    var serverTasks = StartServers(serverPorts, servers);
    
    // Wait a moment for servers to start
    await Task.Delay(1000);
    
    // Start the LoadBalancer for health monitoring
    var loadBalancer = new LoadBalancer(serverPorts, TimeSpan.FromSeconds(5));
    
    // Start the LoadBalancerProxy
    var proxy = new LoadBalancerProxy(loadBalancerPort, loadBalancer);
    var proxyTask = Task.Run(() => proxy.StartAsync());
    
    Console.WriteLine("All systems started!");
    Console.WriteLine($"Access your load-balanced service at: http://localhost:{loadBalancerPort}/");
    Console.WriteLine($"Try endpoints: /health, /helloworld, or any other path");
    Console.WriteLine($"Individual server health: {string.Join(", ", serverPorts.Select(p => $"http://localhost:{p}/health"))}");
    
    // Start the ServerKiller with random intervals between 5-15 seconds
    using var serverKiller = new ServerKiller(servers, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
    
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

static Task[] StartServers(int[] ports, List<FakeHttpServer> servers)
{
    var tasks = new Task[ports.Length];
    
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
        Thread.Sleep(50);
    }
    
    return tasks;
}