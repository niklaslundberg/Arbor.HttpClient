using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Cookies;

/// <summary>Registers the Cookie Jar tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class CookieJarDockRegistration : IDockPanelRegistration
{
    public CookieJarDockRegistration(CookieJarViewModel cookieJarViewModel)
    {
        Dockable = cookieJarViewModel;
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
