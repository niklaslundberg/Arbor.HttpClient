using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>Registers the Request document dockable with <see cref="DockFactory"/>.</summary>
public sealed class RequestDockRegistration : IDockPanelRegistration
{
    public RequestDockRegistration(IRequestPanelContext requestPanelContext)
    {
        Dockable = new RequestViewModel(requestPanelContext);
    }

    public DockPanelLocation Location => DockPanelLocation.Document;

    public IDockable Dockable { get; }
}
