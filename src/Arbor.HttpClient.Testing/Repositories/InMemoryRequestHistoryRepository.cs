using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// In-memory implementation of IRequestHistoryRepository for testing purposes.
/// Thread-safe and supports all repository operations.
/// </summary>
public sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
{
    private readonly List<SavedRequest> _items = [];
    private readonly object _lock = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.Add(request);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<SavedRequest>>(_items.Take(limit).ToList());
        }
    }

    /// <summary>
    /// Gets all saved requests (useful for testing assertions).
    /// </summary>
    public IReadOnlyList<SavedRequest> Items
    {
        get
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    /// <summary>
    /// Clears all saved requests (useful for test cleanup).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
