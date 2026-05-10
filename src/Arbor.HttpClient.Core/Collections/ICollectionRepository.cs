using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Collections;

public interface ICollectionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default, IReadOnlyList<RequestHeader>? headers = null);

    Task UpdateAsync(int collectionId, string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default, IReadOnlyList<RequestHeader>? headers = null);

    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default);
}
