
namespace Arbor.HttpClient.Core.HttpRequest;

/// <summary>
/// Defines persistence operations for the HTTP request history log.
/// </summary>
public interface IRequestHistoryRepository
{
    /// <summary>Creates the underlying storage schema if it does not yet exist.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends a completed request entry to the history.</summary>
    /// <param name="request">The history entry to persist.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveAsync(RequestHistoryEntry request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent history entries up to <paramref name="limit"/> items,
    /// ordered newest-first.
    /// </summary>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<IReadOnlyList<RequestHistoryEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
