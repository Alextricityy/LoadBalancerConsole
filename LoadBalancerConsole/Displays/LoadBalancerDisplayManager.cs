using Spectre.Console;

namespace LoadBalancerConsole.Displays;

public class LoadBalancerDisplayManager
{
    private readonly Dictionary<int, DateTime> _lastServerCalls;
    private readonly int _loadBalancerPort;
    
    public LoadBalancerDisplayManager(Dictionary<int, DateTime> lastServerCalls, int loadBalancerPort)
    {
        _lastServerCalls = lastServerCalls;
        _loadBalancerPort = loadBalancerPort;
    }
    
    public void DisplayLoadBalancer()
    {
        var panel = CreateLoadBalancerPanel();
        AnsiConsole.Write(panel);
    }
    
    private Panel CreateLoadBalancerPanel()
    {
        var content = CreateLoadBalancerContent();
        var status = GetLoadBalancerStatus();
        
        var panel = new Panel(content)
            .Header($"[bold cyan]Load Balancer[/] - Port {_loadBalancerPort}")
            .Border(BoxBorder.Rounded)
            .BorderColor(status.IsActive ? Color.Green : Color.Blue)
            .Padding(28, 1);
            
        return panel;
    }
    
    private string CreateLoadBalancerContent()
    {
        var status = GetLoadBalancerStatus();
        
        return $"""
                       {status.Indicator}
                       {status.StatusText}
                """;
    }
    
    private (bool IsActive, string Indicator, string StatusText) GetLoadBalancerStatus()
    {
        var recentCall = _lastServerCalls.Values.Any(time => (DateTime.Now - time).TotalSeconds < 3);
        
        if (recentCall)
        {
            return (true, "[green]<--->[/]", "[green]ACTIVE[/]");
        }
        else
        {
            return (false, "[blue]-----[/]", "[blue]IDLE[/]");
        }
    }
}