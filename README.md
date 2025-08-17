# Load Balancer Console
A simple .NET 9.0 console application demonstrating HTTP server health monitoring and load balancing with an interactive CLI interface.

![Demo](LoadBalancerConsole/Demo.gif)

## Features

### ✅ What it can do:
- **Round Robin Load Balancing**: Distributes requests evenly across available servers
- **Connection Simulation**: Simulates hung connections with automatic failover to next available server
- **Server Management**: ServerKiller component simulates server failures and recovery based on configurable percentages
- **Health Monitoring**: Ensures connections receive 200 responses, automatically retries with different servers
- **Interactive CLI**: Beautiful server health visualization and connection simulation using Spectre.Console

### ❌ What it doesn't do:
- Track individual request/response metrics per server
- Act as a reverse proxy (servers call load balancer on port 9000 instead)

## Quick Start

```bash
# Build and run the application
dotnet build
dotnet run --project LoadBalancerConsole

# Run tests
dotnet test
```

The application starts 5 HTTP servers on ports 8001-8005 with an interactive menu for testing load balancer functionality.

## Architecture

- **ServerInfo.cs**: Immutable server configuration and health tracking
- **FakeHttpServer.cs**: Simple HTTP server with health endpoints
- **LoadBalancer.cs**: Health monitoring and server selection logic
- **Interactive Menu**: CLI interface for testing and visualization



