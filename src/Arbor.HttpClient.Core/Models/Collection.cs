namespace Arbor.HttpClient.Core.Models;

public sealed record Collection(
    int Id,
    string Name,
    string? SourcePath,
    string? BaseUrl,
    IReadOnlyList<CollectionRequest> Requests);
