namespace Arbor.HttpClient.Core.Models;

public sealed record HttpRequestDraft(
    string Name,
    string Method,
    string Url,
    string? Body,
    IReadOnlyList<RequestHeader>? Headers = null,
    Version? HttpVersion = null);
