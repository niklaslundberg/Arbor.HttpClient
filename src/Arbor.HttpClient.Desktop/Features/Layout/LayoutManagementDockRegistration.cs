using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>Registers the Layout management tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class LayoutManagementDockRegistration : IDockPanelRegistration
{
    public LayoutManagementDockRegistration(LayoutManagementViewModel layoutManagementViewModel)
    {
        Dockable = layoutManagementViewModel;
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
