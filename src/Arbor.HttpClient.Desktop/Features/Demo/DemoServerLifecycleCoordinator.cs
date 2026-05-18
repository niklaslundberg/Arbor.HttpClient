using Arbor.HttpClient.Desktop.Demo;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Demo;

/// <summary>
/// Coordinates starting and stopping of the embedded demo server and maps outcomes for UI projection.
/// </summary>
public sealed class DemoServerLifecycleCoordinator
{
    private readonly DemoServer? _demoServer;
    private readonly ILogger _logger;

    public DemoServerLifecycleCoordinator(DemoServer? demoServer, ILogger logger)
    {
        _demoServer = demoServer;
        _logger = logger;
    }

    public async Task<DemoServerStartOutcome> StartAsync(
        int httpPort,
        int httpsPort,
        bool isHttpEnabled,
        bool isHttpsEnabled)
    {
        if (_demoServer is null || _demoServer.IsRunning)
        {
            return DemoServerStartOutcome.NoChange();
        }

        if (!isHttpEnabled && !isHttpsEnabled)
        {
            return DemoServerStartOutcome.Failed("Enable at least one of HTTP or HTTPS before starting the demo server.");
        }

        try
        {
            await _demoServer.StartAsync(httpPort, httpsPort, isHttpEnabled, isHttpsEnabled);

            _logger.Information(
                "Demo server started — HTTP: {HttpEnabled} port {HttpPort}, HTTPS: {HttpsEnabled} port {HttpsPort}",
                isHttpEnabled,
                httpPort,
                isHttpsEnabled,
                httpsPort);

            return DemoServerStartOutcome.Success();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            _logger.Error(exception, "Failed to start demo server on port {Port}/{HttpsPort}", httpPort, httpsPort);
            return DemoServerStartOutcome.Failed($"Failed to start demo server: {exception.Message}");
        }
    }

    public async Task<DemoServerStopOutcome> StopAsync()
    {
        if (_demoServer is null || !_demoServer.IsRunning)
        {
            return DemoServerStopOutcome.NoChange();
        }

        await _demoServer.StopAsync();
        _logger.Information("Demo server stopped");

        return DemoServerStopOutcome.Success();
    }
}

public sealed record DemoServerStartOutcome
{
    private DemoServerStartOutcome(bool changed, bool isRunning, string? errorMessage)
    {
        Changed = changed;
        IsRunning = isRunning;
        ErrorMessage = errorMessage;
    }

    public bool Changed { get; }

    public bool IsRunning { get; }

    public string? ErrorMessage { get; }

    public static DemoServerStartOutcome Success() =>
        new(true, true, null);

    public static DemoServerStartOutcome Failed(string errorMessage) =>
        new(false, false, errorMessage);

    public static DemoServerStartOutcome NoChange() =>
        new(false, false, null);
}

public sealed record DemoServerStopOutcome
{
    private DemoServerStopOutcome(bool changed, bool isRunning)
    {
        Changed = changed;
        IsRunning = isRunning;
    }

    public bool Changed { get; }

    public bool IsRunning { get; }

    public static DemoServerStopOutcome Success() =>
        new(true, false);

    public static DemoServerStopOutcome NoChange() =>
        new(false, false);
}
