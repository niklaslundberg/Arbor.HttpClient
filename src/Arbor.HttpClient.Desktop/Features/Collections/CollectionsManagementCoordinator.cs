using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.OpenApiImport;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Coordinates collection-management commands (create, rename, add request, import, and delete)
/// and persistence side effects.
/// </summary>
public sealed class CollectionsManagementCoordinator
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly Func<CancellationToken, Task> _loadCollectionsAsync;
    private readonly Func<IReadOnlyCollection<Collection>> _getCollections;
    private readonly Func<Collection?> _getSelectedCollection;
    private readonly Func<ResolvedHttpRequestDraft> _buildResolvedHttpRequestDraft;
    private readonly OpenApiImportService _openApiImportService;
    private readonly ILogger _logger;

    public CollectionsManagementCoordinator(
        ICollectionRepository collectionRepository,
        Func<CancellationToken, Task> loadCollectionsAsync,
        Func<IReadOnlyCollection<Collection>> getCollections,
        Func<Collection?> getSelectedCollection,
        Func<ResolvedHttpRequestDraft> buildResolvedHttpRequestDraft,
        OpenApiImportService openApiImportService,
        ILogger logger)
    {
        _collectionRepository = collectionRepository;
        _loadCollectionsAsync = loadCollectionsAsync;
        _getCollections = getCollections;
        _getSelectedCollection = getSelectedCollection;
        _buildResolvedHttpRequestDraft = buildResolvedHttpRequestDraft;
        _openApiImportService = openApiImportService;
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

    /// <summary>
    /// Imports an OpenAPI specification, persists it as a new collection, and reloads the
    /// collection list. The stream is opened inside the failure boundary so picker/IO errors
    /// also surface as a failed outcome.
    /// </summary>
    public async Task<ImportCollectionOutcome> ImportCollectionAsync(
        Func<Task<Stream>> openSpecificationStreamAsync,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var specificationStream = await openSpecificationStreamAsync();
            var importedCollection = _openApiImportService.Import(specificationStream, sourcePath);
            var importedCollectionId = await _collectionRepository.SaveAsync(
                importedCollection.Name,
                importedCollection.SourcePath,
                importedCollection.BaseUrl,
                importedCollection.Requests,
                importedCollection.Headers,
                cancellationToken);

            await _loadCollectionsAsync(cancellationToken);
            _logger.Information("Imported collection {CollectionName} from {Path}", importedCollection.Name, sourcePath);
            return ImportCollectionOutcome.Success(importedCollectionId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ImportCollectionOutcome.Failed($"Import failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Deletes the collection and reloads the collection list. The outcome reports whether
    /// the deleted collection was the selected one so the host can clear its selection.
    /// </summary>
    public async Task<DeleteCollectionOutcome> DeleteCollectionAsync(
        Collection? collection,
        CancellationToken cancellationToken = default)
    {
        if (collection is null)
        {
            return DeleteCollectionOutcome.NoChange();
        }

        await _collectionRepository.DeleteAsync(collection.Id, cancellationToken);
        _logger.Information("Deleted collection {CollectionName}", collection.Name);

        var wasSelected = _getSelectedCollection()?.Id == collection.Id;
        await _loadCollectionsAsync(cancellationToken);
        return DeleteCollectionOutcome.Deleted(wasSelected);
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

public sealed record ImportCollectionOutcome
{
    private ImportCollectionOutcome(bool changed, int? selectedCollectionId, string? errorMessage)
    {
        Changed = changed;
        SelectedCollectionId = selectedCollectionId;
        ErrorMessage = errorMessage;
    }

    public bool Changed { get; }

    public int? SelectedCollectionId { get; }

    public string? ErrorMessage { get; }

    public static ImportCollectionOutcome Success(int selectedCollectionId) =>
        new(true, selectedCollectionId, null);

    public static ImportCollectionOutcome Failed(string errorMessage) =>
        new(false, null, errorMessage);
}

public sealed record DeleteCollectionOutcome
{
    private DeleteCollectionOutcome(bool changed, bool wasSelected)
    {
        Changed = changed;
        WasSelected = wasSelected;
    }

    public bool Changed { get; }

    /// <summary>True when the deleted collection was the selected one.</summary>
    public bool WasSelected { get; }

    public static DeleteCollectionOutcome Deleted(bool wasSelected) =>
        new(true, wasSelected);

    public static DeleteCollectionOutcome NoChange() =>
        new(false, false);
}
