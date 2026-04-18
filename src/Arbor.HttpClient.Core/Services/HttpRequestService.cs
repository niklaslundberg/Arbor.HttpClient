using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Arbor.HttpClient.Core.Services;

public sealed class HttpRequestService(global::System.Net.Http.HttpClient httpClient, IRequestHistoryRepository requestHistoryRepository, TimeProvider? timeProvider = null)
{
    private readonly global::System.Net.Http.HttpClient _httpClient = httpClient;
    private readonly IRequestHistoryRepository _requestHistoryRepository = requestHistoryRepository;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private Func<global::System.Net.Http.HttpClient>? _httpClientFactory;
    private Func<bool?, global::System.Net.Http.HttpClient>? _httpClientFactoryWithRedirectOverride;
    private Action<HttpRequestDiagnostics>? _diagnosticsObserver;
    private bool _httpDiagnosticsEnabled;

    public void SetHttpClientFactory(Func<global::System.Net.Http.HttpClient> httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public void SetHttpClientFactory(Func<bool?, global::System.Net.Http.HttpClient> httpClientFactoryWithRedirectOverride)
    {
        _httpClientFactoryWithRedirectOverride = httpClientFactoryWithRedirectOverride ?? throw new ArgumentNullException(nameof(httpClientFactoryWithRedirectOverride));
    }

    public void SetHttpDiagnosticsEnabled(bool enabled) => _httpDiagnosticsEnabled = enabled;

    public void SetHttpDiagnosticsObserver(Action<HttpRequestDiagnostics> diagnosticsObserver)
    {
        _diagnosticsObserver = diagnosticsObserver ?? throw new ArgumentNullException(nameof(diagnosticsObserver));
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

        var activeClient = _httpClientFactoryWithRedirectOverride?.Invoke(requestDraft.FollowRedirects)
            ?? _httpClientFactory?.Invoke()
            ?? _httpClient;

        var totalStopwatch = Stopwatch.StartNew();
        var requestedHttpVersion = requestMessage.Version.ToString(2);
        var dnsLookupStopwatch = Stopwatch.StartNew();
        var dnsLookupResult = "Skipped";
        if (_httpDiagnosticsEnabled)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
                dnsLookupResult = addresses.Length > 0
                    ? string.Join(", ", addresses.Select(address => address.ToString()))
                    : "No DNS addresses found";
            }
            catch (Exception exception)
            {
                dnsLookupResult = $"DNS lookup failed: {exception.Message}";
            }
        }
        dnsLookupStopwatch.Stop();

        var tlsStopwatch = Stopwatch.StartNew();
        var tlsResult = uri.Scheme == Uri.UriSchemeHttps ? "TLS negotiation unavailable" : "Not applicable (HTTP)";
        if (_httpDiagnosticsEnabled && uri.Scheme == Uri.UriSchemeHttps)
        {
            tlsResult = await ProbeTlsAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        tlsStopwatch.Stop();

        var headersStopwatch = Stopwatch.StartNew();
        using var response = await activeClient.SendAsync(requestMessage, global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        headersStopwatch.Stop();

        var bodyStopwatch = Stopwatch.StartNew();
        var responseBodyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        bodyStopwatch.Stop();
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

        var responseBody = encoding.GetString(responseBodyBytes);

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

        if (_httpDiagnosticsEnabled && _diagnosticsObserver is not null)
        {
            totalStopwatch.Stop();
            _diagnosticsObserver.Invoke(new HttpRequestDiagnostics(
                requestDraft.Method,
                requestDraft.Url,
                requestedHttpVersion,
                response.Version.ToString(2),
                dnsLookupResult,
                tlsResult,
                dnsLookupStopwatch.Elapsed.TotalMilliseconds,
                tlsStopwatch.Elapsed.TotalMilliseconds,
                headersStopwatch.Elapsed.TotalMilliseconds,
                bodyStopwatch.Elapsed.TotalMilliseconds,
                totalStopwatch.Elapsed.TotalMilliseconds));
        }
        else
        {
            totalStopwatch.Stop();
        }

        return new HttpResponseDetails(
            (int)response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            responseBody,
            responseHeaders,
            responseBodyBytes,
            totalStopwatch.Elapsed.TotalMilliseconds);
    }

    private static async Task<string> ProbeTlsAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var port = uri.Port > 0 ? uri.Port : 443;
            await tcpClient.ConnectAsync(uri.Host, port, cancellationToken).ConfigureAwait(false);
            using var networkStream = tcpClient.GetStream();
            using var sslStream = new SslStream(networkStream, false);
            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = uri.Host,
                EnabledSslProtocols = SslProtocols.None,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };
            await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
            return sslStream.SslProtocol.ToString();
        }
        catch (Exception exception)
        {
            return $"TLS probe failed: {exception.Message}";
        }
    }
}
