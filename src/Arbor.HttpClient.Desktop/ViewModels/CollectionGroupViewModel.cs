using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// Represents a named group of collection requests for the tree-view display.
/// Groups are derived by splitting request paths on '/' and taking the first segment.
/// </summary>
public sealed partial class CollectionGroupViewModel : ObservableObject
{
    public CollectionGroupViewModel(string groupKey, IReadOnlyList<CollectionItemViewModel> items)
    {
        GroupKey = groupKey;
        Items = items;
    }

    public string GroupKey { get; }
    public IReadOnlyList<CollectionItemViewModel> Items { get; }

    [ObservableProperty]
    private bool _isExpanded = true;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
