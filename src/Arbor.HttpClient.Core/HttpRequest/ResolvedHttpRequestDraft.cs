namespace Arbor.HttpClient.Core.HttpRequest;

/// <summary>
/// Represents a send-ready HTTP request after editor values, variables, headers, and options have been resolved.
/// </summary>
/// <param name="Name">The display name for the request.</param>
/// <param name="Method">The HTTP method to send.</param>
/// <param name="Url">The absolute request URL.</param>
/// <param name="Body">The request body, if any.</param>
/// <param name="Headers">The resolved headers to include with the request.</param>
/// <param name="HttpVersion">The requested HTTP version, if overridden.</param>
/// <param name="FollowRedirects">The per-request redirect override, if configured.</param>
/// <param name="IgnoreCertificateValidation">The per-request certificate-validation override, if configured.</param>
/// <param name="TimeoutSeconds">The per-request timeout in seconds, if configured.</param>
/// <param name="TlsVersionOverride">The per-request TLS version override, if configured.</param>
public sealed record ResolvedHttpRequestDraft(
    string Name,
    string Method,
    string Url,
    string? Body,
    IReadOnlyList<RequestHeader>? Headers = null,
    Version? HttpVersion = null,
    bool? FollowRedirects = null,
    bool? IgnoreCertificateValidation = null,
    int? TimeoutSeconds = null,
    string? TlsVersionOverride = null);
