using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>Where a registered dockable is placed in the layout built by <see cref="DockFactory"/>.</summary>
public enum DockPanelLocation
{
    /// <summary>The left-side <c>ToolDock</c> ("left-tool-dock").</summary>
    LeftTool,

    /// <summary>The document area ("request-dock").</summary>
    Document
}

/// <summary>
/// Declares a single dockable panel for <see cref="DockFactory"/>. Each feature that owns a dock
/// panel provides one implementation, allowing <see cref="DockFactory"/> to compose the layout
/// without referencing feature view models directly.
/// </summary>
public interface IDockPanelRegistration
{
    /// <summary>Where this dockable is placed in the layout.</summary>
    DockPanelLocation Location { get; }

    /// <summary>The dockable view model for this panel.</summary>
    IDockable Dockable { get; }
}
