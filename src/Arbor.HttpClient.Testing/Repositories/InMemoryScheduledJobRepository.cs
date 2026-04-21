using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// In-memory implementation of IScheduledJobRepository for testing purposes.
/// Thread-safe and supports all repository operations.
/// </summary>
public sealed class InMemoryScheduledJobRepository : IScheduledJobRepository
{
    private readonly List<ScheduledJobConfig> _items = [];
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> SaveAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var id = _nextId++;
            _items.Add(config with { Id = id });
            return Task.FromResult(id);
        }
    }

    public Task UpdateAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(x => x.Id == config.Id);
            if (idx >= 0)
            {
                _items[idx] = config;
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.RemoveAll(x => x.Id == id);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ScheduledJobConfig>>(_items.ToList());
        }
    }

    /// <summary>
    /// Clears all scheduled jobs (useful for test cleanup).
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
