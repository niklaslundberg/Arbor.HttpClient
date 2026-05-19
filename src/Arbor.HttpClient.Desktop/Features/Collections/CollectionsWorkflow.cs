using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Owns collection-oriented workflows extracted from MainWindow, including implicit request persistence
/// and collection-request construction for HTTP and GraphQL flows.
/// </summary>
public sealed class CollectionsWorkflow
{
    public const string ImplicitCollectionName = "Implicit Requests";
    public const string ImplicitCollectionSourcePath = "arbor://implicit-requests";

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key",
        "X-Auth-Token"
    };

    private readonly ICollectionRepository _collectionRepository;
    private readonly ILogger _logger;

    public CollectionsWorkflow(ICollectionRepository collectionRepository, ILogger logger)
    {
        _collectionRepository = collectionRepository;
        _logger = logger;
    }

    public CollectionRequest BuildCollectionRequestFromResolvedHttpDraft(
        ResolvedHttpRequestDraft resolvedRequest,
        string requestNotes,
        string contentType)
    {
        var collectionName = string.IsNullOrWhiteSpace(resolvedRequest.Name)
            ? $"{resolvedRequest.Method} {resolvedRequest.Url}"
            : resolvedRequest.Name;
        if (collectionName.Length > 120)
        {
            collectionName = collectionName[..120];
        }

        return new CollectionRequest(
            collectionName,
            resolvedRequest.Method,
            resolvedRequest.Url,
            Description: null,
            Notes: requestNotes,
            Body: resolvedRequest.Body,
            ContentType: contentType,
            Headers: resolvedRequest.Headers is { } headers
                ? headers.Where(header => !IsSensitiveHeaderName(header.Name)).ToList()
                : null);
    }

    public CollectionRequest BuildCollectionRequestFromGraphQlState(
        string url,
        string requestName,
        string requestNotes,
        string graphQlBodyJson,
        IReadOnlyList<RequestHeader>? resolvedHeaders)
    {
        var collectionName = string.IsNullOrWhiteSpace(requestName)
            ? $"POST {url}"
            : requestName;
        if (collectionName.Length > 120)
        {
            collectionName = collectionName[..120];
        }

        return new CollectionRequest(
            collectionName,
            "POST",
            url,
            Description: null,
            Notes: requestNotes,
            Body: graphQlBodyJson,
            ContentType: "application/json",
            Headers: resolvedHeaders is { } headers
                ? headers.Where(header => !IsSensitiveHeaderName(header.Name)).ToList()
                : null);
    }

    public async Task SaveRequestToImplicitCollectionBestEffortAsync(
        CollectionRequest collectionRequest,
        Func<CancellationToken, Task> reloadCollectionsAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveRequestToImplicitCollectionAsync(collectionRequest, reloadCollectionsAsync, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Warning(
                exception,
                "Failed to persist implicit collection request {RequestName}; request send result remains successful",
                collectionRequest.Name);
        }
    }

    private async Task SaveRequestToImplicitCollectionAsync(
        CollectionRequest collectionRequest,
        Func<CancellationToken, Task> reloadCollectionsAsync,
        CancellationToken cancellationToken)
    {
        var collections = await _collectionRepository.GetAllAsync(cancellationToken);
        var implicitCollection = collections.FirstOrDefault(collection =>
            string.Equals(collection.SourcePath, ImplicitCollectionSourcePath, StringComparison.Ordinal))
            ?? collections.FirstOrDefault(collection =>
                string.Equals(collection.Name, ImplicitCollectionName, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(collection.SourcePath));

        if (implicitCollection is null)
        {
            await _collectionRepository.SaveAsync(
                ImplicitCollectionName,
                sourcePath: ImplicitCollectionSourcePath,
                baseUrl: null,
                requests: [collectionRequest],
                cancellationToken: cancellationToken);

            await reloadCollectionsAsync(cancellationToken);
            return;
        }

        var updatedRequests = implicitCollection.Requests.ToList();
        var existingRequestIndex = updatedRequests.FindIndex(request =>
            string.Equals(request.Name, collectionRequest.Name, StringComparison.Ordinal)
            && string.Equals(request.Method, collectionRequest.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Path, collectionRequest.Path, StringComparison.Ordinal));

        if (existingRequestIndex >= 0)
        {
            updatedRequests[existingRequestIndex] = collectionRequest;
        }
        else
        {
            updatedRequests.Add(collectionRequest);
        }

        await _collectionRepository.UpdateAsync(
            implicitCollection.Id,
            implicitCollection.Name,
            ImplicitCollectionSourcePath,
            implicitCollection.BaseUrl,
            updatedRequests,
            implicitCollection.Headers,
            cancellationToken);

        await reloadCollectionsAsync(cancellationToken);
    }

    private static bool IsSensitiveHeaderName(string headerName)
    {
        if (SensitiveHeaderNames.Contains(headerName))
        {
            return true;
        }

        return headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
               || headerName.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }
}
