namespace Arbor.HttpClient.Core.Models;

public sealed record SavedRequest(string Name, string Method, string Url, string? Body, DateTimeOffset CreatedAtUtc);
