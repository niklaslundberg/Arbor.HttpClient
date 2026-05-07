namespace Arbor.HttpClient.Core.HttpRequest;

public sealed record HttpRequestDraft(
    string Name,
    string Method,
    string Url,
    string? Body,
    IReadOnlyList<RequestHeader>? Headers = null,
    Version? HttpVersion = null,
    bool? FollowRedirects = null,
    int? TimeoutSeconds = null);
