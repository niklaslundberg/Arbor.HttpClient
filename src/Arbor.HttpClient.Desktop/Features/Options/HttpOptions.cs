using Arbor.HttpClient.Desktop.Demo;
namespace Arbor.HttpClient.Desktop.Features.Options;

public sealed class HttpOptions
{
    public string HttpVersion { get; init; } = "1.1";

    public string TlsVersion { get; init; } = "SystemDefault";

    public bool EnableHttpDiagnostics { get; init; }

    public string DefaultContentType { get; init; } = "application/json";

    public bool FollowRedirects { get; init; } = true;

    public string DefaultRequestUrl { get; init; } = "http://localhost:5000/echo";

    public int DemoServerPort { get; init; } = DemoServer.DefaultPort;
}
