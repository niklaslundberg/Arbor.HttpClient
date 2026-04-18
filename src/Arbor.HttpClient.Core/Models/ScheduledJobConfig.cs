namespace Arbor.HttpClient.Core.Models;

public sealed record ScheduledJobConfig(
    int Id,
    string Name,
    string Method,
    string Url,
    string? Body,
    string? HeadersJson,
    int IntervalSeconds,
    bool AutoStart,
    bool? FollowRedirects = null);
