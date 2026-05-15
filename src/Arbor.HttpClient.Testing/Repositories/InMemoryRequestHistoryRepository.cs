using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRequestHistoryRepository"/> for testing.
/// </summary>
public sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
{
    private readonly List<RequestHistoryEntry> _items = [];
    private readonly object _lock = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(RequestHistoryEntry request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.Add(request);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RequestHistoryEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<RequestHistoryEntry>>(_items.Take(limit).ToList());
        }
    }

    /// <summary>Gets all request history entries. Useful for test assertions.</summary>
    public IReadOnlyList<RequestHistoryEntry> Items
    {
        get
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }
    }

    /// <summary>Clears all request history entries. Useful for test cleanup.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
