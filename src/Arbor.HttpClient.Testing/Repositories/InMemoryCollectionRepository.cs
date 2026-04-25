using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ICollectionRepository"/> for testing.
/// </summary>
public sealed class InMemoryCollectionRepository : ICollectionRepository
{
    private readonly List<Collection> _items = [];
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> SaveAsync(
        string name,
        string? sourcePath,
        string? baseUrl,
        IReadOnlyList<CollectionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var id = _nextId++;
            _items.Add(new Collection(id, name, sourcePath, baseUrl, requests.ToList()));
            return Task.FromResult(id);
        }
    }

    public Task UpdateAsync(
        int collectionId,
        string name,
        string? sourcePath,
        string? baseUrl,
        IReadOnlyList<CollectionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(c => c.Id == collectionId);
            if (index >= 0)
            {
                _items[index] = new Collection(collectionId, name, sourcePath, baseUrl, requests.ToList());
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Collection>>(_items.ToList());
        }
    }

    public Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.RemoveAll(c => c.Id == collectionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>Clears all collections. Useful for test cleanup.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _nextId = 1;
        }
    }
}
