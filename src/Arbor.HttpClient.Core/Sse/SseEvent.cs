namespace Arbor.HttpClient.Core.Sse;

/// <summary>A parsed Server-Sent Events (SSE) event as defined by the W3C EventSource specification.</summary>
public sealed record SseEvent(
    string? Id,
    string? EventType,
    string Data,
    DateTimeOffset Timestamp);
