using Microsoft.Extensions.Configuration;

namespace LoadBalancerConsole;

public class ServerKiller : IDisposable
{
    private readonly List<FakeHttpServer> _servers;
    private readonly Timer _killerTimer;
    private readonly Random _random;
    private readonly TimeSpan _minInterval;
    private readonly TimeSpan _maxInterval;
    private readonly int _killProbability;
    private readonly int _reviveProbability;
    private string _lastAction = "ServerKiller activated";
    private DateTime _lastActionTime = DateTime.Now;

    public string LastAction => _lastAction;
    public DateTime LastActionTime => _lastActionTime;

    public ServerKiller(IEnumerable<FakeHttpServer> servers, TimeSpan? minInterval = null, TimeSpan? maxInterval = null, IConfiguration? configuration = null)
    {
        _servers = servers.ToList();
        _random = new Random();
        _minInterval = minInterval ?? TimeSpan.FromSeconds(configuration?.GetValue<int>("ServerKiller:MinIntervalSeconds", 5) ?? 5);
        _maxInterval = maxInterval ?? TimeSpan.FromSeconds(configuration?.GetValue<int>("ServerKiller:MaxIntervalSeconds", 15) ?? 15);
        _killProbability = configuration?.GetValue<int>("ServerKiller:KillProbabilityPercent", 70) ?? 70;
        _reviveProbability = configuration?.GetValue<int>("ServerKiller:ReviveProbabilityPercent", 80) ?? 80;
        
        // Start the killer timer with initial random delay
        var initialDelay = GetRandomInterval();
        _killerTimer = new Timer(ExecuteRandomKill, null, initialDelay, Timeout.InfiniteTimeSpan);
            }

    public void KillServer(FakeHttpServer server)
    {
        server.SetHealthy(false);
        UpdateLastAction($"Killed {server.ServerInfo.ServerId}");
    }

    public void ReviveServer(FakeHttpServer server)
    {
        server.SetHealthy(true);
        UpdateLastAction($"Revived {server.ServerInfo.ServerId}");
    }
    
    private void UpdateLastAction(string action)
    {
        _lastAction = action;
        _lastActionTime = DateTime.Now;
    }

    public void PrintStatus()
    {
        Console.WriteLine("ServerKiller Status Report:");
        foreach (var server in _servers)
        {
            var status = server.IsHealthy ? "ALIVE" : "DEAD";
            Console.WriteLine($"   {server.ServerInfo.ServerId}: {status}");
        }
    }

    private void ExecuteRandomKill(object? state)
    {
        if (!_servers.Any())
        {
            ScheduleNextKill();
            return;
        }

        var randomServer = _servers[_random.Next(_servers.Count)];
        var action = _random.Next(100);

        // chance to kill if healthy, chance to revive if dead
        if (randomServer.IsHealthy && action < _killProbability)
        {
            randomServer.SetHealthy(false);
            UpdateLastAction($"Auto-killed {randomServer.ServerInfo.ServerId}");
        }
        else if (!randomServer.IsHealthy && action < _reviveProbability)
        {
            randomServer.SetHealthy(true);
            UpdateLastAction($"Auto-revived {randomServer.ServerInfo.ServerId}");
        }

        ScheduleNextKill();
    }
    private void ScheduleNextKill()
    {
        var nextInterval = GetRandomInterval();
        _killerTimer.Change(nextInterval, Timeout.InfiniteTimeSpan);
    }

    private TimeSpan GetRandomInterval()
    {
        var minMs = (int)_minInterval.TotalMilliseconds;
        var maxMs = (int)_maxInterval.TotalMilliseconds;
        var randomMs = _random.Next(minMs, maxMs + 1);
        return TimeSpan.FromMilliseconds(randomMs);
    }

    public void Dispose()
    {
        _killerTimer?.Dispose();
        Console.WriteLine("ServerKiller deactivated");
    }
}