using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Collections;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.GraphQl;

/// <summary>
/// Coordinates GraphQL request execution and post-send persistence side effects.
/// </summary>
public sealed class ManualGraphQlRequestCoordinator
{
    private readonly GraphQlRequestWorkflow _graphQlRequestWorkflow;
    private readonly CollectionsWorkflow _collectionsWorkflow;
    private readonly Func<CancellationToken, Task> _loadCollectionsAsync;
    private readonly Func<CancellationToken, Task> _loadHistoryAsync;
    private readonly Func<GraphQlRequestCollectionState> _getCollectionState;
    private readonly ILogger _httpRequestsLogger;

    public ManualGraphQlRequestCoordinator(
        GraphQlRequestWorkflow graphQlRequestWorkflow,
        CollectionsWorkflow collectionsWorkflow,
        Func<CancellationToken, Task> loadCollectionsAsync,
        Func<CancellationToken, Task> loadHistoryAsync,
        Func<GraphQlRequestCollectionState> getCollectionState,
        ILogger httpRequestsLogger)
    {
        _graphQlRequestWorkflow = graphQlRequestWorkflow;
        _collectionsWorkflow = collectionsWorkflow;
        _loadCollectionsAsync = loadCollectionsAsync;
        _loadHistoryAsync = loadHistoryAsync;
        _getCollectionState = getCollectionState;
        _httpRequestsLogger = httpRequestsLogger;
    }

    public async Task<ManualGraphQlRequestOutcome> SendAsync(CancellationToken cancellationToken)
    {
        try
        {
            var executionResult = await _graphQlRequestWorkflow.SendAsync(cancellationToken);

            var collectionState = _getCollectionState();
            var implicitCollectionRequest = _collectionsWorkflow.BuildCollectionRequestFromGraphQlState(
                executionResult.Url,
                collectionState.RequestName,
                collectionState.RequestNotes,
                collectionState.RequestBodyJson,
                executionResult.Headers);

            await _collectionsWorkflow.SaveRequestToImplicitCollectionBestEffortAsync(
                implicitCollectionRequest,
                _loadCollectionsAsync,
                cancellationToken);

            await _loadHistoryAsync(cancellationToken);

            return ManualGraphQlRequestOutcome.Success(executionResult.Response);
        }
        catch (Exception exception)
        {
            _httpRequestsLogger.Error(exception, "GraphQL request failed");
            return ManualGraphQlRequestOutcome.Failed(exception.Message, clearResponseMetadata: true);
        }
    }
}

public sealed record GraphQlRequestCollectionState(string RequestName, string RequestNotes, string RequestBodyJson);

public sealed record ManualGraphQlRequestOutcome
{
    private ManualGraphQlRequestOutcome(bool isSuccessful, HttpResponseDetails? response, string errorMessage, bool clearResponseMetadata)
    {
        IsSuccessful = isSuccessful;
        Response = response;
        ErrorMessage = errorMessage;
        ClearResponseMetadata = clearResponseMetadata;
    }

    public bool IsSuccessful { get; }

    public HttpResponseDetails? Response { get; }

    public string ErrorMessage { get; }

    public bool ClearResponseMetadata { get; }

    public static ManualGraphQlRequestOutcome Success(HttpResponseDetails response) =>
        new(true, response, string.Empty, clearResponseMetadata: false);

    public static ManualGraphQlRequestOutcome Failed(string errorMessage, bool clearResponseMetadata = false) =>
        new(false, null, errorMessage, clearResponseMetadata);
}
