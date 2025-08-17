using Spectre.Console;
using System.Diagnostics;

namespace LoadBalancerConsole;

public class InteractiveMenu : IDisposable
{
    private readonly List<FakeHttpServer> _servers;
    private readonly ServerKiller _serverKiller;
    private readonly LoadBalancer _loadBalancer;
    private readonly HttpClient _httpClient;
    private readonly int[] _serverPorts;
    private readonly Dictionary<int, DateTime> _lastServerCalls;
    private readonly int _loadBalancerPort;
    private readonly ConnectionDisplayManager _connectionDisplayManager;
    private readonly LoadBalancerDisplayManager _loadBalancerDisplayManager;

    public InteractiveMenu(List<FakeHttpServer> servers, ServerKiller serverKiller, LoadBalancer loadBalancer, int[] serverPorts, int loadBalancerPort = 9000)
    {
        _servers = servers;
        _serverKiller = serverKiller;
        _loadBalancer = loadBalancer;
        _serverPorts = serverPorts;
        _loadBalancerPort = loadBalancerPort;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _lastServerCalls = new Dictionary<int, DateTime>();
        _connectionDisplayManager = new ConnectionDisplayManager(_lastServerCalls);
        _loadBalancerDisplayManager = new LoadBalancerDisplayManager(_lastServerCalls, _loadBalancerPort);
    }

    public async Task ShowMainMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            DisplayServerStatusTable();

            AnsiConsole.WriteLine();

