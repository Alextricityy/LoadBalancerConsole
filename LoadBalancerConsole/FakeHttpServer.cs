using System.Net;
using System.Text;
using System.Text.Json;

namespace LoadBalancerConsole;

public class FakeHttpServer
{
    private readonly HttpListener _listener;
    private readonly ServerInfo _serverInfo;
    private bool _isHealthy = true;

    public ServerInfo ServerInfo => _serverInfo;
    public bool IsHealthy => _isHealthy;

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
       
        Console.WriteLine($"Server {_serverInfo.ServerId} started on port {_serverInfo.Port}");

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

        try
        {
            var (responseText, contentType) = GetResponse(request.Url?.AbsolutePath);
            
            response.StatusCode = _isHealthy ? 200 : 503;
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
            port = _serverInfo.Port
        };
        
        return JsonSerializer.Serialize(healthData);
    }

// make this method public so serverkiller can directly affect status, which wouldn't happen in a normal scenario
    public void SetHealthy(bool isHealthy)
    {
        _isHealthy = isHealthy;
        _serverInfo.Status = isHealthy ? ServerStatus.Healthy : ServerStatus.Unhealthy;
    }
}