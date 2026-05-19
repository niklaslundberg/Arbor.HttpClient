using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Demo;

/// <summary>
/// Seeds demo collections and environments used by the embedded localhost demo server.
/// </summary>
public sealed class DemoDataWorkflow
{
    private const string DemoCollectionName = "Localhost Demo";
    private const string DemoEnvironmentName = "Demo (localhost)";

    private readonly ICollectionRepository _collectionRepository;
    private readonly Func<CancellationToken, Task<IReadOnlyList<RequestEnvironment>>> _getAllEnvironmentsAsync;
    private readonly Func<string, IReadOnlyList<EnvironmentVariable>, CancellationToken, Task> _seedEnvironmentAsync;
    private readonly Func<int> _getDemoServerPort;
    private readonly ILogger _logger;

    public DemoDataWorkflow(
        ICollectionRepository collectionRepository,
        Func<CancellationToken, Task<IReadOnlyList<RequestEnvironment>>> getAllEnvironmentsAsync,
        Func<string, IReadOnlyList<EnvironmentVariable>, CancellationToken, Task> seedEnvironmentAsync,
        Func<int> getDemoServerPort,
        ILogger logger)
    {
        _collectionRepository = collectionRepository;
        _getAllEnvironmentsAsync = getAllEnvironmentsAsync;
        _seedEnvironmentAsync = seedEnvironmentAsync;
        _getDemoServerPort = getDemoServerPort;
        _logger = logger;
    }

    public async Task<DemoDataSeedResult> SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        int? seededCollectionId = null;

        var existingCollections = await _collectionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (!existingCollections.Any(collection => string.Equals(collection.Name, DemoCollectionName, StringComparison.Ordinal)))
        {
            var demoRequests = new List<CollectionRequest>
            {
                new("Documentation", "GET", "/docs.html",
                    "Read endpoint docs and sample usage for the local demo server."),
                new("Echo GET", "GET", "/echo",
                    "Simple HTTP GET — returns request info as JSON when no body is present."),
                new("Echo POST", "POST", "/echo",
                    "HTTP POST — your JSON body is echoed back in the response."),
                new("Echo PUT", "PUT", "/echo",
                    "HTTP PUT — your JSON body is echoed back in the response."),
                new("Echo DELETE", "DELETE", "/echo",
                    "HTTP DELETE — returns request info as JSON."),
                new("Server status", "GET", "/status",
                    "Returns a JSON summary of the demo server (version, port, endpoints)."),
                new("Server-Sent Events", "SSE", "/sse",
                    "Streams five numbered SSE events, one every 500 ms. Request type is set to SSE automatically."),
                new("WebSocket echo", "WS", "/ws",
                    "WebSocket echo server — every message you send is reflected back. Request type is set to WebSocket automatically.")
            };

            seededCollectionId = await _collectionRepository.SaveAsync(
                DemoCollectionName,
                null,
                "{{baseUrl}}",
                demoRequests,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.Information("Seeded demo collection '{Name}'", DemoCollectionName);
        }

        var seededEnvironment = false;
        var existingEnvironments = await _getAllEnvironmentsAsync(cancellationToken).ConfigureAwait(false);
        if (!existingEnvironments.Any(environment => string.Equals(environment.Name, DemoEnvironmentName, StringComparison.Ordinal)))
        {
            await _seedEnvironmentAsync(
                DemoEnvironmentName,
                [new EnvironmentVariable("baseUrl", $"http://localhost:{_getDemoServerPort()}", true)],
                cancellationToken).ConfigureAwait(false);

            _logger.Information("Seeded demo environment '{Name}'", DemoEnvironmentName);
            seededEnvironment = true;
        }

        return new DemoDataSeedResult(seededCollectionId, seededEnvironment);
    }
}

public sealed record DemoDataSeedResult(int? SeededCollectionId, bool SeededEnvironment);
