using LoadBalancerConsole;

var fakeHttpServer = new FakeHttpServer(8001, "1");

Console.WriteLine("Starting fake HTTP server...");
await fakeHttpServer.StartAsync();