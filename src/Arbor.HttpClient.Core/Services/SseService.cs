using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

/// <summary>
/// Connects to a Server-Sent Events (SSE) endpoint and streams <see cref="SseEvent"/>
/// records to the caller via a callback.
/// </summary>
public sealed class SseService(global::System.Net.Http.HttpClient httpClient)
{
    /// <summary>
    /// Opens a long-lived GET request to <paramref name="url"/> with an
    /// <c>Accept: text/event-stream</c> header and streams parsed SSE events to
    /// <paramref name="onEvent"/> until <paramref name="cancellationToken"/> is cancelled
    /// or the server closes the stream.
    /// </summary>
    public async Task ConnectAsync(
        string url,
        Action<SseEvent> onEvent,
        IReadOnlyList<RequestHeader>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme is not ("http" or "https")))
        {
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS URL", nameof(url));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        if (additionalHeaders is not null)
        {
            foreach (var header in additionalHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
            {
                if (!string.Equals(header.Name, "Accept", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }
            }
        }

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        await ParseSseStreamAsync(reader, onEvent, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task ParseSseStreamAsync(
        StreamReader reader,
        Action<SseEvent> onEvent,
        CancellationToken cancellationToken)
    {
        string? id = null;
        string? eventType = null;
        var dataLines = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                break;
            }

            if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                id = line.Length > 3 ? line[3..].TrimStart() : string.Empty;
            }
            else if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventType = line.Length > 6 ? line[6..].TrimStart() : string.Empty;
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line.Length > 5 ? line[5..].TrimStart() : string.Empty);
            }
            else if (line.StartsWith(":", StringComparison.Ordinal))
            {
                // SSE comment line – ignore
            }
            else if (string.IsNullOrEmpty(line) && dataLines.Count > 0)
            {
                // Blank line dispatches the accumulated event
                onEvent(new SseEvent(id, eventType, string.Join("\n", dataLines), DateTimeOffset.UtcNow));
                id = null;
                eventType = null;
                dataLines.Clear();
            }
        }

        // Dispatch any trailing event that was not terminated by a blank line
        if (dataLines.Count > 0)
        {
            onEvent(new SseEvent(id, eventType, string.Join("\n", dataLines), DateTimeOffset.UtcNow));
        }
    }
}
