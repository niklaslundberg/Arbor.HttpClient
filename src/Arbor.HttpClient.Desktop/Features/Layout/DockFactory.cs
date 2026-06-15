using System.Collections.Generic;
using System.Linq;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Builds the initial Dock layout for the main window from a set of feature-provided
/// <see cref="IDockPanelRegistration"/> entries. Adding a new dock panel requires only a new
/// registration; this factory does not reference feature view models directly.
/// </summary>
public sealed class DockFactory : Factory
{
    private readonly IReadOnlyList<IDockPanelRegistration> _registrations;

    public DockFactory(IReadOnlyList<IDockPanelRegistration> registrations)
    {
        _registrations = registrations;
    }

    /// <summary>The left-side ToolDock; used to activate the Options tool programmatically.</summary>
    public ToolDock? LeftToolDock { get; private set; }

    /// <summary>Returns the registered dockable of type <typeparamref name="T"/>, if any.</summary>
    public T? GetDockable<T>() where T : IDockable =>
        _registrations.Select(registration => registration.Dockable).OfType<T>().FirstOrDefault();

    /// <summary>
    /// Updates <see cref="LeftToolDock"/> after a layout tree has been rebuilt from a saved
    /// snapshot. The new reference is the ToolDock that currently owns the left-tool dockables (as
    /// reported by <see cref="IDockable.Owner"/> after <see cref="Factory.InitLayout"/> has run).
    /// Falls back to the original "left-tool-dock" reference when none of the left-tool dockables
    /// have a <see cref="ToolDock"/> owner.
    /// </summary>
    public void UpdateLeftToolDock()
    {
        var leftToolDockable = _registrations
            .Where(registration => registration.Location == DockPanelLocation.LeftTool)
            .Select(registration => registration.Dockable)
            .FirstOrDefault();

        if (leftToolDockable is { Owner: ToolDock ownerDock })
        {
            LeftToolDock = ownerDock;
        }
    }

    public override IRootDock CreateLayout()
    {
        var leftToolDockables = _registrations
            .Where(registration => registration.Location == DockPanelLocation.LeftTool)
            .Select(registration => registration.Dockable)
            .ToArray();

        var leftToolDock = new ToolDock
        {
            Id = "left-tool-dock",
            Proportion = 0.25,
            ActiveDockable = leftToolDockables.FirstOrDefault(),
            VisibleDockables = CreateList(leftToolDockables),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible
        };
        LeftToolDock = leftToolDock;

        var documentDockables = _registrations
            .Where(registration => registration.Location == DockPanelLocation.Document)
            .Select(registration => registration.Dockable)
            .ToArray();

        var requestDock = new DocumentDock
        {
            Id = "request-dock",
            Proportion = 1,
            ActiveDockable = documentDockables.FirstOrDefault(),
            VisibleDockables = CreateList(documentDockables),
            IsCollapsable = false
        };

        var documentLayout = new ProportionalDock
        {
            Id = "document-layout",
            Proportion = 0.75,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(requestDock)
        };

        var mainLayout = new ProportionalDock
        {
            Id = "main-layout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftToolDock,
                new ProportionalDockSplitter { Id = "main-splitter" },
                documentLayout)
        };

        var rootDock = new RootDock
        {
            Id = "root",
            IsCollapsable = false,
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout),
            Windows = CreateList<IDockWindow>()
        };

        DefaultHostWindowLocator = () => new HostWindow();

        return rootDock;
    }
}
