using System.Text.Json;

namespace Arbor.HttpClient.Core.Scripting;

/// <summary>
/// HTTP response data made available to post-response scripts.
/// <see cref="BodyJson"/> is parsed from <see cref="Body"/> using
/// <see cref="System.Text.Json.JsonDocument"/> so scripts can navigate the
/// response with standard STJ APIs without an extra import.
/// </summary>
public sealed class ScriptResponse
{
    public ScriptResponse(
        int statusCode,
        string reasonPhrase,
        string body,
        IReadOnlyDictionary<string, string> headers)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Body = body;
        Headers = headers;
        BodyJson = TryParseJson(body);
    }

    /// <summary>HTTP status code (e.g. 200, 404).</summary>
    public int StatusCode { get; }

    /// <summary>HTTP reason phrase (e.g. "OK", "Not Found").</summary>
    public string ReasonPhrase { get; }

    /// <summary>Response body as a plain string.</summary>
    public string Body { get; }

    /// <summary>
    /// Response headers. Keys are header names; values are the first value for each header.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Response body parsed as a <see cref="JsonElement"/> when the body contains valid JSON,
    /// or <see langword="null"/> when the body is empty or not valid JSON.
    /// Scripts can use standard STJ APIs to navigate the element:
    /// <code>var id = ctx.Response!.BodyJson!.Value.GetProperty("id").GetInt32();</code>
    /// </summary>
    public JsonElement? BodyJson { get; }

    private static JsonElement? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
