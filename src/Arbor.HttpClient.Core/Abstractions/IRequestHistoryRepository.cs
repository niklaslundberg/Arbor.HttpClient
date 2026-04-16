using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Abstractions;

public interface IRequestHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
