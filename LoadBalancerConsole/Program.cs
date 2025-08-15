using LoadBalancerConsole;

var serverPorts = new[] { 8001, 8002, 8003, 8004, 8005 };

Console.WriteLine($"Starting {serverPorts.Length} HTTP servers...");

try
{
    // Start all servers concurrently
    var serverTasks = StartServers(serverPorts);
    
    Console.WriteLine("All servers started. Press Ctrl+C to stop.");
    Console.WriteLine($"Health endpoints: {string.Join(", ", serverPorts.Select(p => $"http://localhost:{p}/health"))}");
    
    await Task.WhenAll(serverTasks);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    Console.WriteLine("Shutting down...");
}

static Task[] StartServers(int[] ports)
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