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

    public string ResponseSaveDefaultFolder { get; init; } = string.Empty;

    public string ResponseSaveFileNamePattern { get; init; } = global::Arbor.HttpClient.Desktop.Features.Main.ResponseSaveFileNamePatternFormatter.DefaultPattern;

    public int DefaultRequestTimeoutSeconds { get; init; } = 100;

    public int DemoServerPort { get; init; } = DemoServer.DefaultPort;

    public int DemoServerHttpsPort { get; init; } = DemoServer.DefaultHttpsPort;

    public bool DemoServerHttpEnabled { get; init; } = true;

    public bool DemoServerHttpsEnabled { get; init; }
}
