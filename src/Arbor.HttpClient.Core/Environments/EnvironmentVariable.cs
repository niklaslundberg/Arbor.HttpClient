namespace Arbor.HttpClient.Core.Environments;

public sealed record EnvironmentVariable(
    string Name,
    string Value,
    bool IsEnabled = true,
    bool IsSensitive = false,
    DateTimeOffset? ExpiresAtUtc = null)
{
    /// <summary>Returns <c>true</c> when an expiry has been set and the current UTC time is past that expiry.</summary>
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTimeOffset.UtcNow >= ExpiresAtUtc.Value;
}
