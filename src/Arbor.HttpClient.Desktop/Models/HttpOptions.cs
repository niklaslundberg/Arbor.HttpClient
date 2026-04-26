namespace Arbor.HttpClient.Desktop.Models;

public sealed class HttpOptions
{
    public string HttpVersion { get; init; } = "1.1";

    public string TlsVersion { get; init; } = "SystemDefault";

    public bool EnableHttpDiagnostics { get; init; }

    public string DefaultContentType { get; init; } = "application/json";

    public bool FollowRedirects { get; init; } = true;

    public string DefaultRequestUrl { get; init; } = "http://localhost:5000/echo";

    public int DemoServerPort { get; init; } = Services.DemoServer.DefaultPort;
}
