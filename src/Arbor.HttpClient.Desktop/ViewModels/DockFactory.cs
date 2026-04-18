using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// Builds the initial Dock layout for the main window.
/// Panels: Explorer tool (left), Request document, Response document.
/// </summary>
public sealed class DockFactory : Factory
{
    private readonly MainWindowViewModel _mainVm;

    public DockFactory(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
    }

    public override IRootDock CreateLayout()
    {
        var leftPanel = new LeftPanelViewModel(_mainVm);
        var request = new RequestViewModel(_mainVm);
        var response = new ResponseViewModel(_mainVm);

        var leftToolDock = new ToolDock
        {
            Id = "left-tool-dock",
            Proportion = 0.25,
            ActiveDockable = leftPanel,
            VisibleDockables = CreateList<IDockable>(leftPanel),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible
        };

        var documentDock = new DocumentDock
        {
            Id = "document-dock",
            Proportion = 0.75,
            ActiveDockable = request,
            VisibleDockables = CreateList<IDockable>(request, response),
            IsCollapsable = false
        };

        var mainLayout = new ProportionalDock
        {
            Id = "main-layout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftToolDock,
                new ProportionalDockSplitter { Id = "main-splitter" },
                documentDock)
        };

        var rootDock = new RootDock
        {
            Id = "root",
            IsCollapsable = false,
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        return rootDock;
    }
}
