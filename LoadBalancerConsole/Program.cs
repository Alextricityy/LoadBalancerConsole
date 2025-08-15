using LoadBalancerConsole;

var serverPorts = new[] { 8001, 8002, 8003, 8004, 8005 };
var servers = new List<FakeHttpServer>();
var serverTasks = new List<Task>();

Console.WriteLine($"Starting {serverPorts.Length} fake HTTP servers...");

try
{
    for (int i = 0; i < serverPorts.Length; i++)
    {
        var server = new FakeHttpServer(serverPorts[i], $"server-{i + 1}");
        servers.Add(server);
        
        Console.WriteLine($"Starting fake HTTP server on port {serverPorts[i]} (ID: server-{i + 1})");
        
        var serverTask = Task.Run(async () =>
        {
            try
            {
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server on port {serverPorts[i]} failed: {ex.Message}");
            }
        });
        
        serverTasks.Add(serverTask);
        
        await Task.Delay(100);
    }

    Console.WriteLine("All servers started successfully. Press Ctrl+C to stop.");
    
    await Task.WhenAll(serverTasks);
}
catch (Exception ex)
{
    Console.WriteLine($"Error starting servers: {ex.Message}");
}
finally
{
    Console.WriteLine("Shutting down servers...");
}