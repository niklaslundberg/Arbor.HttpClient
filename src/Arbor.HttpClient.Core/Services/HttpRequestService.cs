using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

public sealed class HttpRequestService(global::System.Net.Http.HttpClient httpClient, IRequestHistoryRepository requestHistoryRepository, TimeProvider? timeProvider = null)
{
    private readonly global::System.Net.Http.HttpClient _httpClient = httpClient;
    private readonly IRequestHistoryRepository _requestHistoryRepository = requestHistoryRepository;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private Func<global::System.Net.Http.HttpClient>? _httpClientFactory;

    public void SetHttpClientFactory(Func<global::System.Net.Http.HttpClient> httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<HttpResponseDetails> SendAsync(HttpRequestDraft requestDraft, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestDraft.Method))
        {
            throw new ArgumentException("HTTP method is required", nameof(requestDraft));
        }

        if (!Uri.TryCreate(requestDraft.Url, UriKind.Absolute, out var uri) || (uri.Scheme is not ("http" or "https")))
        {
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS URL", nameof(requestDraft));
        }

        using var requestMessage = new global::System.Net.Http.HttpRequestMessage(new global::System.Net.Http.HttpMethod(requestDraft.Method), uri);
        if (requestDraft.HttpVersion is not null)
        {
            requestMessage.Version = requestDraft.HttpVersion;
            requestMessage.VersionPolicy = global::System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
        }

        var contentTypeHeader = requestDraft.Headers?
            .FirstOrDefault(h => h.IsEnabled && string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(requestDraft.Body))
        {
            requestMessage.Content = contentTypeHeader is not null
                ? new global::System.Net.Http.StringContent(requestDraft.Body, System.Text.Encoding.UTF8, contentTypeHeader.Value)
                : new global::System.Net.Http.StringContent(requestDraft.Body);
        }

        if (requestDraft.Headers is not null)
        {
            foreach (var header in requestDraft.Headers)
            {
                if (!header.IsEnabled || string.IsNullOrWhiteSpace(header.Name))
                {
                    continue;
                }

                if (string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                requestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        var activeClient = _httpClientFactory?.Invoke() ?? _httpClient;

        using var response = await activeClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(h => h.Value.Select(v => (h.Key, v)))
            .ToList();

        await _requestHistoryRepository.SaveAsync(
            new SavedRequest(
                string.IsNullOrWhiteSpace(requestDraft.Name) ? requestDraft.Url : requestDraft.Name,
                requestDraft.Method,
                requestDraft.Url,
                requestDraft.Body,
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        return new HttpResponseDetails((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBody, responseHeaders);
    }
}
