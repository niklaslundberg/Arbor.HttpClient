namespace Arbor.HttpClient.Core.Collections;

public sealed record Collection(
    int Id,
    string Name,
    string? SourcePath,
    string? BaseUrl,
    IReadOnlyList<CollectionRequest> Requests);
