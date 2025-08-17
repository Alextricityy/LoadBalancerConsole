using Spectre.Console;

namespace LoadBalancerConsole;

public class ConnectionDisplayManager
{
    private readonly Dictionary<int, DateTime> _lastServerCalls;
    
    public ConnectionDisplayManager(Dictionary<int, DateTime> lastServerCalls)
    {
        _lastServerCalls = lastServerCalls;
    }
    
    public void DisplayConnectionLines(List<ServerInfo> servers, string[] colors)
    {
        var hasRecentCall = _lastServerCalls.Values.Any(time => (DateTime.Now - time).TotalSeconds < 3);
        
        if (!hasRecentCall)
        {
            return; // No connections to show
        }
        
        var connectionTable = CreateConnectionTable(servers, colors);
        AnsiConsole.Write(connectionTable);
    }
    
    private Table CreateConnectionTable(List<ServerInfo> servers, string[] colors)
    {
        var table = new Table();
        table.Border = TableBorder.None;
        table.ShowHeaders = false;
        
        for (int i = 0; i < servers.Count; i++)
        {
            table.AddColumn(new TableColumn("").Centered());
        }
        
        var lines = CreateConnectionLines(servers, colors);
        
        foreach (var line in lines)
        {
            table.AddRow(line);
        }
        
        return table;
    }
    
    private List<string[]> CreateConnectionLines(List<ServerInfo> servers, string[] colors)
    {
        var lines = new List<string[]>();
        var centerIndex = servers.Count / 2;
        
        var line1 = CreateVerticalLines(servers, colors, "      |      ");
        lines.Add(line1);
        
        var line2 = CreateVerticalLines(servers, colors, "      |      ");
        lines.Add(line2);
        
        var line3 = CreateHorizontalConnectionLine(servers, colors, centerIndex);
        lines.Add(line3);
        
        var line4 = CreateLoadBalancerConnectionLine(servers, centerIndex);
        lines.Add(line4);
        
        return lines;
    }
    
    private string[] CreateVerticalLines(List<ServerInfo> servers, string[] colors, string pattern)
    {
        var line = new string[servers.Count];
        
        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var color = colors[i % colors.Length];
            var isRecentlyCalled = IsServerRecentlyCalled(server.Port);
            
            line[i] = isRecentlyCalled ? $"[{color}]{pattern}[/]" : "              ";
        }
        
        return line;
    }
    
    private string[] CreateHorizontalConnectionLine(List<ServerInfo> servers, string[] colors, int centerIndex)
    {
        var line = new string[servers.Count];
        
        for (int i = 0; i < servers.Count; i++)
        {
            var server = servers[i];
            var color = colors[i % colors.Length];
            var isRecentlyCalled = IsServerRecentlyCalled(server.Port);
            
            if (isRecentlyCalled)
            {
                if (i < centerIndex)
                {
                    line[i] = $"[{color}]     └──────[/]";
                }
                else if (i > centerIndex)
                {
                    line[i] = $"[{color}]────┘    [/]";
                }
                else
                {
                    line[i] = $"[cyan]────────┼────────[/]";
                }
            }
            else
            {
                line[i] = $"[cyan]───────────[/]";
            }
        }
        
        return line;
    }
    
    private string[] CreateLoadBalancerConnectionLine(List<ServerInfo> servers, int centerIndex)
    {
        var line = new string[servers.Count];
        
        for (int i = 0; i < servers.Count; i++)
        {
            line[i] = i == centerIndex ? $"[cyan]       |      [/]" : "              ";
        }
        
        return line;
    }
    
    private bool IsServerRecentlyCalled(int port)
    {
        return _lastServerCalls.ContainsKey(port) && 
               (DateTime.Now - _lastServerCalls[port]).TotalSeconds < 3;
    }
}