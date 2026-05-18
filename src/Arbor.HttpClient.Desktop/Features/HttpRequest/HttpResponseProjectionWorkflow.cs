using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Projects HTTP response details to UI-friendly display values and content formatting.
/// </summary>
public sealed class HttpResponseProjectionWorkflow
{
    public HttpResponseProjectionResult BuildProjection(HttpResponseDetails response)
    {
        var responseContentType = GetResponseContentType(response.Headers);
        var mediaType = HttpContentTypeHelper.NormalizeMediaType(responseContentType);
        var responseKind = DetermineResponseKind(mediaType);
        var responseBodyTabLabel = responseKind switch
        {
            ResponseKind.Json => "JSON",
            ResponseKind.Xml => "XML",
            _ => "Body"
        };

        var bodyProjection = BuildBodyProjection(response.Body, mediaType, responseKind);

        var responseHeaders = response.Headers
            .Select(header => $"{header.Name}: {header.Value}")
            .ToList();

        var responseRawText = responseHeaders.Count == 0
            ? bodyProjection.RawResponseBody
            : $"{string.Join(Environment.NewLine, responseHeaders)}{Environment.NewLine}{Environment.NewLine}{bodyProjection.RawResponseBody}";

        return new HttpResponseProjectionResult(
            ResponseStatus: $"{response.StatusCode} {response.ReasonPhrase}",
            ResponseStatusCode: response.StatusCode,
            ResponseTimeDisplay: FormatElapsedMilliseconds(response.ElapsedMilliseconds),
            ResponseSizeDisplay: FormatByteSize(response.BodyBytes?.LongLength ?? 0),
            LastResponseBodyBytes: response.BodyBytes ?? [],
            RawResponseBody: bodyProjection.RawResponseBody,
            ResponseHeaders: responseHeaders,
            HasResponseHeaders: responseHeaders.Count > 0,
            ResponseContentType: responseContentType,
            ResponseBodyTabLabel: responseBodyTabLabel,
            IsBinaryResponse: bodyProjection.IsBinaryResponse,
            ResponseBody: bodyProjection.ResponseBody,
            IsResponseWebViewAvailable: bodyProjection.IsResponseWebViewAvailable,
            ResponseWebViewUri: bodyProjection.ResponseWebViewUri,
            ResponseRawText: responseRawText,
            HasTextResponse: responseHeaders.Count > 0 && !bodyProjection.IsBinaryResponse);
    }

    private static BodyProjection BuildBodyProjection(string responseBody, string mediaType, ResponseKind responseKind)
    {
        if (responseKind == ResponseKind.Binary)
        {
            var binaryBody = $"Binary response ({mediaType}). Use \"Save and Open\" to inspect the content.";
            return new BodyProjection(binaryBody, binaryBody, true, false, "about:blank");
        }

        var projectedBody = responseBody;
        var isResponseWebViewAvailable = false;
        var responseWebViewUri = "about:blank";

        if (responseKind == ResponseKind.Json)
        {
            if (TryFormatJson(responseBody, out var formattedJson))
            {
                projectedBody = formattedJson;
            }
        }
        else if (responseKind is ResponseKind.Xml or ResponseKind.Html)
        {
            if (TryFormatXml(responseBody, out var formattedXml))
            {
                projectedBody = formattedXml;
            }

            if (responseKind == ResponseKind.Html && TryBuildResponseWebViewUri(responseBody, out var webViewUri))
            {
                isResponseWebViewAvailable = true;
                responseWebViewUri = webViewUri;
            }
        }

        return new BodyProjection(projectedBody, responseBody, false, isResponseWebViewAvailable, responseWebViewUri);
    }

    private static ResponseKind DetermineResponseKind(string mediaType)
    {
        if (HttpContentTypeHelper.IsJsonMediaType(mediaType))
        {
            return ResponseKind.Json;
        }

        if (HttpContentTypeHelper.IsXmlMediaType(mediaType))
        {
            return ResponseKind.Xml;
        }

        if (HttpContentTypeHelper.IsHtmlMediaType(mediaType))
        {
            return ResponseKind.Html;
        }

        return IsBinaryMediaType(mediaType) ? ResponseKind.Binary : ResponseKind.Text;
    }

