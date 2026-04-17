using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Abstractions;

public interface IScheduledJobRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> SaveAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default);
}
