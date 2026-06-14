using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Owns the collection inherited-headers persistence pipeline extracted from MainWindow:
/// debounced auto-save scheduling, pending-change tracking with flush-on-close, suppression
/// scopes for programmatic header rebuilds, and the snapshot persistence flow.
/// Re-selecting the updated collection and marshalling persistence onto the UI thread stay
/// with the host view model and are injected as callbacks.
/// </summary>
public sealed class CollectionInheritedHeadersWorkflow : IDisposable
{
    public static readonly TimeSpan AutoSaveThrottleInterval = TimeSpan.FromSeconds(1);

    private readonly ICollectionRepository _collectionRepository;
    private readonly Func<int, Collection?> _findCollectionById;
    private readonly Func<CancellationToken, Task> _reloadCollectionsAsync;
    private readonly Action<int> _reselectUpdatedCollection;
    private readonly Func<Func<Task>, Task> _invokeOnUiThreadAsync;
    private readonly ILogger _logger;
    private readonly Subject<CollectionInheritedHeadersAutoSaveSnapshot> _autoSaveRequestedSubject = new();
    private readonly IObservable<CollectionInheritedHeadersAutoSaveSnapshot> _autoSaveRequested;
    private int _autoSaveSuppressionDepth;
    private Task? _autoSaveTask;
    private int _autoSaveVersion;
    private bool _hasPendingAutoSave;
    private CollectionInheritedHeadersAutoSaveSnapshot? _pendingAutoSaveSnapshot;

    public CollectionInheritedHeadersWorkflow(
        ICollectionRepository collectionRepository,
        Func<int, Collection?> findCollectionById,
        Func<CancellationToken, Task> reloadCollectionsAsync,
        Action<int> reselectUpdatedCollection,
        ILogger logger,
        IScheduler? autoSaveScheduler = null,
        Func<Func<Task>, Task>? invokeOnUiThreadAsync = null)
    {
        _collectionRepository = collectionRepository;
        _findCollectionById = findCollectionById;
        _reloadCollectionsAsync = reloadCollectionsAsync;
        _reselectUpdatedCollection = reselectUpdatedCollection;
        _logger = logger;
        _invokeOnUiThreadAsync = invokeOnUiThreadAsync ?? (work => work());
        _autoSaveRequested = _autoSaveRequestedSubject
            .Throttle(AutoSaveThrottleInterval, autoSaveScheduler ?? DefaultScheduler.Instance);
    }

    /// <summary>
    /// Debounced stream of auto-save requests. The subscriber is responsible for marshalling
    /// the <see cref="TriggerAutoSave"/> call onto the UI thread.
    /// </summary>
    public IObservable<CollectionInheritedHeadersAutoSaveSnapshot> AutoSaveRequested => _autoSaveRequested;

    /// <summary>True when header edits have been queued but not yet persisted.</summary>
    public bool HasPendingAutoSave => _hasPendingAutoSave;

    public bool IsAutoSaveSuppressed => Volatile.Read(ref _autoSaveSuppressionDepth) > 0;

    /// <summary>
    /// Suppresses auto-save queuing until the returned scope is disposed. Scopes may nest;
    /// auto-save resumes when the outermost scope is disposed.
    /// </summary>
    public IDisposable SuppressAutoSave()
    {
        Interlocked.Increment(ref _autoSaveSuppressionDepth);
        return Disposable.Create(() => Interlocked.Decrement(ref _autoSaveSuppressionDepth));
    }

    /// <summary>
    /// Converts header view-models to <see cref="RequestHeader"/> records, skipping rows
    /// with blank names and trimming names. Returns null when no rows remain.
    /// </summary>
    public static IReadOnlyList<RequestHeader>? BuildHeaders(IEnumerable<RequestHeaderViewModel> headerViewModels)
    {
        var headers = headerViewModels
            .Where(headerViewModel => !string.IsNullOrWhiteSpace(headerViewModel.Name))
            .Select(headerViewModel => new RequestHeader(
                headerViewModel.Name.Trim(),
                headerViewModel.Value,
                headerViewModel.IsEnabled))
            .ToList();

        return headers is { Count: > 0 } ? headers : null;
    }

