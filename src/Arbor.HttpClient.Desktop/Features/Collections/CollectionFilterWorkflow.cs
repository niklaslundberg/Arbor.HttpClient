namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Owns the collections-panel filter/sort/group pipeline: filters requests by a search query,
/// sorts them by the selected order, and rebuilds tree-view groups while preserving the
/// user's per-group expansion state across filter and sort changes.
/// </summary>
public sealed class CollectionFilterWorkflow
{
    public CollectionFilterResult Apply(
        IEnumerable<CollectionItemViewModel> items,
        string searchQuery,
        string sortBy,
        IReadOnlyDictionary<string, bool> previousGroupExpansion)
    {
        var filtered = items;

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || item.Path.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || item.Method.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        filtered = sortBy switch
        {
            "Name" => filtered.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            "Method" => filtered.OrderBy(item => item.Method, StringComparer.OrdinalIgnoreCase),
            "Path" => filtered.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase),
            _ => filtered
        };

        var filteredList = filtered.ToList();

        var groups = new List<CollectionGroupViewModel>();
        foreach (var group in filteredList.GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase))
        {
            var groupViewModel = new CollectionGroupViewModel(group.Key, group.ToList());
            if (previousGroupExpansion.TryGetValue(group.Key, out var wasExpanded))
            {
                groupViewModel.IsExpanded = wasExpanded;
            }

            groups.Add(groupViewModel);
        }

        return new CollectionFilterResult(filteredList, groups);
    }
}

/// <summary>Filtered/sorted flat items plus the rebuilt tree-view groups.</summary>
public sealed record CollectionFilterResult(
    IReadOnlyList<CollectionItemViewModel> Items,
    IReadOnlyList<CollectionGroupViewModel> Groups);
