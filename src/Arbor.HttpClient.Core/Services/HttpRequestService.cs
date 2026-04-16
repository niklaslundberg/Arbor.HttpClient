using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

public sealed class HttpRequestService(global::System.Net.Http.HttpClient httpClient, IRequestHistoryRepository requestHistoryRepository, TimeProvider? timeProvider = null)
{
    private readonly global::System.Net.Http.HttpClient _httpClient = httpClient;
    private readonly IRequestHistoryRepository _requestHistoryRepository = requestHistoryRepository;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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

        if (!string.IsNullOrWhiteSpace(requestDraft.Body))
        {
            requestMessage.Content = new global::System.Net.Http.StringContent(requestDraft.Body);
        }

        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        await _requestHistoryRepository.SaveAsync(
            new SavedRequest(
                string.IsNullOrWhiteSpace(requestDraft.Name) ? requestDraft.Url : requestDraft.Name,
                requestDraft.Method,
                requestDraft.Url,
                requestDraft.Body,
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        return new HttpResponseDetails((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBody);
    }
}
