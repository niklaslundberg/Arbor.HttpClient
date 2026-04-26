using System;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Arbor.HttpClient.Desktop.Demo;

/// <summary>
/// An embedded Kestrel HTTP server that provides demo endpoints for the local demo collection.
/// The server is started and stopped on demand and is never running by default.
/// Endpoints: <c>/echo</c>, <c>/sse</c>, <c>/ws</c>, <c>/status</c>.
/// </summary>
public sealed class DemoServer : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>Default port used when no explicit port is specified.</summary>
    public const int DefaultPort = 5999;

    /// <summary>Gets a value indicating whether the demo server is currently running.</summary>
    public bool IsRunning => _app is not null;

    /// <summary>Gets the port the server is (or was last) started on.</summary>
    public int Port { get; private set; } = DefaultPort;

    /// <summary>Starts the demo server on the specified <paramref name="port"/>. No-op if already running.</summary>
    public async Task StartAsync(int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        Port = port;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
        });

        var app = builder.Build();
        app.UseWebSockets();

        // /status — returns a JSON summary of the demo server.
        app.MapGet("/status", (HttpContext context) =>
        {
            context.Response.ContentType = "application/json";
            return Results.Ok(new
            {
                server = "Arbor.HttpClient Demo Server",
                version = "1.0",
                port,
                endpoints = new[] { "/echo", "/sse", "/ws", "/status" }
            });
        });

        // /echo — HTTP echo: reflects the request body back as the response body.
        // Returns 200 OK. When there is no body, returns a JSON summary of the
        // request method, path, and query string.
        app.Map("/echo", async context =>
        {
            context.Response.StatusCode = 200;
            if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                context.Response.ContentType = context.Request.ContentType ?? "application/json";
                await context.Request.Body.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            else
            {
                context.Response.ContentType = "application/json";
                var info = new
                {
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    query = context.Request.QueryString.Value,
                    timestamp = DateTimeOffset.UtcNow
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(info), context.RequestAborted);
            }
        });

        // /sse — streams five numbered SSE events 500 ms apart, then closes.
        app.Map("/sse", async context =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            for (var i = 1; i <= 5; i++)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    break;
                }

                var payload = JsonSerializer.Serialize(new { @event = i, timestamp = DateTimeOffset.UtcNow });
                await context.Response.WriteAsync($"data: {payload}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);

                try
                {
                    await Task.Delay(500, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        // /ws — WebSocket echo: reflects every text frame back to the sender.
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[8192];

            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
                {
                    if (ex is WebSocketException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DemoServer] WebSocket receive ended with an error: {ex.Message}");
                    }
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Echo close", CancellationToken.None);
                    return;
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    context.RequestAborted);
            }
        });

        _app = app;
        await app.StartAsync(cancellationToken);
    }

    /// <summary>Stops the demo server gracefully. No-op if not running.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is { } app)
        {
            _app = null;
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();
}
