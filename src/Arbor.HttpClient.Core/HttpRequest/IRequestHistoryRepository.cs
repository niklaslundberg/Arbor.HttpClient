
namespace Arbor.HttpClient.Core.HttpRequest;

public interface IRequestHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RequestHistoryEntry request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequestHistoryEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
