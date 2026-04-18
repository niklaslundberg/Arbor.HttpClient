using System.Globalization;

namespace Arbor.HttpClient.Core.Models;

public sealed record SavedRequest(string Name, string Method, string Url, string? Body, DateTimeOffset CreatedAtUtc)
{
    public string CreatedAtLocalDisplay => CreatedAtUtc
        .ToLocalTime()
        .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
