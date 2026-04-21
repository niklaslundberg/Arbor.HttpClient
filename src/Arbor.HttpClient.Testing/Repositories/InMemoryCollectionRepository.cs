using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// In-memory implementation of ICollectionRepository for testing purposes.
/// Thread-safe and supports all repository operations.
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
            var collection = new Collection(id, name, sourcePath, baseUrl, requests.ToList());
            _items.Add(collection);
            return Task.FromResult(id);
        }
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

    /// <summary>
    /// Clears all collections (useful for test cleanup).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _nextId = 1;
        }
    }
}
