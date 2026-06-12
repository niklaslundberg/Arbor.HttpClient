using System.Collections.ObjectModel;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Owns the named-layout collection and coordinates layout-management commands for
/// saving, restoring, and removing named layouts.
/// </summary>
public sealed class LayoutWorkflow
{
    private readonly Dictionary<string, DockLayoutSnapshot> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);
    private int _layoutNameCounter = 1;

    /// <summary>Names of the saved layouts, ordered alphabetically. Bound to the layout-selection UI.</summary>
    public ObservableCollection<string> SavedLayoutNames { get; } = [];

    /// <summary>
    /// Replaces the saved layouts with those from <paramref name="layouts"/> and returns the
    /// layout name that should become selected (the first saved layout in alphabetical order,
    /// or <see langword="null"/> if none are saved).
    /// </summary>
    public string? LoadFromOptions(LayoutOptions? layouts)
    {
        _savedLayouts.Clear();

        if (layouts?.SavedLayouts is { } savedLayouts)
        {
            foreach (var namedLayout in savedLayouts)
            {
                if (!string.IsNullOrWhiteSpace(namedLayout.Name) && namedLayout.Layout is { } layout)
                {
                    _savedLayouts[namedLayout.Name] = layout;
                }
            }
        }

        RefreshSavedLayoutNames();
        UpdateLayoutNameCounter();
        return SavedLayoutNames.FirstOrDefault();
    }

    public bool TryGetLayout(string layoutName, out DockLayoutSnapshot? snapshot) =>
        _savedLayouts.TryGetValue(layoutName, out snapshot);

    public string? SaveLayoutAsNew(Func<DockLayoutSnapshot?> captureLayoutSnapshot)
    {
        var layoutSnapshot = captureLayoutSnapshot();
        if (layoutSnapshot is null)
        {
            return null;
        }

        var layoutName = GenerateNextLayoutName();
        _savedLayouts[layoutName] = layoutSnapshot;
        RefreshSavedLayoutNames();
        return layoutName;
    }

    public bool SaveLayoutToExisting(string? layoutName, Func<DockLayoutSnapshot?> captureLayoutSnapshot)
    {
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return false;
        }

        var layoutSnapshot = captureLayoutSnapshot();
        if (layoutSnapshot is null)
        {
            return false;
        }

        _savedLayouts[layoutName] = layoutSnapshot;
        RefreshSavedLayoutNames();
        return true;
    }

    public bool RestoreDefaultLayout(DockLayoutSnapshot? defaultLayout, Action<DockLayoutSnapshot> applyLayoutSnapshot)
    {
        if (defaultLayout is null)
        {
            return false;
        }

        applyLayoutSnapshot(defaultLayout);
        return true;
    }

    public RemoveLayoutResult RemoveLayout(string? layoutName, string? selectedLayoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName) || !_savedLayouts.Remove(layoutName))
        {
            return new RemoveLayoutResult(false, selectedLayoutName);
        }

        RefreshSavedLayoutNames();

        if (string.Equals(selectedLayoutName, layoutName, StringComparison.OrdinalIgnoreCase))
        {
            selectedLayoutName = SavedLayoutNames.FirstOrDefault();
        }

        return new RemoveLayoutResult(true, selectedLayoutName);
    }

    /// <summary>Builds the persisted list of named layouts, ordered alphabetically by name.</summary>
    public IReadOnlyList<NamedDockLayout> BuildNamedLayouts() =>
        _savedLayouts
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new NamedDockLayout { Name = item.Key, Layout = item.Value })
            .ToList();

    private void RefreshSavedLayoutNames()
    {
        var names = _savedLayouts.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        SavedLayoutNames.Clear();
        foreach (var name in names)
        {
            SavedLayoutNames.Add(name);
        }
    }

    private string GenerateNextLayoutName()
    {
        UpdateLayoutNameCounter();
        return $"Layout {_layoutNameCounter++}";
    }

    private void UpdateLayoutNameCounter()
    {
        while (_savedLayouts.ContainsKey($"Layout {_layoutNameCounter}"))
        {
            _layoutNameCounter++;
        }
    }
}

public sealed record RemoveLayoutResult(bool Removed, string? SelectedLayoutName);
