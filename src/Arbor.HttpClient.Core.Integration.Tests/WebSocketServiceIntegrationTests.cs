using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.WebSocket;

namespace Arbor.HttpClient.Core.Integration.Tests;

/// <summary>
/// System integration tests for <see cref="WebSocketService"/> that run against a real
/// in-process Kestrel server.  These tests cover the I/O paths that cannot be reached
/// by unit tests (ConnectAsync body, ReceiveLoopAsync, SendMessageAsync, DisconnectAsync).
/// </summary>
[Collection("KestrelServer")]
public sealed class WebSocketServiceIntegrationTests(KestrelServerFixture fixture)
{
    // ── ConnectAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_WithEchoServer_SetsIsConnectedTrue()
    {
        using var service = new WebSocketService();

        await service.ConnectAsync(fixture.WebSocketEchoUrl, _ => { });

        service.IsConnected.Should().BeTrue();

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_WithCustomHeaders_ForwardsHeadersToServer()
    {
        using var service = new WebSocketService();
        using var received = new SemaphoreSlim(0, 1);
        string? receivedContent = null;

        var headers = new[]
        {
            new RequestHeader("X-Test-Header", "my-test-value"),
            new RequestHeader("X-Disabled", "ignored", IsEnabled: false),
            new RequestHeader("  ", "blank-name-ignored"),
        };

        await service.ConnectAsync(
            fixture.WebSocketHeadersUrl,
            msg =>
            {
                receivedContent = msg.Content;
                received.Release();
            },
            additionalHeaders: headers);

        var gotMessage = await received.WaitAsync(TimeSpan.FromSeconds(10));
        gotMessage.Should().BeTrue("the header-echo server should have sent a frame");
        receivedContent.Should().Be("my-test-value");

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_WhenCalledTwice_DisposesOldSocketAndReconnects()
    {
        using var service = new WebSocketService();

        await service.ConnectAsync(fixture.WebSocketEchoUrl, _ => { });
        await service.DisconnectAsync();

        // _webSocket is now not null (but Closed); calling ConnectAsync again exercises
        // the _webSocket?.Dispose() branch on line 48 of WebSocketService.cs.
        await service.ConnectAsync(fixture.WebSocketEchoUrl, _ => { });
        service.IsConnected.Should().BeTrue();

        await service.DisconnectAsync();
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_ToEchoServer_MessageIsEchoedBack()
    {
        using var service = new WebSocketService();
        using var received = new SemaphoreSlim(0, 1);
        WebSocketMessage? receivedMessage = null;

        await service.ConnectAsync(
            fixture.WebSocketEchoUrl,
            msg =>
            {
                receivedMessage = msg;
                received.Release();
            });

        await service.SendMessageAsync("hello integration test");

        var gotMessage = await received.WaitAsync(TimeSpan.FromSeconds(10));
        gotMessage.Should().BeTrue("the echo server should have reflected the frame");
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Content.Should().Be("hello integration test");
        receivedMessage.Direction.Should().Be(WebSocketMessageDirection.Received);

        await service.DisconnectAsync();
    }

    // ── DisconnectAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_AfterConnect_ClosesConnectionGracefully()
    {
        using var service = new WebSocketService();
        using var disconnected = new SemaphoreSlim(0, 1);

        await service.ConnectAsync(
            fixture.WebSocketEchoUrl,
            _ => { },
            onDisconnected: () => disconnected.Release());

        await service.DisconnectAsync();

        var wasDisconnected = await disconnected.WaitAsync(TimeSpan.FromSeconds(10));
        wasDisconnected.Should().BeTrue("the receive loop should have exited after the close handshake");
        service.IsConnected.Should().BeFalse();
    }

    // ── ReceiveLoopAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReceiveLoop_WithFragmentedMessage_AssemblesCompleteMessage()
    {
        using var service = new WebSocketService();
        using var received = new SemaphoreSlim(0, 1);
        WebSocketMessage? receivedMessage = null;

        await service.ConnectAsync(
            fixture.WebSocketFragmentUrl,
            msg =>
            {
                receivedMessage = msg;
                received.Release();
            });

        var gotMessage = await received.WaitAsync(TimeSpan.FromSeconds(10));
        gotMessage.Should().BeTrue("the server should have sent a fragmented text frame");
        receivedMessage!.Content.Should().Be("Hello World");

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ReceiveLoop_WhenCancelled_ExitsCleanly()
    {
        using var service = new WebSocketService();
        using var cts = new CancellationTokenSource();
        using var completed = new SemaphoreSlim(0, 1);

        await service.ConnectAsync(
            fixture.WebSocketEchoUrl,
            _ => { },
            onDisconnected: () => completed.Release(),
            cancellationToken: cts.Token);

        await cts.CancelAsync();

        var finished = await completed.WaitAsync(TimeSpan.FromSeconds(10));
        finished.Should().BeTrue("cancellation should cause the receive loop to exit via OperationCanceledException");
    }

    [Fact]
    public async Task ReceiveLoop_WhenConnectionDropped_CatchesWebSocketExceptionAndExits()
    {
        using var service = new WebSocketService();
        using var readyReceived = new SemaphoreSlim(0, 1);
        using var completed = new SemaphoreSlim(0, 1);

        await service.ConnectAsync(
            fixture.WebSocketDropUrl,
            msg =>
            {
                if (msg.Content == "ready")
                {
                    readyReceived.Release();
                }
            },
            onDisconnected: () => completed.Release());

        // Wait for the server's "ready" frame to confirm the connection is established.
        var gotReady = await readyReceived.WaitAsync(TimeSpan.FromSeconds(10));
        gotReady.Should().BeTrue("the drop-server should have sent a ready frame");

        // Send ack so the server knows the client is in the receive loop, then aborts.
        await service.SendMessageAsync("ack");

        // Wait for the receive loop to exit after the WebSocketException.
        var finished = await completed.WaitAsync(TimeSpan.FromSeconds(10));
        finished.Should().BeTrue("an abruptly dropped connection should cause the receive loop to exit via WebSocketException");
        service.IsConnected.Should().BeFalse();
    }
}
