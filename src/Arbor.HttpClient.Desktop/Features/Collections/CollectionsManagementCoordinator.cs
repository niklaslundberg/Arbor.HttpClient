using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Coordinates collection-management commands (create, rename, and add request) and persistence side effects.
/// </summary>
public sealed class CollectionsManagementCoordinator
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly Func<CancellationToken, Task> _loadCollectionsAsync;
    private readonly Func<IReadOnlyCollection<Collection>> _getCollections;
    private readonly Func<Collection?> _getSelectedCollection;
    private readonly Func<ResolvedHttpRequestDraft> _buildResolvedHttpRequestDraft;
    private readonly ILogger _logger;

    public CollectionsManagementCoordinator(
        ICollectionRepository collectionRepository,
        Func<CancellationToken, Task> loadCollectionsAsync,
        Func<IReadOnlyCollection<Collection>> getCollections,
        Func<Collection?> getSelectedCollection,
        Func<ResolvedHttpRequestDraft> buildResolvedHttpRequestDraft,
        ILogger logger)
    {
        _collectionRepository = collectionRepository;
        _loadCollectionsAsync = loadCollectionsAsync;
        _getCollections = getCollections;
        _getSelectedCollection = getSelectedCollection;
        _buildResolvedHttpRequestDraft = buildResolvedHttpRequestDraft;
        _logger = logger;
    }

    public async Task<CreateCollectionOutcome> CreateCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var trimmedName = collectionName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return CreateCollectionOutcome.NoChange();
        }

        if (_getCollections().Any(collection => string.Equals(collection.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return CreateCollectionOutcome.Failed($"A collection named \"{trimmedName}\" already exists. Choose a different name.");
        }

        var createdCollectionId = await _collectionRepository.SaveAsync(trimmedName, null, null, [], cancellationToken: cancellationToken);
        await _loadCollectionsAsync(cancellationToken);

        _logger.Information("Created new collection {CollectionName}", trimmedName);
        return CreateCollectionOutcome.Success(createdCollectionId);
    }

    public async Task<RenameCollectionOutcome> RenameSelectedCollectionAsync(string newCollectionName, CancellationToken cancellationToken)
    {
        if (_getSelectedCollection() is not { } collection)
        {
            return RenameCollectionOutcome.NoChange();
        }

        var trimmedName = newCollectionName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return RenameCollectionOutcome.NoChange();
        }

        if (_getCollections().Any(candidateCollection =>
                candidateCollection.Id != collection.Id
                && string.Equals(candidateCollection.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            return RenameCollectionOutcome.Failed($"A collection named \"{trimmedName}\" already exists. Choose a different name.");
        }

        await _collectionRepository.UpdateAsync(
            collection.Id,
            trimmedName,
            collection.SourcePath,
            collection.BaseUrl,
            collection.Requests,
            collection.Headers,
            cancellationToken);

        await _loadCollectionsAsync(cancellationToken);

        _logger.Information("Renamed collection {OldName} to {NewName}", collection.Name, trimmedName);
        return RenameCollectionOutcome.Success(collection.Id);
    }

    public async Task<AddRequestToCollectionOutcome> AddCurrentRequestToSelectedCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_getSelectedCollection() is not { } collection)
        {
            return AddRequestToCollectionOutcome.NoChange();
        }

        var requestDraft = _buildResolvedHttpRequestDraft();
        var requestPath = BuildRequestPath(collection.BaseUrl, requestDraft.Url);
        var requestName = string.IsNullOrWhiteSpace(requestDraft.Name)
            ? requestDraft.Method + " " + requestPath
            : requestDraft.Name;

        var newRequest = new CollectionRequest(requestName, requestDraft.Method, requestPath, null);

        var updatedRequests = collection.Requests.Append(newRequest).ToList();
        await _collectionRepository.UpdateAsync(
            collection.Id,
            collection.Name,
            collection.SourcePath,
            collection.BaseUrl,
            updatedRequests,
            collection.Headers,
            cancellationToken);

        await _loadCollectionsAsync(cancellationToken);

        _logger.Information("Added request {RequestName} to collection {CollectionName}", newRequest.Name, collection.Name);
        return AddRequestToCollectionOutcome.Success(collection.Id);
    }

    private static string BuildRequestPath(string? collectionBaseUrl, string resolvedUrl)
    {
        var baseUrl = collectionBaseUrl?.TrimEnd('/');

        string path;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            path = resolvedUrl;
        }
        else if (resolvedUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
        {
            path = resolvedUrl[baseUrl.Length..];
        }
        else if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.PathAndQuery + absoluteUri.Fragment;
        }
        else
        {
            path = resolvedUrl;
        }

        return string.IsNullOrWhiteSpace(path) ? "/" : path;
    }
}

public sealed record CreateCollectionOutcome
{
    private CreateCollectionOutcome(bool changed, int? selectedCollectionId, string? errorMessage)
    {
        Changed = changed;
        SelectedCollectionId = selectedCollectionId;
        ErrorMessage = errorMessage;
    }

    public bool Changed { get; }

    public int? SelectedCollectionId { get; }

    public string? ErrorMessage { get; }

    public static CreateCollectionOutcome Success(int selectedCollectionId) =>
        new(true, selectedCollectionId, null);

    public static CreateCollectionOutcome Failed(string errorMessage) =>
        new(false, null, errorMessage);

    public static CreateCollectionOutcome NoChange() =>
        new(false, null, null);
}

public sealed record RenameCollectionOutcome
{
    private RenameCollectionOutcome(bool changed, int? selectedCollectionId, string? errorMessage)
    {
        Changed = changed;
        SelectedCollectionId = selectedCollectionId;
        ErrorMessage = errorMessage;
    }

    public bool Changed { get; }

    public int? SelectedCollectionId { get; }

    public string? ErrorMessage { get; }

    public static RenameCollectionOutcome Success(int selectedCollectionId) =>
        new(true, selectedCollectionId, null);

    public static RenameCollectionOutcome Failed(string errorMessage) =>
        new(false, null, errorMessage);

    public static RenameCollectionOutcome NoChange() =>
        new(false, null, null);
}

public sealed record AddRequestToCollectionOutcome
{
    private AddRequestToCollectionOutcome(bool changed, int? selectedCollectionId)
    {
        Changed = changed;
        SelectedCollectionId = selectedCollectionId;
    }

    public bool Changed { get; }

    public int? SelectedCollectionId { get; }

    public static AddRequestToCollectionOutcome Success(int selectedCollectionId) =>
        new(true, selectedCollectionId);

    public static AddRequestToCollectionOutcome NoChange() =>
        new(false, null);
}
