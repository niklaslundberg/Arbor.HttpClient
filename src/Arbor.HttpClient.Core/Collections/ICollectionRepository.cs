
namespace Arbor.HttpClient.Core.Collections;

public interface ICollectionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default);

    Task UpdateAsync(int collectionId, string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default);
}
