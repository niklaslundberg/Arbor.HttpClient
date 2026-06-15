using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>Registers the Environments tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class EnvironmentsDockRegistration : IDockPanelRegistration
{
    public EnvironmentsDockRegistration(EnvironmentsViewModel environmentsViewModel)
    {
        Dockable = environmentsViewModel;
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
