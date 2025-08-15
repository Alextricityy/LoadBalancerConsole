using System.Net;
using System.Text;

namespace LoadBalancerConsole;
public class LoadBalancerProxy : IDisposable
{
    private readonly HttpListener _listener;
    private readonly LoadBalancer _loadBalancer;
    private readonly HttpClient _httpClient;
    private readonly int _proxyPort;
    private int _currentServerIndex = 0;
    private readonly object _indexLock = new object();

    public LoadBalancerProxy(int proxyPort, LoadBalancer loadBalancer)
    {
        _proxyPort = proxyPort;
        _loadBalancer = loadBalancer;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{proxyPort}/");
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"LoadBalancer Proxy started on port {_proxyPort}");
        Console.WriteLine($"Access your load-balanced service at: http://localhost:{_proxyPort}/");

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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var healthyServers = _loadBalancer.GetHealthyServers();
            
            if (!healthyServers.Any())
            {
                await SendErrorResponse(response, 503, "No healthy servers available", stopwatch.Elapsed);
                return;
            }

            var success = false;
            var attemptsCount = 0;
            var maxAttempts = Math.Min(healthyServers.Count, 3); // Try up to 3 servers or all available

            while (!success && attemptsCount < maxAttempts)
            {
                var targetServer = GetNextHealthyServer(healthyServers);
                if (targetServer == null)
                {
                    break;
                }

                try
                {
                    success = await ForwardRequestAsync(request, response, targetServer, stopwatch);
                    if (success)
                    {
                        LogRequest(request, targetServer, stopwatch.Elapsed, attemptsCount + 1);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to forward to {targetServer.ServerId}: {ex.Message}");
                }

                attemptsCount++;
            }

            if (!success)
            {
                await SendErrorResponse(response, 502, "All backend servers failed", stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Proxy error: {ex.Message}");
            await SendErrorResponse(response, 500, "Internal proxy error", stopwatch.Elapsed);
        }
    }

    private ServerInfo? GetNextHealthyServer(List<ServerInfo> healthyServers)
    {
        if (!healthyServers.Any())
            return null;

        lock (_indexLock)
        {
            // Ensure index is within bounds of current healthy servers
            _currentServerIndex = _currentServerIndex % healthyServers.Count;
            var server = healthyServers[_currentServerIndex];
            _currentServerIndex = (_currentServerIndex + 1) % healthyServers.Count;
            return server;
        }
    }
    private async Task<bool> ForwardRequestAsync(HttpListenerRequest incomingRequest, HttpListenerResponse outgoingResponse, ServerInfo targetServer, System.Diagnostics.Stopwatch stopwatch)
    {
        try
        {
            var targetUrl = $"{targetServer.BaseUrl}{incomingRequest.Url?.PathAndQuery}";
            
            using var httpRequest = new HttpRequestMessage(new HttpMethod(incomingRequest.HttpMethod), targetUrl);
            
            foreach (string headerName in incomingRequest.Headers.AllKeys)
            {
                if (!ShouldSkipHeader(headerName))
                {
                    httpRequest.Headers.TryAddWithoutValidation(headerName, incomingRequest.Headers[headerName]);
                }
            }

            if (incomingRequest.HasEntityBody)
            {
                using var requestStream = incomingRequest.InputStream;
                var bodyBytes = new byte[incomingRequest.ContentLength64];
                await requestStream.ReadExactlyAsync(bodyBytes);
                httpRequest.Content = new ByteArrayContent(bodyBytes);
                
                if (!string.IsNullOrEmpty(incomingRequest.ContentType))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", incomingRequest.ContentType);
                }
            }

            using var backendResponse = await _httpClient.SendAsync(httpRequest);
            
            outgoingResponse.StatusCode = (int)backendResponse.StatusCode;
            
            foreach (var header in backendResponse.Headers)
            {
                outgoingResponse.Headers[header.Key] = string.Join(",", header.Value);
            }
            
            if (backendResponse.Content.Headers != null)
            {
                foreach (var header in backendResponse.Content.Headers)
                {
                    outgoingResponse.Headers[header.Key] = string.Join(",", header.Value);
                }
            }
            var responseBytes = await backendResponse.Content.ReadAsByteArrayAsync();
            outgoingResponse.ContentLength64 = responseBytes.Length;
            await outgoingResponse.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            outgoingResponse.OutputStream.Close();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    private static bool ShouldSkipHeader(string headerName)
    {
        var skipHeaders = new[] { "host", "connection", "content-length", "transfer-encoding", "upgrade" };
        return skipHeaders.Contains(headerName.ToLowerInvariant());
    }

    private static async Task SendErrorResponse(HttpListenerResponse response, int statusCode, string message, TimeSpan elapsed)
    {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            
            var errorResponse = $@"{{
                ""error"": ""{message}"",
                ""statusCode"": {statusCode},
                ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}"",
                ""processingTime"": ""{elapsed.TotalMilliseconds:F0}ms""
            }}";
            
            var buffer = Encoding.UTF8.GetBytes(errorResponse);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
    }

    private static void LogRequest(HttpListenerRequest request, ServerInfo targetServer, TimeSpan elapsed, int attempts)
    {
        var attemptsText = attempts > 1 ? $" (attempt {attempts})" : "";
        Console.WriteLine($"{request.HttpMethod} {request.Url?.PathAndQuery} → {targetServer.ServerId} ({elapsed.TotalMilliseconds:F0}ms){attemptsText}");
    }

    public void Dispose()
    {
        _listener?.Stop();
        _listener?.Close();
        _httpClient?.Dispose();
        Console.WriteLine("LoadBalancer Proxy stopped");
    }
}