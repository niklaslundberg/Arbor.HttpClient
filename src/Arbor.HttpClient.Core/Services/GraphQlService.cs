using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

/// <summary>
/// Sends GraphQL queries (and introspection requests) over HTTP using <c>System.Text.Json</c>
/// for serialisation.  No external library dependency is required.
/// </summary>
public sealed class GraphQlService(global::System.Net.Http.HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string _introspectionQuery =
        """
        {
          __schema {
            types {
              name
              kind
              description
              fields {
                name
                description
                type { name kind ofType { name kind } }
              }
            }
          }
        }
        """;

    /// <summary>
    /// Executes a GraphQL query or mutation and returns the raw JSON response body together
    /// with the HTTP status code and elapsed time.
    /// </summary>
    public async Task<HttpResponseDetails> SendQueryAsync(
        GraphQlDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (!Uri.TryCreate(draft.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme is not ("http" or "https")))
        {
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS URL", nameof(draft));
        }

        var bodyObj = BuildRequestBody(draft.Query, draft.VariablesJson, draft.OperationName);
        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        if (draft.Headers is { } headers)
        {
            foreach (var header in headers.Where(h => h.IsEnabled
                && !string.IsNullOrWhiteSpace(h.Name)
                && !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try
        {
            encoding = !string.IsNullOrWhiteSpace(charset) ? Encoding.GetEncoding(charset) : Encoding.UTF8;
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
        }

        var responseBody = encoding.GetString(responseBytes);

        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(h => h.Value.Select(v => (h.Key, v)))
            .ToList();

        return new HttpResponseDetails(
            (int)response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            responseBody,
            responseHeaders,
            responseBytes,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Sends a GraphQL introspection query to the given URL and returns the formatted
    /// schema JSON string.  Throws on network errors or non-successful status codes.
    /// </summary>
    public async Task<string> IntrospectSchemaAsync(
        string url,
        IReadOnlyList<RequestHeader>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var draft = new GraphQlDraft(url, _introspectionQuery, null, null, headers);
        var result = await SendQueryAsync(draft, cancellationToken).ConfigureAwait(false);

        if (result.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException(
                $"Introspection failed with status {result.StatusCode}: {result.Body}");
        }

        // Pretty-print if valid JSON
        try
        {
            using var doc = JsonDocument.Parse(result.Body);
            return JsonSerializer.Serialize(doc.RootElement, _jsonOptions);
        }
        catch (JsonException)
        {
            return result.Body;
        }
    }

    private static object BuildRequestBody(string query, string? variablesJson, string? operationName)
    {
        JsonNode? variables = null;
        if (!string.IsNullOrWhiteSpace(variablesJson))
        {
            try
            {
                variables = JsonNode.Parse(variablesJson);
            }
            catch (JsonException)
            {
                // Leave variables null if the JSON is invalid; the server will report the error
            }
        }

        return new
        {
            query,
            variables,
            operationName = string.IsNullOrWhiteSpace(operationName) ? null : operationName
        };
    }
}
