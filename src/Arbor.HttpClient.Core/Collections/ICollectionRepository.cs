using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Collections;

/// <summary>
/// Defines persistence operations for HTTP request collections stored by the application.
/// </summary>
public interface ICollectionRepository
{
    /// <summary>Creates the underlying storage schema if it does not yet exist.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new collection and returns its generated identifier.
    /// </summary>
    /// <param name="name">Display name for the collection.</param>
    /// <param name="sourcePath">Optional file path the collection was imported from.</param>
    /// <param name="baseUrl">Optional server base URL applied to relative request paths.</param>
    /// <param name="requests">Ordered list of requests that belong to this collection.</param>
    /// <param name="headers">Optional collection-level security headers shared by all requests.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The generated integer identifier of the saved collection.</returns>
    Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, IReadOnlyList<RequestHeader>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing collection identified by <paramref name="collectionId"/>.
    /// </summary>
    /// <param name="collectionId">Identifier of the collection to update.</param>
    /// <param name="name">Updated display name.</param>
    /// <param name="sourcePath">Updated source file path.</param>
    /// <param name="baseUrl">Updated base URL.</param>
    /// <param name="requests">Replacement request list.</param>
    /// <param name="headers">Replacement collection-level headers.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpdateAsync(int collectionId, string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, IReadOnlyList<RequestHeader>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>Returns all stored collections in insertion order.</summary>
    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the collection identified by <paramref name="collectionId"/>.</summary>
    Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default);
}