    /// <summary>
    /// Merges collection-level headers with request-level headers; a request header
    /// with the same name (case-insensitive) overrides the collection header in place.
    /// Returns null when both inputs are empty.
    /// </summary>
    public static IReadOnlyList<RequestHeader>? MergeCollectionAndRequestHeaders(
        IReadOnlyList<RequestHeader>? collectionHeaders,
        IReadOnlyList<RequestHeader>? requestHeaders)
    {
        var merged = new List<RequestHeader>();
        var headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (collectionHeaders is { })
        {
            foreach (var header in collectionHeaders)
            {
                headerIndexes[header.Name] = merged.Count;
                merged.Add(header);
            }
        }

        if (requestHeaders is { })
        {
            foreach (var requestHeader in requestHeaders)
            {
                if (headerIndexes.TryGetValue(requestHeader.Name, out var index))
                {
                    merged[index] = requestHeader;
                }
                else
                {
                    headerIndexes[requestHeader.Name] = merged.Count;
                    merged.Add(requestHeader);
                }
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    /// <summary>
    /// Builds a persistence snapshot from the selected collection and the current header rows.
    /// Returns null when no collection is selected.
    /// </summary>
    public CollectionInheritedHeadersAutoSaveSnapshot? BuildSnapshot(
        Collection? selectedCollection,
        IEnumerable<RequestHeaderViewModel> headerViewModels)
    {
        if (selectedCollection is not { } collection)
        {
            return null;
        }

        return new CollectionInheritedHeadersAutoSaveSnapshot(
            collection.Id,
            collection.Name,
            collection.SourcePath,
            collection.BaseUrl,
            collection.Requests,
            BuildHeaders(headerViewModels));
    }

    /// <summary>
    /// Marks the current header state as pending and requests a debounced auto-save.
    /// No-op while suppressed or when no collection is selected.
    /// </summary>
    public void QueueAutoSave(Collection? selectedCollection, IEnumerable<RequestHeaderViewModel> headerViewModels)
    {
        if (IsAutoSaveSuppressed)
        {
            return;
        }

        if (BuildSnapshot(selectedCollection, headerViewModels) is not { } snapshot)
        {
            return;
        }

        _hasPendingAutoSave = true;
        _pendingAutoSaveSnapshot = snapshot;

        _autoSaveVersion++;
        _autoSaveRequestedSubject.OnNext(snapshot);
    }

    /// <summary>
    /// Persists a debounced snapshot. Failures are logged and swallowed so a failed
    /// auto-save never crashes the UI; the pending state is kept for a later retry/flush.
    /// </summary>
    public Task TriggerAutoSave(CollectionInheritedHeadersAutoSaveSnapshot snapshot)
    {
        var autoSaveVersion = _autoSaveVersion;
        _autoSaveTask = TriggerAutoSaveAsync(snapshot, autoSaveVersion);
        return _autoSaveTask;
    }

    private async Task TriggerAutoSaveAsync(CollectionInheritedHeadersAutoSaveSnapshot snapshot, int autoSaveVersion)
    {
        try
        {
            await _invokeOnUiThreadAsync(async () => await PersistSnapshotAsync(
                snapshot,
                CancellationToken.None,
                selectUpdatedCollection: false));

            if (autoSaveVersion == _autoSaveVersion)
            {
                ClearPendingAutoSaveState();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Warning(exception, "Collection inherited headers auto-save failed");
        }
    }

    /// <summary>
    /// Persists any pending auto-save immediately, waiting for an in-flight debounced save
    /// first. Used when the main window closes before the debounce interval elapses.
    /// </summary>
    public async Task FlushPendingAutoSaveAsync()
    {
        if (!_hasPendingAutoSave || _pendingAutoSaveSnapshot is not { } snapshot)
        {
            return;
        }

        if (_autoSaveTask is { } autoSaveTask)
        {
            await autoSaveTask.ConfigureAwait(false);
        }

        await PersistSnapshotAsync(snapshot, CancellationToken.None, selectUpdatedCollection: true);
        ClearPendingAutoSaveState();
    }

    /// <summary>Persists the current header rows immediately (explicit Save command).</summary>
    public async Task SaveAsync(
        Collection? selectedCollection,
        IEnumerable<RequestHeaderViewModel> headerViewModels,
        CancellationToken cancellationToken)
    {
        if (BuildSnapshot(selectedCollection, headerViewModels) is { } snapshot)
        {
            await PersistSnapshotAsync(snapshot, cancellationToken, selectUpdatedCollection: true);
            ClearPendingAutoSaveState();
        }
    }

    private async Task PersistSnapshotAsync(
        CollectionInheritedHeadersAutoSaveSnapshot snapshot,
        CancellationToken cancellationToken,
        bool selectUpdatedCollection)
    {
        var currentCollection = _findCollectionById(snapshot.CollectionId);
        if (currentCollection is not { })
        {
            return;
        }

        if (HeadersEqual(currentCollection.Headers, snapshot.InheritedHeaders))
        {
            return;
        }

        await _collectionRepository.UpdateAsync(
            snapshot.CollectionId,
            snapshot.CollectionName,
            snapshot.CollectionSourcePath,
            snapshot.CollectionBaseUrl,
            snapshot.CollectionRequests,
            snapshot.InheritedHeaders,
            cancellationToken);

        await _reloadCollectionsAsync(cancellationToken);
        if (selectUpdatedCollection)
        {
            _reselectUpdatedCollection(snapshot.CollectionId);
        }

        _logger.Information("Updated inherited headers for collection {CollectionName}", snapshot.CollectionName);
    }

    private void ClearPendingAutoSaveState()
    {
        _hasPendingAutoSave = false;
        _pendingAutoSaveSnapshot = null;
    }

    /// <summary>Ordinal, order-sensitive equality of two header lists (null-tolerant).</summary>
    public static bool HeadersEqual(IReadOnlyList<RequestHeader>? left, IReadOnlyList<RequestHeader>? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            var leftHeader = left[index];
            var rightHeader = right[index];
            if (!string.Equals(leftHeader.Name, rightHeader.Name, StringComparison.Ordinal)
                || !string.Equals(leftHeader.Value, rightHeader.Value, StringComparison.Ordinal)
                || leftHeader.IsEnabled != rightHeader.IsEnabled)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="header"/> matches one of <paramref name="inheritedHeaders"/>
    /// by name (case-insensitive), value, and enabled state.
    /// </summary>
    public static bool IsInheritedHeader(RequestHeader header, IReadOnlyList<RequestHeader>? inheritedHeaders) =>
        inheritedHeaders?.Any(inheritedHeader =>
            string.Equals(inheritedHeader.Name, header.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(inheritedHeader.Value, header.Value, StringComparison.Ordinal)
            && inheritedHeader.IsEnabled == header.IsEnabled) == true;

    /// <summary>
    /// True when <paramref name="manualRequestHeaders"/> already contains a row named
    /// <paramref name="headerName"/> (case-insensitive), i.e. the user manually overrode it.
    /// </summary>
    public static bool HasManualHeaderOverride(string headerName, IReadOnlyList<RequestHeaderViewModel> manualRequestHeaders) =>
        manualRequestHeaders.Any(header => string.Equals(header.Name, headerName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds the request within <paramref name="collection"/> whose method, path, and name
    /// match the given values (all ordinal), or <see langword="null"/> when none match.
    /// </summary>
    public static CollectionRequest? FindMatchingRequest(Collection collection, string method, string path, string name) =>
        collection.Requests.FirstOrDefault(request =>
            string.Equals(request.Method, method, StringComparison.Ordinal)
            && string.Equals(request.Path, path, StringComparison.Ordinal)
            && string.Equals(request.Name, name, StringComparison.Ordinal));

    public void Dispose()
    {
        _autoSaveRequestedSubject.OnCompleted();
        _autoSaveRequestedSubject.Dispose();
    }
}

/// <summary>
/// Immutable snapshot of a collection's identity, requests, and edited inherited headers,
/// captured at queue time so a debounced save persists exactly what the user last edited.
/// </summary>
public sealed record CollectionInheritedHeadersAutoSaveSnapshot(
    int CollectionId,
    string CollectionName,
    string? CollectionSourcePath,
    string? CollectionBaseUrl,
    IReadOnlyList<CollectionRequest> CollectionRequests,
    IReadOnlyList<RequestHeader>? InheritedHeaders);
