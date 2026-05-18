using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Collections;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Coordinates manual HTTP request execution, validation, and post-send persistence side effects.
/// </summary>
public sealed class ManualHttpRequestCoordinator
{
    private readonly HttpRequestWorkflow _httpRequestWorkflow;
    private readonly RequestEditorViewModel _requestEditor;
    private readonly CollectionsWorkflow _collectionsWorkflow;
    private readonly Func<CancellationToken, Task> _loadCollectionsAsync;
    private readonly Func<CancellationToken, Task> _loadHistoryAsync;
    private readonly ILogger _httpRequestsLogger;

    public ManualHttpRequestCoordinator(
        HttpRequestWorkflow httpRequestWorkflow,
        RequestEditorViewModel requestEditor,
        CollectionsWorkflow collectionsWorkflow,
        Func<CancellationToken, Task> loadCollectionsAsync,
        Func<CancellationToken, Task> loadHistoryAsync,
        ILogger httpRequestsLogger)
    {
        _httpRequestWorkflow = httpRequestWorkflow;
        _requestEditor = requestEditor;
        _collectionsWorkflow = collectionsWorkflow;
        _loadCollectionsAsync = loadCollectionsAsync;
        _loadHistoryAsync = loadHistoryAsync;
        _httpRequestsLogger = httpRequestsLogger;
    }

    public async Task<ManualHttpRequestOutcome> SendAsync(CancellationToken cancellationToken)
    {
        try
        {
            var executionResult = await _httpRequestWorkflow.SendAsync(cancellationToken);
            if (!executionResult.IsSuccessful)
            {
                return ManualHttpRequestOutcome.Failed(executionResult.ErrorMessage ?? string.Empty);
            }

            var mutatedDraft = executionResult.RequestDraft;
            var response = executionResult.Response;
            if (mutatedDraft is null || response is null)
            {
                return ManualHttpRequestOutcome.Failed(string.Empty);
            }

            var implicitCollectionRequest = _collectionsWorkflow.BuildCollectionRequestFromResolvedHttpDraft(
                mutatedDraft,
                _requestEditor.RequestNotes,
                _requestEditor.ContentType);
            await _collectionsWorkflow.SaveRequestToImplicitCollectionBestEffortAsync(
                implicitCollectionRequest,
                _loadCollectionsAsync,
                cancellationToken);
            await _loadHistoryAsync(cancellationToken);

            return ManualHttpRequestOutcome.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _httpRequestsLogger.Information("Manual request cancelled by user");
            return ManualHttpRequestOutcome.Failed("Request cancelled.");
        }
        catch (OperationCanceledException)
        {
            _httpRequestsLogger.Warning("Manual request timed out");
            return ManualHttpRequestOutcome.Failed("Request timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _httpRequestsLogger.Error(exception, "Manual request failed");
            return ManualHttpRequestOutcome.Failed(exception.Message, clearResponseMetadata: true);
        }
    }
}

public sealed record ManualHttpRequestOutcome
{
    private ManualHttpRequestOutcome(bool isSuccessful, HttpResponseDetails? response, string errorMessage, bool clearResponseMetadata)
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

    public static ManualHttpRequestOutcome Success(HttpResponseDetails response) =>
        new(true, response, string.Empty, clearResponseMetadata: false);

    public static ManualHttpRequestOutcome Failed(string errorMessage, bool clearResponseMetadata = false) =>
        new(false, null, errorMessage, clearResponseMetadata);
}
