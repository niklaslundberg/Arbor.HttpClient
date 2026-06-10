using System.Collections.Generic;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Represents a named group of collection requests for the tree-view display.
/// Groups are derived by splitting request paths on '/' and taking the first segment.
/// </summary>
public sealed partial class CollectionGroupViewModel : ReactiveViewModelBase
{
    public CollectionGroupViewModel(string groupKey, IReadOnlyList<CollectionItemViewModel> items)
    {
        GroupKey = groupKey;
        Items = items;
    }

    public string GroupKey { get; }
    public IReadOnlyList<CollectionItemViewModel> Items { get; }

    [Reactive]
    private bool _isExpanded = true;

    [ReactiveCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
