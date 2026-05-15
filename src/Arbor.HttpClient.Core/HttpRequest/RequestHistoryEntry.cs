using System.Globalization;

namespace Arbor.HttpClient.Core.HttpRequest;

/// <summary>
/// Represents one persisted entry in the recent request history.
/// </summary>
/// <param name="Name">The display name captured when the request was sent.</param>
/// <param name="Method">The HTTP method captured when the request was sent.</param>
/// <param name="Url">The resolved URL captured when the request was sent.</param>
/// <param name="Body">The request body captured when the request was sent, if any.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the request was recorded.</param>
public sealed record RequestHistoryEntry(string Name, string Method, string Url, string? Body, DateTimeOffset CreatedAtUtc)
{
    /// <summary>Gets the creation timestamp formatted in local time for display.</summary>
    public string CreatedAtLocalDisplay => CreatedAtUtc
        .ToLocalTime()
        .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
