using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Testing.Repositories;

/// <summary>
/// In-memory implementation of IEnvironmentRepository for testing purposes.
/// Thread-safe and supports all repository operations.
/// </summary>
public sealed class InMemoryEnvironmentRepository : IEnvironmentRepository
{
    private readonly List<RequestEnvironment> _items = [];
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> SaveAsync(
        string name,
        IReadOnlyList<EnvironmentVariable> variables,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var id = _nextId++;
            _items.Add(new RequestEnvironment(id, name, variables.ToList()));
            return Task.FromResult(id);
        }
    }

    public Task UpdateAsync(
        int environmentId,
        string name,
        IReadOnlyList<EnvironmentVariable> variables,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(e => e.Id == environmentId);
            if (idx >= 0)
            {
                _items[idx] = new RequestEnvironment(environmentId, name, variables.ToList());
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<RequestEnvironment>>(_items.ToList());
        }
    }

    public Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _items.RemoveAll(e => e.Id == environmentId);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all environments (useful for test cleanup).
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
