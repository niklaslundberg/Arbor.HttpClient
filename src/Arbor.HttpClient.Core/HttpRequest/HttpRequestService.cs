using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Arbor.HttpClient.Core.HttpRequest;

public sealed class HttpRequestService(global::System.Net.Http.HttpClient httpClient, IRequestHistoryRepository requestHistoryRepository, TimeProvider? timeProvider = null)
{
    private readonly global::System.Net.Http.HttpClient _httpClient = httpClient;
    private readonly IRequestHistoryRepository _requestHistoryRepository = requestHistoryRepository;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private Func<global::System.Net.Http.HttpClient>? _httpClientFactory;
    private Func<bool?, global::System.Net.Http.HttpClient>? _httpClientFactoryWithRedirectOverride;
    private Action<HttpRequestDiagnostics>? _diagnosticsObserver;
    private bool _httpDiagnosticsEnabled;
    private TimeSpan? _defaultRequestTimeout;

    public void SetHttpClientFactory(Func<global::System.Net.Http.HttpClient> httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public void SetHttpClientFactory(Func<bool?, global::System.Net.Http.HttpClient> httpClientFactoryWithRedirectOverride)
    {
        _httpClientFactoryWithRedirectOverride = httpClientFactoryWithRedirectOverride ?? throw new ArgumentNullException(nameof(httpClientFactoryWithRedirectOverride));
    }

    public void SetHttpDiagnosticsEnabled(bool enabled) => _httpDiagnosticsEnabled = enabled;

    public void SetDefaultRequestTimeout(TimeSpan? timeout) => _defaultRequestTimeout = timeout;

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

        using var requestMessage = new HttpRequestMessage(new HttpMethod(requestDraft.Method), uri);
        if (requestDraft.HttpVersion is { } httpVersion)
        {
            requestMessage.Version = httpVersion;
            requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }

        var contentTypeHeader = requestDraft.Headers?
            .FirstOrDefault(h => h.IsEnabled && string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(requestDraft.Body))
        {
            requestMessage.Content = contentTypeHeader is { } ctHeader
                ? new StringContent(requestDraft.Body, Encoding.UTF8, ctHeader.Value)
                : new StringContent(requestDraft.Body);
        }

        if (requestDraft.Headers is { } requestHeaders)
        {
            foreach (var header in requestHeaders.Where(h => h.IsEnabled
                && !string.IsNullOrWhiteSpace(h.Name)
                && !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        var activeClient = _httpClientFactoryWithRedirectOverride?.Invoke(requestDraft.FollowRedirects)
            ?? _httpClientFactory?.Invoke()
            ?? _httpClient;

        var totalStopwatch = Stopwatch.StartNew();
        var requestedHttpVersion = requestMessage.Version.ToString(2);

        var effectiveTimeout = requestDraft.TimeoutSeconds is > 0
            ? TimeSpan.FromSeconds(requestDraft.TimeoutSeconds.Value)
            : _defaultRequestTimeout;
        using var timeoutCts = effectiveTimeout is { }
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (timeoutCts is { } cts && effectiveTimeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        var effectiveCancellationToken = timeoutCts?.Token ?? cancellationToken;

        var dnsLookupStopwatch = Stopwatch.StartNew();
        var dnsLookupResult = "Skipped";
        if (_httpDiagnosticsEnabled)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, effectiveCancellationToken).ConfigureAwait(false);
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
            tlsResult = await ProbeTlsAsync(uri, effectiveCancellationToken).ConfigureAwait(false);
        }
        tlsStopwatch.Stop();

        var headersStopwatch = Stopwatch.StartNew();
        using var response = await activeClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
        headersStopwatch.Stop();

        var bodyStopwatch = Stopwatch.StartNew();
        var responseBodyBytes = await response.Content.ReadAsByteArrayAsync(effectiveCancellationToken).ConfigureAwait(false);
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

        if (_httpDiagnosticsEnabled && _diagnosticsObserver is { } observer)
        {
            totalStopwatch.Stop();
            observer.Invoke(new HttpRequestDiagnostics(
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
