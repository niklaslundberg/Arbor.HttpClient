using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Collections;

public sealed record Collection(
    int Id,
    string Name,
    string? SourcePath,
    string? BaseUrl,
    IReadOnlyList<CollectionRequest> Requests,
    IReadOnlyList<RequestHeader>? Headers = null);