            var panel = new Panel(
                "[bold yellow]Load Balancer Console - Interactive Menu[/]\n\n" +
                "Choose an option to interact with the system:")
                .Header("Interactive Controls")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "Call Server",
                        "Call Load Balancer",
                        "Interactive ServerKiller",
                        "Exit to Main Display"
                    }));

            switch (choice)
            {
                case "Call Server":
                    await HandleServerCallMenuAsync();
                    break;
                case "Call Load Balancer":
                    await HandleLoadBalancerCallMenuAsync();
                    break;
                case "Interactive ServerKiller":
                    await HandleServerKillerMenuAsync();
                    break;
                case "Exit to Main Display":
                    return;
            }
        }
    }

    private void DisplayServerStatusTable()
    {
        var allServers = _loadBalancer.GetAllServers();

        var table = new Table();
        table.Title = new TableTitle("[bold]Server Health Status[/]");
        table.Border = TableBorder.Rounded;

        for (int i = 0; i < allServers.Count; i++)
        {
            var server = allServers[i];
            var columnTitle = $"[bold]{server.ServerId}[/]\nPort {server.Port}";
            table.AddColumn(new TableColumn(columnTitle).Centered());
        }

        // Create the status row with colored smiley faces
        var statusCells = new string[allServers.Count];
        var colors = new[] { "green", "blue", "red", "yellow", "magenta" };

        for (int i = 0; i < allServers.Count; i++)
        {
            var server = allServers[i];
            var color = colors[i % colors.Length];
            var face = server.IsHealthy ? ":)" : ":(";
            var status = server.IsHealthy ? "HEALTHY" : "UNHEALTHY";

            statusCells[i] = $"[{color}]{face}[/]\n[{color}]{status}[/]";
        }

        table.AddRow(statusCells);

        var timeCells = new string[allServers.Count];
        for (int i = 0; i < allServers.Count; i++)
        {
            var server = allServers[i];
            var color = colors[i % colors.Length];
            var lastCheck = server.LastHealthCheck == DateTime.MinValue ? "Never" : server.LastHealthCheck.ToString("HH:mm:ss");
            timeCells[i] = $"[{color}]Last Check:[/]\n[{color}]{lastCheck}[/]";
        }

        table.AddRow(timeCells);
        
        var connectionCells = new string[allServers.Count];
        for (int i = 0; i < allServers.Count; i++)
        {
            var server = allServers[i];
            var color = colors[i % colors.Length];
            var httpServer = _servers.FirstOrDefault(s => s.ServerInfo.Port == server.Port);
            var activeConnections = httpServer?.ActiveConnections ?? 0;
            var maxConnections = httpServer?.MaxConnections ?? 10;
            connectionCells[i] = $"[{color}]Connections:[/]\n[{color}]{activeConnections}/{maxConnections}[/]";
        }
        
        table.AddRow(connectionCells);

        AnsiConsole.Write(table);

        _connectionDisplayManager.DisplayConnectionLines(allServers, colors);

        _loadBalancerDisplayManager.DisplayLoadBalancer();

        AnsiConsole.MarkupLine($"[dim]Last updated: {DateTime.Now:HH:mm:ss}[/]");
    }

    private async Task HandleServerCallMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            DisplayServerStatusTable();
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[bold cyan]Server Call Menu[/]");
            AnsiConsole.WriteLine();

            var serverChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Which server(s) do you want to call?[/]")
                    .PageSize(10)
                    .AddChoices(
                        _serverPorts.Select(port => $"Server on port {port}").Concat(new[] { "All Servers", "Back to Main Menu" })
                    ));

            if (serverChoice == "Back to Main Menu")
                return;

            var endpoint = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Which endpoint do you want to call?[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "/health",
                        "/helloworld",
                        "/simulate-hung-connection",
                        "Custom endpoint",
                        "Back"
                    }));

            if (endpoint == "Back")
                continue;

            if (endpoint == "Custom endpoint")
            {
                endpoint = AnsiConsole.Ask<string>("[green]Enter custom endpoint (e.g., /api/test):[/]");
            }

            if (serverChoice == "All Servers")
            {
                await CallAllServersAsync(endpoint);
            }
            else
            {
                var port = int.Parse(serverChoice.Split(' ').Last());
                await CallSingleServerAsync(port, endpoint);
            }

            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey();
        }
    }

    private async Task CallSingleServerAsync(int port, string endpoint)
    {
        try
        {
            _lastServerCalls[port] = DateTime.Now;
            AnsiConsole.MarkupLine($"[yellow]Calling server on port {port} with endpoint {endpoint}...[/]");

            var url = $"http://localhost:{port}{endpoint}";
            var stopwatch = Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(url);
            stopwatch.Stop();

            var content = await response.Content.ReadAsStringAsync();

            AnsiConsole.Clear();
            DisplayServerStatusTable();
            AnsiConsole.WriteLine();
            var table = new Table();
            table.Border = TableBorder.Rounded;
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("URL", url);
            table.AddRow("Status Code", $"[{(response.IsSuccessStatusCode ? "green" : "red")}]{(int)response.StatusCode} {response.StatusCode}[/]");
            table.AddRow("Response Time", $"{stopwatch.ElapsedMilliseconds}ms");
            table.AddRow("Content Type", response.Content.Headers.ContentType?.ToString() ?? "N/A");
            table.AddRow("Content Length", response.Content.Headers.ContentLength?.ToString() ?? "N/A");

            AnsiConsole.Write(table);

            if (!string.IsNullOrEmpty(content))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Response Content:[/]");
                var panel = new Panel(content)
                    .Border(BoxBorder.Rounded)
                    .BorderColor(response.IsSuccessStatusCode ? Color.Green : Color.Red);
                AnsiConsole.Write(panel);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error calling server: {ex.Message}[/]");
        }
    }

    private async Task CallAllServersAsync(string endpoint)
    {
        AnsiConsole.MarkupLine($"[yellow]Calling all servers with endpoint {endpoint}...[/]");

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Server");
        table.AddColumn("Status");
        table.AddColumn("Response Time");
        table.AddColumn("Content Preview");

        var tasks = _serverPorts.Select(async port =>
        {
            try
            {
                _lastServerCalls[port] = DateTime.Now;
                var url = $"http://localhost:{port}{endpoint}";
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var preview = content.Length > 50 ? content.Substring(0, 50) + "..." : content;

                return new
                {
                    Port = port,
                    Status = $"[{(response.IsSuccessStatusCode ? "green" : "red")}]{(int)response.StatusCode}[/]",
                    ResponseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    Preview = preview.Replace("\n", " ").Replace("\r", "")
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Port = port,
                    Status = "[red]Error[/]",
                    ResponseTime = "N/A",
                    Preview = ex.Message
                };
            }
        });

        var results = await Task.WhenAll(tasks);

        AnsiConsole.Clear();
        DisplayServerStatusTable();
        AnsiConsole.WriteLine();

        foreach (var result in results)
        {
            table.AddRow(
                $"Port {result.Port}",
                result.Status,
                result.ResponseTime,
                result.Preview
            );
        }

        AnsiConsole.Write(table);
    }

    private async Task HandleLoadBalancerCallMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            DisplayServerStatusTable();
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[bold cyan]Load Balancer Call Menu[/]");
            AnsiConsole.WriteLine();

            var endpoint = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Which endpoint do you want to call on the load balancer?[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "/health",
                        "/helloworld",
                        "/simulate-hung-connection",
                        "Custom endpoint",
                        "Back to Main Menu"
                    }));

            if (endpoint == "Back to Main Menu")
                return;

            if (endpoint == "Custom endpoint")
            {
                endpoint = AnsiConsole.Ask<string>("[green]Enter custom endpoint (e.g., /api/test):[/]");
            }

            await CallLoadBalancerAsync(endpoint);

            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey();
        }
    }

    private async Task CallLoadBalancerAsync(string endpoint)
    {
        try
        {
            AnsiConsole.MarkupLine($"[yellow]Calling load balancer on port {_loadBalancerPort} with endpoint {endpoint}...[/]");

            var url = $"http://localhost:{_loadBalancerPort}{endpoint}";
            var stopwatch = Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(url);
            stopwatch.Stop();

            var content = await response.Content.ReadAsStringAsync();

            string routedServerId = null;
            if (content.Contains("serverId"))
            {
                try
                {
                    var serverIdMatch = System.Text.RegularExpressions.Regex.Match(content, @"serverId[\""\s]*:\s*[\""]([^\""]*)");
                    if (serverIdMatch.Success)
                    {
                        routedServerId = serverIdMatch.Groups[1].Value;
                        var serverInfo = _loadBalancer.GetAllServers().FirstOrDefault(s => s.ServerId == routedServerId);
                        if (serverInfo != null)
                        {
                            _lastServerCalls[serverInfo.Port] = DateTime.Now;
                        }
                    }
                }
                catch { }
            }
            AnsiConsole.Clear();
            DisplayServerStatusTable();
            AnsiConsole.WriteLine();

            var table = new Table();
            table.Border = TableBorder.Rounded;
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("URL", url);
            table.AddRow("Status Code", $"[{(response.IsSuccessStatusCode ? "green" : "red")}]{(int)response.StatusCode} {response.StatusCode}[/]");
            table.AddRow("Response Time", $"{stopwatch.ElapsedMilliseconds}ms");
            table.AddRow("Content Type", response.Content.Headers.ContentType?.ToString() ?? "N/A");
            table.AddRow("Content Length", response.Content.Headers.ContentLength?.ToString() ?? "N/A");

            if (!string.IsNullOrEmpty(routedServerId))
            {
                table.AddRow("Routed to Server", $"[cyan]{routedServerId}[/]");
            }

            AnsiConsole.Write(table);

            if (!string.IsNullOrEmpty(content))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Response Content:[/]");
                var panel = new Panel(content)
                    .Border(BoxBorder.Rounded)
                    .BorderColor(response.IsSuccessStatusCode ? Color.Green : Color.Red);
                AnsiConsole.Write(panel);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error calling load balancer: {ex.Message}[/]");
        }
    }

    private async Task HandleServerKillerMenuAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();

            DisplayServerStatusTable();
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[bold red]Interactive ServerKiller[/]");
            AnsiConsole.WriteLine();

            var actionTable = new Table();
            actionTable.Border = TableBorder.Rounded;
            actionTable.AddColumn("Server");
            actionTable.AddColumn("Status");
            actionTable.AddColumn("Action Available");

            foreach (var server in _servers)
            {
                var status = server.IsHealthy ? "[green]HEALTHY[/]" : "[red]UNHEALTHY[/]";
                var action = server.IsHealthy ? "[red]Kill[/]" : "[green]Revive[/]";
                actionTable.AddRow(server.ServerInfo.ServerId, status, action);
            }

            AnsiConsole.Write(actionTable);
            AnsiConsole.WriteLine();

            var menuAction = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What do you want to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "Kill a server",
                        "Revive a server",
                        "Kill random server",
                        "Revive random server",
                        "Back to Main Menu"
                    }));

            switch (menuAction)
            {
                case "Kill a server":
                    await HandleKillServerAsync();
                    break;
                case "Revive a server":
                    await HandleReviveServerAsync();
                    break;
                case "Kill random server":
                    await HandleRandomKillAsync();
                    break;
                case "Revive random server":
                    await HandleRandomReviveAsync();
                    break;
                case "Back to Main Menu":
                    return;
            }

            if (menuAction != "Back to Main Menu")
            {
                AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
                Console.ReadKey();
            }
        }
    }

    private async Task HandleKillServerAsync()
    {
        var healthyServers = _servers.Where(s => s.IsHealthy).ToList();
        if (!healthyServers.Any())
        {
            AnsiConsole.MarkupLine("[red]No healthy servers to kill![/]");
            return;
        }

        var serverChoice = AnsiConsole.Prompt(
            new SelectionPrompt<FakeHttpServer>()
                .Title("[red]Which server do you want to kill?[/]")
                .UseConverter(server => $"{server.ServerInfo.ServerId} (Port {server.ServerInfo.Port})")
                .AddChoices(healthyServers));

        _serverKiller.KillServer(serverChoice);
        AnsiConsole.MarkupLine($"[red]Killed {serverChoice.ServerInfo.ServerId}![/]");
        await Task.Delay(1000);
    }

    private async Task HandleReviveServerAsync()
    {
        var deadServers = _servers.Where(s => !s.IsHealthy).ToList();
        if (!deadServers.Any())
        {
            AnsiConsole.MarkupLine("[green]All servers are already healthy![/]");
            return;
        }

        var serverChoice = AnsiConsole.Prompt(
            new SelectionPrompt<FakeHttpServer>()
                .Title("[green]Which server do you want to revive?[/]")
                .UseConverter(server => $"{server.ServerInfo.ServerId} (Port {server.ServerInfo.Port})")
                .AddChoices(deadServers));

        _serverKiller.ReviveServer(serverChoice);
        AnsiConsole.MarkupLine($"[green]Revived {serverChoice.ServerInfo.ServerId}![/]");
        await Task.Delay(1000);
    }

    private async Task HandleRandomKillAsync()
    {
        var healthyServers = _servers.Where(s => s.IsHealthy).ToList();
        if (!healthyServers.Any())
        {
            AnsiConsole.MarkupLine("[red]No healthy servers to kill![/]");
            return;
        }

        var random = new Random();
        var server = healthyServers[random.Next(healthyServers.Count)];
        _serverKiller.KillServer(server);
        AnsiConsole.MarkupLine($"[red]Randomly killed {server.ServerInfo.ServerId}![/]");
        await Task.Delay(1000);
    }

    private async Task HandleRandomReviveAsync()
    {
        var deadServers = _servers.Where(s => !s.IsHealthy).ToList();
        if (!deadServers.Any())
        {
            AnsiConsole.MarkupLine("[green]All servers are already healthy![/]");
            return;
        }

        var random = new Random();
        var server = deadServers[random.Next(deadServers.Count)];
        _serverKiller.ReviveServer(server);
        AnsiConsole.MarkupLine($"[green]Randomly revived {server.ServerInfo.ServerId}![/]");
        await Task.Delay(1000);
    }



    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}