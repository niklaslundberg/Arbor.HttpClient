namespace Arbor.HttpClient.Core.HttpRequest;

/// <summary>
/// Discriminates the protocol / request style used by the request composer.
/// </summary>
public enum RequestType
{
    /// <summary>Standard HTTP / REST request (GET, POST, PUT, PATCH, DELETE, …).</summary>
    Http,

    /// <summary>GraphQL query or mutation sent as an HTTP POST with a JSON body.</summary>
    GraphQL,

    /// <summary>Long-lived WebSocket connection (ws:// or wss://).</summary>
    WebSocket,

    /// <summary>Server-Sent Events stream consumed over an HTTP GET with <c>text/event-stream</c> content-type.</summary>
    Sse,

    /// <summary>gRPC unary RPC over HTTP/2 using a user-supplied .proto schema.</summary>
    GrpcUnary
}
