using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arbor.HttpClient.Core.Integration.Tests;

/// <summary>
/// xUnit class fixture that starts a real Kestrel server exposing WebSocket and SSE
/// endpoints used by the integration tests.  One instance is shared across all tests
/// in the collection so the server is only started once per test class.
/// </summary>
public sealed class KestrelServerFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Base URL for the WebSocket echo endpoint (<c>/ws</c>).</summary>
    public string WebSocketEchoUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Base URL for the WebSocket endpoint that abruptly drops the TCP connection
    /// (<c>/ws-drop</c>), exercising the <c>WebSocketException</c> catch path.
    /// </summary>
    public string WebSocketDropUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Base URL for the WebSocket endpoint that sends the value of the
    /// <c>X-Test-Header</c> request header back as a text frame (<c>/ws-headers</c>).
    /// </summary>
    public string WebSocketHeadersUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Base URL for the WebSocket endpoint that sends two text fragments in a single
    /// logical message (<c>/ws-fragment</c>), exercising the multi-segment receive path.
    /// </summary>
    public string WebSocketFragmentUrl { get; private set; } = string.Empty;

    /// <summary>Base URL for the SSE endpoint (<c>/sse</c>).</summary>
    public string SseUrl { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        var app = builder.Build();
        app.UseWebSockets();

        // /ws — echo server: reflects every text frame back to the sender.
        // Handles the WebSocket close handshake gracefully.
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
                catch
                {
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

        // /ws-drop — accepts the WebSocket upgrade, sends a "ready" frame to confirm the
        // connection, waits for the client to send an "ack", then abruptly aborts the
        // underlying TCP connection to trigger a WebSocketException in the receive loop.
        app.Map("/ws-drop", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();

            // Tell the client the connection is ready.
            var ready = Encoding.UTF8.GetBytes("ready");
            await ws.SendAsync(new ArraySegment<byte>(ready), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

            // Wait for the client's acknowledgment so we know it is in the receive loop.
            var buffer = new byte[256];
            try
            {
                var ack = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                if (ack.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            // Client is in the receive loop — abort the TCP connection without a close frame.
            context.Abort();
            await Task.Yield();
        });

        // /ws-headers — sends the value of X-Test-Header back as a text frame, then
        // waits for the client to initiate the close handshake.
        app.Map("/ws-headers", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var headerValue = context.Request.Headers["X-Test-Header"].FirstOrDefault() ?? "(not set)";
            using var ws = await context.WebSockets.AcceptWebSocketAsync();

            var payload = Encoding.UTF8.GetBytes(headerValue);
            await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, endOfMessage: true, context.RequestAborted);

            var buffer = new byte[256];
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Headers echo close", CancellationToken.None);
                    return;
                }
            }
        });

        // /ws-fragment — sends a logical message split across two frames
        // (endOfMessage:false then endOfMessage:true), then waits for close.
        app.Map("/ws-fragment", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();

            var part1 = Encoding.UTF8.GetBytes("Hello");
            var part2 = Encoding.UTF8.GetBytes(" World");

            await ws.SendAsync(new ArraySegment<byte>(part1), WebSocketMessageType.Text, endOfMessage: false, context.RequestAborted);
            await ws.SendAsync(new ArraySegment<byte>(part2), WebSocketMessageType.Text, endOfMessage: true, context.RequestAborted);

            var buffer = new byte[256];
            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fragment close", CancellationToken.None);
                    return;
                }
            }
        });

        // /sse — streams two SSE events and then closes the response body.
        app.Map("/sse", async context =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";

            await context.Response.WriteAsync("data: event1\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await context.Response.WriteAsync("data: event2\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        });

        _app = app;
        await app.StartAsync();

        var serverAddresses = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        var baseUri = new Uri(serverAddresses);
        var host = $"{baseUri.Host}:{baseUri.Port}";

        WebSocketEchoUrl = $"ws://{host}/ws";
        WebSocketDropUrl = $"ws://{host}/ws-drop";
        WebSocketHeadersUrl = $"ws://{host}/ws-headers";
        WebSocketFragmentUrl = $"ws://{host}/ws-fragment";
        SseUrl = $"http://{host}/sse";
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
