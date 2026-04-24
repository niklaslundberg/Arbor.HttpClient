using System.Net.WebSockets;
using System.Text;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

/// <summary>
/// Manages a single WebSocket connection.  Call <see cref="ConnectAsync"/> to open the
/// connection, then use <see cref="SendMessageAsync"/> to push text frames.  Incoming
/// frames are delivered to the <c>onMessage</c> callback supplied when connecting.
/// Call <see cref="DisconnectAsync"/> (or cancel the token) to close the connection.
/// </summary>
public sealed class WebSocketService : IDisposable
{
    private ClientWebSocket? _webSocket;
    private bool _disposed;

    /// <summary>
    /// <c>true</c> when a WebSocket connection is currently in the <see cref="WebSocketState.Open"/> state.
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Opens a WebSocket connection to <paramref name="url"/> (ws:// or wss://).
    /// Incoming text frames are delivered synchronously to <paramref name="onMessage"/>
    /// from a background receive loop.
    /// </summary>
    /// <param name="url">WebSocket endpoint URL.</param>
    /// <param name="onMessage">Callback invoked for every received frame.</param>
    /// <param name="onDisconnected">Optional callback invoked when the connection is closed.</param>
    /// <param name="additionalHeaders">Optional HTTP upgrade request headers.</param>
    /// <param name="cancellationToken">Token that aborts both the connection and the receive loop.</param>
    public async Task ConnectAsync(
        string url,
        Action<WebSocketMessage> onMessage,
        Action? onDisconnected = null,
        IReadOnlyList<RequestHeader>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme is not ("ws" or "wss")))
        {
            throw new ArgumentException("URL must be an absolute ws:// or wss:// WebSocket URL", nameof(url));
        }

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        if (additionalHeaders is not null)
        {
            foreach (var header in additionalHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
            {
                _webSocket.Options.SetRequestHeader(header.Name, header.Value);
            }
        }

        await _webSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

        // Start the receive loop on a background thread; we don't await it here
        _ = ReceiveLoopAsync(_webSocket, onMessage, onDisconnected, cancellationToken);
    }

    /// <summary>Sends a UTF-8 text frame to the server.</summary>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates a graceful WebSocket close handshake.  The receive loop will exit after
    /// the server acknowledges the close.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Closed by user",
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _webSocket?.Dispose();
        _webSocket = null;
    }

    private static async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        Action<WebSocketMessage> onMessage,
        Action? onDisconnected,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var text = Encoding.UTF8.GetString(ms.ToArray());
                onMessage(new WebSocketMessage(text, WebSocketMessageDirection.Received, DateTimeOffset.UtcNow));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation – swallow
        }
        catch (WebSocketException)
        {
            // Connection dropped – swallow and let the caller discover via IsConnected
        }
        finally
        {
            onDisconnected?.Invoke();
        }
    }
}
