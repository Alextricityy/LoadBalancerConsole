using Spectre.Console;

namespace LoadBalancerConsole;

public static class ServerStatusDisplay
{
    private static readonly string[] Colors = { "green", "blue", "red", "yellow", "magenta" };

    public static void DisplayServerStatus(List<ServerInfo> servers)
    {
        var table = new Table();
        table.Title = new TableTitle("[bold]Server Health Status[/]");
        table.Border = TableBorder.Rounded;

        // Add columns for each server
        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var columnTitle = $"[bold]{server.ServerId}[/]\nPort {server.Port}";
            table.AddColumn(new TableColumn(columnTitle).Centered());
        }

        // Create the status row with colored smiley faces
        var statusCells = new string[servers.Count];
        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var color = Colors[i % Colors.Length];
            var status = server.IsHealthy ? "HEALTHY" : "UNHEALTHY";
            
            statusCells[i] = $"[{color}]{status}[/]";
        }

        table.AddRow(statusCells);
        
        var timeCells = new string[servers.Count];
        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var color = Colors[i % Colors.Length];
            var lastCheck = server.LastHealthCheck == DateTime.MinValue ? "Never" : server.LastHealthCheck.ToString("HH:mm:ss");
            timeCells[i] = $"[{color}]Last Check:[/]\n[{color}]{lastCheck}[/]";
        }

        table.AddRow(timeCells);

        AnsiConsole.Clear();
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Last updated: {DateTime.Now:HH:mm:ss}[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop the application[/]");
    }

    public static void DisplayWelcomeMessage(int loadBalancerPort, int[] serverPorts)
    {
        var panel = new Panel(
            $"""
            [bold green]Load Balancer Console Started![/]
            
            [yellow]Access your load-balanced service at:[/]
            [blue]http://localhost:{loadBalancerPort}/[/]
            
            [yellow]Try endpoints:[/] /health, /helloworld, or any other path
            
            [yellow]Individual server health:[/]
            {string.Join("\n", serverPorts.Select(p => $"[blue]http://localhost:{p}/health[/]"))}
            """)
            .Header("Load Balancer Console")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
}