using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>Registers the Explorer (left panel) tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class LeftPanelDockRegistration : IDockPanelRegistration
{
    public LeftPanelDockRegistration(ILeftPanelContext leftPanelContext)
    {
        Dockable = new LeftPanelViewModel(leftPanelContext);
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
