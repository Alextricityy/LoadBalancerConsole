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
        Console.WriteLine($"ServerKiller: Manually killed {server.ServerInfo.ServerId} (port {server.ServerInfo.Port})");
    }

    public void ReviveServer(FakeHttpServer server)
    {
        server.SetHealthy(true);
        Console.WriteLine($"ServerKiller: Manually revived {server.ServerInfo.ServerId} (port {server.ServerInfo.Port})");
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
        }
        else if (!randomServer.IsHealthy && action < _reviveProbability)
        {
            randomServer.SetHealthy(true);
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