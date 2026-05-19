namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Coordinates layout-management commands for saving, restoring, and removing named layouts.
/// </summary>
public sealed class LayoutWorkflow
{
    public string? SaveLayoutAsNew(
        Func<DockLayoutSnapshot?> captureLayoutSnapshot,
        Func<string> generateNextLayoutName,
        IDictionary<string, DockLayoutSnapshot> savedLayouts)
    {
        var layoutSnapshot = captureLayoutSnapshot();
        if (layoutSnapshot is null)
        {
            return null;
        }

        var layoutName = generateNextLayoutName();
        savedLayouts[layoutName] = layoutSnapshot;
        return layoutName;
    }

    public bool SaveLayoutToExisting(
        string? layoutName,
        Func<DockLayoutSnapshot?> captureLayoutSnapshot,
        IDictionary<string, DockLayoutSnapshot> savedLayouts)
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

        savedLayouts[layoutName] = layoutSnapshot;
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

    public RemoveLayoutResult RemoveLayout(
        string? layoutName,
        IDictionary<string, DockLayoutSnapshot> savedLayouts,
        string? selectedLayoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName) || !savedLayouts.Remove(layoutName))
        {
            return new RemoveLayoutResult(false, selectedLayoutName);
        }

        if (string.Equals(selectedLayoutName, layoutName, StringComparison.OrdinalIgnoreCase))
        {
            selectedLayoutName = savedLayouts.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        return new RemoveLayoutResult(true, selectedLayoutName);
    }
}

public sealed record RemoveLayoutResult(bool Removed, string? SelectedLayoutName);
