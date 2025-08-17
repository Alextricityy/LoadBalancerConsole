using LoadBalancerConsole;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

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
    

    ServerStatusDisplay.DisplayWelcomeMessage(loadBalancerPort, serverPorts);
    
    // Start the ServerKiller with random intervals from configuration
    var minKillInterval = configuration.GetValue<int>("ServerKiller:MinIntervalSeconds", 5);
    var maxKillInterval = configuration.GetValue<int>("ServerKiller:MaxIntervalSeconds", 15);
    var serverKiller = new ServerKiller(servers, TimeSpan.FromSeconds(minKillInterval), TimeSpan.FromSeconds(maxKillInterval), configuration);
    

    await RunApplicationAsync(serverTasks, proxyTask, loadBalancer, servers, serverKiller, serverPorts, loadBalancerPort);
    serverKiller.Dispose();
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

static async Task RunApplicationAsync(Task[] serverTasks, Task proxyTask, LoadBalancer loadBalancer, List<FakeHttpServer> servers, ServerKiller serverKiller, int[] serverPorts, int loadBalancerPort)
{
    bool shouldExit = false;
    bool showingMenu = false;
    
    var displayUpdateTask = Task.Run(async () =>
    {
        while (!shouldExit)
        {
            if (!showingMenu)
            {
                try
                {
                    await Task.Delay(2000);
                    if (!showingMenu && !shouldExit)
                    {
                        var allServers = loadBalancer.GetAllServers();
                        if (allServers.Count > 0)
                        {
                            ServerStatusDisplay.DisplayServerStatus(allServers);
                        }
                    }
                }
                catch
                {
                    break;
                }
            }
            else
            {
                await Task.Delay(100); // Short delay when menu is showing
            }
        }
    });
    
    // Show initial status
    await Task.Delay(3000); // Give servers time to start
    var initialServers = loadBalancer.GetAllServers();
    if (initialServers.Count > 0)
    {
        ServerStatusDisplay.DisplayServerStatus(initialServers);
    }
    
    // Start input monitoring for interactive mode
    var inputTask = Task.Run(async () =>
    {
        while (!shouldExit)
        {
            try
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.I) // Press 'I' for interactive mode
                {
                    showingMenu = true;
                    
                    // Create and show interactive menu
                    using var menu = new InteractiveMenu(servers, serverKiller, loadBalancer, serverPorts, loadBalancerPort);
                    await menu.ShowMainMenuAsync();
                    
                    showingMenu = false;
                    
                    // Show status again after exiting menu
                    var allServers = loadBalancer.GetAllServers();
                    if (allServers.Count > 0)
                    {
                        ServerStatusDisplay.DisplayServerStatus(allServers);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Q) // Press 'Q' to quit
                {
                    shouldExit = true;
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    });
    
    // Wait for the input task to signal exit
    await inputTask;
}