using Spectre.Console;

namespace LoadBalancerConsole;

public class ServerKillerDisplayManager
{
    private readonly ServerKiller _serverKiller;
    
    public ServerKillerDisplayManager(ServerKiller serverKiller)
    {
        _serverKiller = serverKiller;
    }
    
    public void DisplayServerKiller()
    {
        var panel = CreateServerKillerPanel();
        AnsiConsole.Write(panel);
    }
    
    private Panel CreateServerKillerPanel()
    {
        var content = CreateServerKillerContent();
        var actionType = GetActionType();
        
        var panel = new Panel(content)
            .Header("[bold red]ServerKiller[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(actionType.BorderColor)
            .Padding(1, 0);
            
        return panel;
    }
    
    private string CreateServerKillerContent()
    {
        var lastAction = _serverKiller.LastAction;
        var timeSinceAction = DateTime.Now - _serverKiller.LastActionTime;
        var timeDisplay = FormatTimeSince(timeSinceAction);
        
        return $"""
                {GetActionPrefix(lastAction)} {lastAction}
                [dim]({timeDisplay} ago)[/]
                """;
    }
    
    private string GetActionPrefix(string action)
    {
        if (action.Contains("killed") || action.Contains("Killed"))
        {
            return "[red]KILL:[/]";
        }
        else if (action.Contains("revived") || action.Contains("Revived"))
        {
            return "[green]REVIVE:[/]";
        }
        else
        {
            return "[blue]STATUS:[/]";
        }
    }
    
    private (Color BorderColor, bool IsActive) GetActionType()
    {
        var lastAction = _serverKiller.LastAction;
        var timeSinceAction = DateTime.Now - _serverKiller.LastActionTime;
        
        // Recent action (within last 10 seconds) gets highlighted border
        var isRecent = timeSinceAction.TotalSeconds < 10;
        
        if (lastAction.Contains("killed") || lastAction.Contains("Killed"))
        {
            return (isRecent ? Color.Red : Color.DarkRed, isRecent);
        }
        else if (lastAction.Contains("revived") || lastAction.Contains("Revived"))
        {
            return (isRecent ? Color.Green : Color.DarkGreen, isRecent);
        }
        else
        {
            return (isRecent ? Color.Blue : Color.DarkBlue, isRecent);
        }
    }
    
    private static string FormatTimeSince(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
        }
        else
        {
            return $"{(int)timeSpan.TotalSeconds}s";
        }
    }
}