using System;

namespace Arbor.HttpClient.Desktop.Shared;

internal static class HttpContentTypeHelper
{
    public static string NormalizeMediaType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var semicolonIndex = contentType.IndexOf(';');
        var mediaType = semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType;
        return mediaType.Trim().ToLowerInvariant();
    }

    public static bool IsJsonMediaType(string mediaType) =>
        mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.Ordinal);

    public static bool IsXmlMediaType(string mediaType) =>
        mediaType is "application/xml" or "text/xml" || mediaType.EndsWith("+xml", StringComparison.Ordinal);

    public static bool IsHtmlMediaType(string mediaType) =>
        mediaType == "text/html";
}
