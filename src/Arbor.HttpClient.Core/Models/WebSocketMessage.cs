namespace Arbor.HttpClient.Core.Models;

/// <summary>Direction of a WebSocket frame relative to the client.</summary>
public enum WebSocketMessageDirection
{
    /// <summary>Frame was sent by the client to the server.</summary>
    Sent,

    /// <summary>Frame was received from the server.</summary>
    Received
}

/// <summary>A single WebSocket text frame with direction metadata.</summary>
public sealed record WebSocketMessage(
    string Content,
    WebSocketMessageDirection Direction,
    DateTimeOffset Timestamp);
