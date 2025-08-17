using System.Net;
using System.Text;
using System.Text.Json;

namespace LoadBalancerConsole.FakeServer;

public class FakeHttpServer
{
    private readonly HttpListener _listener;
    private readonly ServerInfo _serverInfo;
    private bool _isHealthy = true;
    private int? _forcedStatusCode = null;
    private int _activeConnections = 0;
    private const int MaxConnectionLimit = 10;

    public ServerInfo ServerInfo => _serverInfo;
    public bool IsHealthy => _isHealthy;
    public int ActiveConnections => _activeConnections;
    public int MaxConnections => MaxConnectionLimit;
    public bool HasAvailableConnections => _activeConnections < MaxConnectionLimit;

    public FakeHttpServer(int port, string serverId)
    {
        _serverInfo = new ServerInfo(port, serverId);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public FakeHttpServer(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{serverInfo.Port}/");
    }

    public async Task StartAsync()
    {
        _serverInfo.Status = ServerStatus.Starting;
        _listener.Start();
        _serverInfo.Status = ServerStatus.Healthy;
       

        while (true)
        {
            var context = await _listener.GetContextAsync();
            _ = Task.Run(() => HandleRequestAsync(context));
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        Interlocked.Increment(ref _activeConnections);
        
        try
        {
            var path = request.Url?.AbsolutePath;
            
            if (path == "/simulate-hung-connection")
            {
                await HandleHungConnectionAsync(response);
                return;
            }
            
            var (responseText, contentType) = GetResponse(path);
            
            response.StatusCode = _forcedStatusCode ?? (_isHealthy ? 200 : 503);
            response.ContentType = contentType;
            
            var buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in {_serverInfo.ServerId}: {ex.Message}");
            _serverInfo.Status = ServerStatus.Error;    
            response.StatusCode = 500;
            response.OutputStream.Close();
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }
    
    private async Task HandleHungConnectionAsync(HttpListenerResponse response)
    {
        try
        {
            response.StatusCode = 200;
            response.ContentType = "application/json";
            
            var responseData = new
            {
                serverId = _serverInfo.ServerId,
                status = "hung connection started",
                activeConnections = _activeConnections,
                maxConnections = MaxConnectionLimit,
                timestamp = DateTime.UtcNow
            };
            
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseData));
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
            
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hung connection in {_serverInfo.ServerId} ended: {ex.Message}");
        }
    }
    
    private (string responseText, string contentType) GetResponse(string? path)
    {
        return path switch
        {
            "/health" => (CreateHealthResponse(), "application/json"),
            "/helloworld" => ("hello world", "text/plain"),
            _ => ("default response", "application/json")
        };
    }
    private string CreateHealthResponse()
    {
        var healthData = new
        {
            serverId = _serverInfo.ServerId,
            status = _isHealthy ? "healthy" : "unhealthy",
            timestamp = DateTime.UtcNow,
            port = _serverInfo.Port,
            activeConnections = _activeConnections,
            maxConnections = MaxConnectionLimit,
            hasAvailableConnections = HasAvailableConnections
        };
        
        return JsonSerializer.Serialize(healthData);
    }

    // make this method public so serverkiller can directly affect status, which wouldn't happen in a normal scenario
    public void SetHealthy(bool isHealthy)
    {
        _isHealthy = isHealthy;
        _serverInfo.Status = isHealthy ? ServerStatus.Healthy : ServerStatus.Unhealthy;
    }
    // make this method public so that we can test forced status codes
    public void SetForcedStatusCode(int? statusCode)
    {
        _forcedStatusCode = statusCode;
    }
}