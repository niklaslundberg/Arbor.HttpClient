using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Logging;

/// <summary>Registers the Logs tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class LogPanelDockRegistration : IDockPanelRegistration
{
    public LogPanelDockRegistration(LogWindowViewModel logWindowViewModel)
    {
        Dockable = new LogPanelViewModel(logWindowViewModel);
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
