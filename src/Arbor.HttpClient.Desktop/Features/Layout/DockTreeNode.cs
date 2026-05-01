namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Represents a single node in the serialized dock layout tree.
/// Used to persist and restore the full dock structure including any structural changes
/// made by the user (e.g., docking a panel to a new position in the layout).
/// </summary>
public sealed class DockTreeNode
{
    /// <summary>Optional node ID. <see langword="null"/> for auto-generated docks created by user drag-and-drop.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Node type discriminator:
    /// <list type="bullet">
    ///   <item><term>"Root"</term><description>RootDock — the top-level layout root.</description></item>
    ///   <item><term>"Proportional"</term><description>ProportionalDock — horizontal or vertical splitter container.</description></item>
    ///   <item><term>"Splitter"</term><description>ProportionalDockSplitter — the visible resize handle.</description></item>
    ///   <item><term>"Tool"</term><description>ToolDock — container for tool panels.</description></item>
    ///   <item><term>"Document"</term><description>DocumentDock — container for document panels.</description></item>
    /// </list>
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Layout proportion (0 means use default; ignored for "Splitter" nodes).</summary>
    public double Proportion { get; init; }

    /// <summary>
    /// Orientation for "Proportional" nodes: "Horizontal" or "Vertical".
    /// <see langword="null"/> for all other node types.
    /// </summary>
    public string? Orientation { get; init; }

    /// <summary>
    /// Alignment for "Tool" nodes: "Left", "Right", "Top", "Bottom", or "Unset".
    /// <see langword="null"/> for all other node types.
    /// </summary>
    public string? Alignment { get; init; }

    /// <summary>
    /// Grip mode for "Tool" nodes: "Visible", "AutoHide", or "Hidden".
    /// <see langword="null"/> for all other node types.
    /// </summary>
    public string? GripMode { get; init; }

    /// <summary>Whether the dock is collapsable. Used for "Document" and "Tool" nodes.</summary>
    public bool IsCollapsable { get; init; }

    /// <summary>
    /// IDs of the content dockables (tool/document view-models) held by this node.
    /// Only populated for "Tool" and "Document" type nodes.
    /// </summary>
    public List<string> ContentIds { get; init; } = [];

    /// <summary>The active content dockable ID for "Tool" and "Document" nodes.</summary>
    public string? ActiveContentId { get; init; }

    /// <summary>Child structural dock nodes. Only populated for "Root" and "Proportional" nodes.</summary>
    public List<DockTreeNode> Children { get; init; } = [];
}
