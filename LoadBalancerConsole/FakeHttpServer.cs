using System.Net;
using System.Text;
using System.Text.Json;

namespace LoadBalancerConsole;

public class FakeHttpServer
{
    private readonly HttpListener _listener;
    private readonly int _port;
    private readonly string _serverId;
    private bool _isHealthy;

    public FakeHttpServer(int port, string serverId)
    {
        _port = port;
        _serverId = serverId;
        _isHealthy = true;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Server {_serverId} started on port {_port}");

        while (1 == 1)
        {
            var context = await _listener.GetContextAsync();
            _ = Task.Run(() => HandleRequest(context));
        }
    }
    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            string responseText;
            int statusCode = 200;

            switch (request.Url?.AbsolutePath)
            {
                case "/health":
                    statusCode = _isHealthy ? 200 : 503;
                    responseText = JsonSerializer.Serialize(new
                    {
                        serverId = _serverId,
                        status = _isHealthy ? "healthy" : "unhealthy",
                        timestamp = DateTime.UtcNow
                    });
                    break;

                case "/helloworld":
                    responseText = "hello world";
                    response.ContentType = "text/plain";
                    break;

                default:
                    responseText = "default response";
                    break;
            }

            response.StatusCode = statusCode;
            if (response.ContentType == null)
                response.ContentType = "application/json";

            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request on server {_serverId}: {ex.Message}");
            response.StatusCode = 500;
            response.OutputStream.Close();
        }
    }
}