    public static string FormatElapsedMilliseconds(double milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        if (milliseconds < 1000)
        {
            return $"{Math.Round(milliseconds)} ms";
        }

        var seconds = milliseconds / 1000.0;
        return seconds < 60
            ? $"{seconds.ToString("0.00", CultureInfo.InvariantCulture)} s"
            : $"{((long)seconds) / 60} min {((long)seconds) % 60} s";
    }

    public static string FormatByteSize(long byteCount)
    {
        if (byteCount < 0)
        {
            byteCount = 0;
        }

        const double kilobyte = 1024.0;
        const double megabyte = kilobyte * 1024.0;
        const double gigabyte = megabyte * 1024.0;

        if (byteCount < kilobyte)
        {
            return $"{byteCount} B";
        }

        if (byteCount < megabyte)
        {
            return $"{(byteCount / kilobyte).ToString("0.##", CultureInfo.InvariantCulture)} KB";
        }

        if (byteCount < gigabyte)
        {
            return $"{(byteCount / megabyte).ToString("0.##", CultureInfo.InvariantCulture)} MB";
        }

        return $"{(byteCount / gigabyte).ToString("0.##", CultureInfo.InvariantCulture)} GB";
    }

    private static string GetResponseContentType(IReadOnlyList<(string Name, string Value)> headers) =>
        headers.FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

    private static bool IsBinaryMediaType(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        if (mediaType.StartsWith("text/", StringComparison.Ordinal)
            || HttpContentTypeHelper.IsJsonMediaType(mediaType)
            || HttpContentTypeHelper.IsXmlMediaType(mediaType)
            || HttpContentTypeHelper.IsHtmlMediaType(mediaType))
        {
            return false;
        }

        return mediaType.StartsWith("image/", StringComparison.Ordinal)
               || mediaType.StartsWith("audio/", StringComparison.Ordinal)
               || mediaType.StartsWith("video/", StringComparison.Ordinal)
               || mediaType is "application/octet-stream"
               || mediaType is "application/zip"
               || mediaType is "application/pdf"
               || mediaType is "application/msword"
               || mediaType is "application/vnd.ms-excel"
               || mediaType is "application/vnd.ms-powerpoint"
               || mediaType.StartsWith("application/vnd.openxmlformats-officedocument.", StringComparison.Ordinal);
    }

    private static bool TryFormatJson(string input, out string formatted)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            formatted = JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return true;
        }
        catch (JsonException)
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static bool TryFormatXml(string input, out string formatted)
    {
        try
        {
            var document = XDocument.Parse(input, LoadOptions.PreserveWhitespace);
            using var writer = new StringWriter();
            using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                NewLineOnAttributes = false
            });
            document.Save(xmlWriter);
            xmlWriter.Flush();
            formatted = writer.ToString();
            return true;
        }
        catch (XmlException)
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static bool TryBuildResponseWebViewUri(string htmlResponseBody, out string uri)
    {
        if (string.IsNullOrWhiteSpace(htmlResponseBody))
        {
            uri = string.Empty;
            return false;
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlResponseBody));
        uri = $"data:text/html;charset=utf-8;base64,{encoded}";
        return true;
    }
}

public sealed record HttpResponseProjectionResult(
    string ResponseStatus,
    int ResponseStatusCode,
    string ResponseTimeDisplay,
    string ResponseSizeDisplay,
    byte[] LastResponseBodyBytes,
    string RawResponseBody,
    IReadOnlyList<string> ResponseHeaders,
    bool HasResponseHeaders,
    string ResponseContentType,
    string ResponseBodyTabLabel,
    bool IsBinaryResponse,
    string ResponseBody,
    bool IsResponseWebViewAvailable,
    string ResponseWebViewUri,
    string ResponseRawText,
    bool HasTextResponse);

internal enum ResponseKind
{
    Text,
    Json,
    Xml,
    Html,
    Binary
}

internal sealed record BodyProjection(
    string ResponseBody,
    string RawResponseBody,
    bool IsBinaryResponse,
    bool IsResponseWebViewAvailable,
    string ResponseWebViewUri);
