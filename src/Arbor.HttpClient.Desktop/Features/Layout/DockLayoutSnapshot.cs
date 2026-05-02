namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class DockLayoutSnapshot
{
    public double LeftToolProportion { get; init; } = 0.25;

    public double DocumentProportion { get; init; } = 0.75;

    /// <summary>Proportion of the request dock in the vertical request/response split (0 means use default).</summary>
    public double RequestDockProportion { get; init; }

    /// <summary>Proportion of the response dock in the vertical request/response split (0 means use default).</summary>
    public double ResponseDockProportion { get; init; }

    public string? ActiveToolDockableId { get; init; }

    public List<string> LeftToolDockableOrder { get; init; } = [];

    public List<FloatingWindowSnapshot> FloatingWindows { get; init; } = [];

    /// <summary>Saved main window width in device-independent pixels (0 means use default).</summary>
    public double WindowWidth { get; init; }

    /// <summary>Saved main window height in device-independent pixels (0 means use default).</summary>
    public double WindowHeight { get; init; }

    /// <summary>Saved main window X position in screen pixels.</summary>
    public int WindowX { get; init; }

    /// <summary>Saved main window Y position in screen pixels.</summary>
    public int WindowY { get; init; }

    /// <summary>
    /// <see langword="true"/> when <see cref="WindowX"/> / <see cref="WindowY"/> contain an
    /// explicitly captured position (including the valid top-left corner position (0, 0)).
    /// <see langword="false"/> (the default after JSON deserialization when the field is absent)
    /// means position was never saved and should not be restored.
    /// </summary>
    public bool HasWindowPosition { get; init; }

    /// <summary>
    /// Full serialized dock tree structure. When present this supersedes the individual
    /// proportion and order fields and is used to restore any structural layout changes the
    /// user made (e.g. docking a panel to a new position). <see langword="null"/> for layouts
    /// saved before this field was introduced — the individual proportion/order fields are used
    /// as a fallback in that case.
    /// </summary>
    public DockTreeNode? DockTree { get; init; }
}
