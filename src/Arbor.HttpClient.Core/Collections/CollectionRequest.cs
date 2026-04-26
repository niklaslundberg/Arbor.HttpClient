namespace Arbor.HttpClient.Core.Collections;

public sealed record CollectionRequest(string Name, string Method, string Path, string? Description, string? Notes = null);
