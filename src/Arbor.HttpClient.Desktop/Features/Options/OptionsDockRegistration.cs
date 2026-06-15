using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Options;

/// <summary>Registers the Options tool dockable with <see cref="DockFactory"/>.</summary>
public sealed class OptionsDockRegistration : IDockPanelRegistration
{
    public OptionsDockRegistration(OptionsViewModel optionsViewModel)
    {
        Dockable = optionsViewModel;
    }

    public DockPanelLocation Location => DockPanelLocation.LeftTool;

    public IDockable Dockable { get; }
}
