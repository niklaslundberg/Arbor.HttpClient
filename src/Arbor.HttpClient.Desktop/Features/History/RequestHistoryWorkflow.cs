using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.Features.History;

/// <summary>
/// Owns the request-history list: loading recent entries from <see cref="IRequestHistoryRepository"/>
/// and applying the search-query filter that backs the History panel.
/// </summary>
public sealed class RequestHistoryWorkflow
{
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly List<RequestHistoryEntry> _allHistory = [];

    public RequestHistoryWorkflow(IRequestHistoryRepository requestHistoryRepository)
    {
        _requestHistoryRepository = requestHistoryRepository;
    }

    /// <summary>Filtered history entries bound by the History panel.</summary>
    public ObservableCollection<RequestHistoryEntry> History { get; } = [];

    /// <summary>Reloads recent history from the repository and re-applies <paramref name="searchQuery"/>.</summary>
    public async Task LoadAsync(string searchQuery, CancellationToken cancellationToken = default)
    {
        var requests = (await _requestHistoryRepository.GetRecentAsync(100, cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        _allHistory.Clear();
        _allHistory.AddRange(requests);

        ApplyFilter(searchQuery);
    }

    /// <summary>
    /// Filters the loaded history by <paramref name="query"/> (matching name, URL, or method)
    /// and updates <see cref="History"/> in place, preserving item identity for unchanged entries.
    /// </summary>
    public void ApplyFilter(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allHistory
            : _allHistory
                .Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Url.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Method.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Remove items no longer in the filtered set
        for (var i = History.Count - 1; i >= 0; i--)
        {
            if (!filtered.Contains(History[i]))
            {
                History.RemoveAt(i);
            }
        }

        // Append items that are missing, maintaining order
        for (var i = 0; i < filtered.Count; i++)
        {
            if (i >= History.Count || !ReferenceEquals(History[i], filtered[i]))
            {
                History.Insert(i, filtered[i]);
            }
        }
    }

    /// <summary>
    /// Builds the request-editor field values projected from <paramref name="entry"/>,
    /// used to populate the active request editor when a history entry is loaded.
    /// </summary>
    public static RequestHistoryEditorProjection BuildEditorProjection(RequestHistoryEntry entry) =>
        new(entry.Method, entry.Name, entry.Url, entry.Body ?? string.Empty);
}

/// <summary>Request-editor field values projected from a <see cref="RequestHistoryEntry"/>.</summary>
public sealed record RequestHistoryEditorProjection(string Method, string Name, string Url, string Body);
