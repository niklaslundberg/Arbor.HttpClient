using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Arbor.HttpClient.Core.HttpRequest;

public sealed class HttpRequestService(System.Net.Http.HttpClient httpClient, IRequestHistoryRepository requestHistoryRepository, TimeProvider? timeProvider = null)
{
    private readonly System.Net.Http.HttpClient _httpClient = EnsureHttpClientTimeoutDisabled(httpClient);
    private readonly IRequestHistoryRepository _requestHistoryRepository = requestHistoryRepository;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    // Single volatile field — all three SetHttpClientFactory overloads normalise to this
    // two-parameter signature so that SendAsync reads exactly one reference (preventing
    // inconsistency between independently mutable fields during concurrent access).
    private volatile Func<bool?, bool?, string?, System.Net.Http.HttpClient>? _httpClientFactory;
    private Action<HttpRequestDiagnostics>? _diagnosticsObserver;
    private bool _httpDiagnosticsEnabled;
    private TimeSpan? _defaultRequestTimeout;

    public void SetHttpClientFactory(Func<System.Net.Http.HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = (_, _, _) => EnsureHttpClientTimeoutDisabled(httpClientFactory());
    }

    public void SetHttpClientFactory(Func<bool?, System.Net.Http.HttpClient> httpClientFactoryWithRedirectOverride)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactoryWithRedirectOverride);
        _httpClientFactory = (followRedirects, _, _) => EnsureHttpClientTimeoutDisabled(httpClientFactoryWithRedirectOverride(followRedirects));
    }

    /// <summary>
    /// Sets a factory that selects an <see cref="System.Net.Http.HttpClient"/> based on the per-request
    /// <see cref="ResolvedHttpRequestDraft.FollowRedirects"/> and <see cref="ResolvedHttpRequestDraft.IgnoreCertificateValidation"/> overrides.
    /// </summary>
    public void SetHttpClientFactory(Func<bool?, bool?, System.Net.Http.HttpClient> httpClientFactoryWithCertOverride)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactoryWithCertOverride);
        _httpClientFactory = (followRedirects, ignoreCertValidation, _) =>
            EnsureHttpClientTimeoutDisabled(httpClientFactoryWithCertOverride(followRedirects, ignoreCertValidation));
    }

    /// <summary>
    /// Sets a factory that selects an <see cref="System.Net.Http.HttpClient"/> based on the per-request
    /// <see cref="ResolvedHttpRequestDraft.FollowRedirects"/>, <see cref="ResolvedHttpRequestDraft.IgnoreCertificateValidation"/>,
    /// and <see cref="ResolvedHttpRequestDraft.TlsVersionOverride"/> overrides.
    /// </summary>
    public void SetHttpClientFactory(Func<bool?, bool?, string?, System.Net.Http.HttpClient> httpClientFactoryWithTlsOverride)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactoryWithTlsOverride);
        _httpClientFactory = (followRedirects, ignoreCertValidation, tlsVersionOverride) =>
            EnsureHttpClientTimeoutDisabled(httpClientFactoryWithTlsOverride(followRedirects, ignoreCertValidation, tlsVersionOverride));
    }

    public void SetHttpDiagnosticsEnabled(bool enabled) => _httpDiagnosticsEnabled = enabled;

    public void SetDefaultRequestTimeout(TimeSpan? timeout)
    {
        if (timeout is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Default request timeout must be greater than zero.");
        }

        _defaultRequestTimeout = timeout;
    }

    public void SetHttpDiagnosticsObserver(Action<HttpRequestDiagnostics> diagnosticsObserver)
    {
        _diagnosticsObserver = diagnosticsObserver ?? throw new ArgumentNullException(nameof(diagnosticsObserver));
    }

    public async Task<HttpResponseDetails> SendAsync(ResolvedHttpRequestDraft resolvedRequest, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resolvedRequest.Method))
        {
            throw new ArgumentException("HTTP method is required", nameof(resolvedRequest));
        }

        if (!Uri.TryCreate(resolvedRequest.Url, UriKind.Absolute, out var uri) || (uri.Scheme is not ("http" or "https")))
        {
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS URL", nameof(resolvedRequest));
        }

        using var requestMessage = new HttpRequestMessage(new HttpMethod(resolvedRequest.Method), uri);
        if (resolvedRequest.HttpVersion is { } httpVersion)
        {
            requestMessage.Version = httpVersion;
            requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }

        var contentTypeHeader = resolvedRequest.Headers?
            .FirstOrDefault(h => h.IsEnabled && string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resolvedRequest.Body))
        {
            requestMessage.Content = contentTypeHeader is { } ctHeader
            ? new StringContent(resolvedRequest.Body, Encoding.UTF8, ctHeader.Value)
            : new StringContent(resolvedRequest.Body);
        }

        if (resolvedRequest.Headers is { } requestHeaders)
        {
            foreach (var header in requestHeaders.Where(h => h.IsEnabled
                && !string.IsNullOrWhiteSpace(h.Name)
                && !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }

        // Read the factory once into a local to avoid observing a partially-updated state
        // if another thread calls SetHttpClientFactory concurrently with SendAsync.
        var factory = _httpClientFactory;
        var activeClient = factory?.Invoke(resolvedRequest.FollowRedirects, resolvedRequest.IgnoreCertificateValidation, resolvedRequest.TlsVersionOverride)
            ?? _httpClient;

        var totalStopwatch = Stopwatch.StartNew();
        var requestedHttpVersion = requestMessage.Version.ToString(2);

        var effectiveTimeout = resolvedRequest.TimeoutSeconds switch
        {
            > 0 => TimeSpan.FromSeconds(resolvedRequest.TimeoutSeconds.Value),
            0 => null,
            _ => _defaultRequestTimeout
        };
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
            new RequestHistoryEntry(
                string.IsNullOrWhiteSpace(resolvedRequest.Name) ? resolvedRequest.Url : resolvedRequest.Name,
                resolvedRequest.Method,
                resolvedRequest.Url,
                resolvedRequest.Body,
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        if (_httpDiagnosticsEnabled && _diagnosticsObserver is { } observer)
        {
            totalStopwatch.Stop();
            observer.Invoke(new HttpRequestDiagnostics(
                resolvedRequest.Method,
                resolvedRequest.Url,
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

    /// <summary>
    /// Ensures the provided <see cref="System.Net.Http.HttpClient"/> will not apply
    /// its own timeout so request timeouts are governed exclusively by cancellation tokens.
    /// This mutates the supplied client instance in place and returns the same instance.
    /// </summary>
    private static System.Net.Http.HttpClient EnsureHttpClientTimeoutDisabled(System.Net.Http.HttpClient client)
    {
        if (client.Timeout != Timeout.InfiniteTimeSpan)
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        }

        return client;
    }
}
