namespace Arbor.HttpClient.Desktop.Features.Collections;

public static class CollectionUrlHelper
{
    public static string BuildFullUrl(string? baseUrl, string path)
    {
        if (IsAbsoluteWebUrl(path) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        return JoinBaseUrlAndPath(baseUrl, path);
    }

    public static bool IsAbsoluteWebUrl(string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" or "ws" or "wss";

    public static string JoinBaseUrlAndPath(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return baseUrl;
        }

        var baseEndsWithSlash = baseUrl.EndsWith("/", StringComparison.Ordinal);
        var pathStartsWithSlash = path.StartsWith("/", StringComparison.Ordinal);

        if (baseEndsWithSlash && pathStartsWithSlash)
        {
            return baseUrl + path[1..];
        }

        if (!baseEndsWithSlash && !pathStartsWithSlash)
        {
            return baseUrl + "/" + path;
        }

        return baseUrl + path;
    }
}
