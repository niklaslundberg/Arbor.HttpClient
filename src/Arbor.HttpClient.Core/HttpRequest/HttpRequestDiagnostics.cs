namespace Arbor.HttpClient.Core.HttpRequest;

public sealed record HttpRequestDiagnostics(
    string Method,
    string Url,
    string RequestedHttpVersion,
    string ResponseHttpVersion,
    string DnsLookup,
    string TlsNegotiation,
    double DnsLookupMilliseconds,
    double TlsNegotiationMilliseconds,
    double ResponseHeadersMilliseconds,
    double ResponseBodyMilliseconds,
    double TotalMilliseconds);
