namespace Arbor.HttpClient.Core.Models;

public sealed record CollectionRequest(string Name, string Method, string Path, string? Description);
