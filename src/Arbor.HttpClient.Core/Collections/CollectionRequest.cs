using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Collections;

public sealed record CollectionRequest(
    string Name,
    string Method,
    string Path,
    string? Description,
    string? Notes = null,
    string? Tag = null,
    string? Body = null,
    string? ContentType = null,
    IReadOnlyList<RequestHeader>? Headers = null);
