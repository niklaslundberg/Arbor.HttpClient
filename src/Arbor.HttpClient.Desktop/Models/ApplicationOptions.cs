namespace Arbor.HttpClient.Desktop.Models;

public sealed class ApplicationOptions
{
    public HttpOptions Http { get; init; } = new();

    public AppearanceOptions Appearance { get; init; } = new();
}

public sealed class HttpOptions
{
    public string HttpVersion { get; init; } = "1.1";

    public string TlsVersion { get; init; } = "SystemDefault";

    public string DefaultContentType { get; init; } = "application/json";

    public bool FollowRedirects { get; init; } = true;

    public string DefaultRequestUrl { get; init; } = "https://postman-echo.com/get?hello=world";
}

public sealed class AppearanceOptions
{
    public string Theme { get; init; } = "System";

    public double FontSize { get; init; } = 13d;

    public string FontFamily { get; init; } = "Cascadia Code,Consolas,Menlo,monospace";
}
