using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.Cookies;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Builds the initial Dock layout for the main window.
    /// Panels: Explorer tool (left), Options tool (left), Environments tool (left), Logs tool (left), Cookie Jar tool (left), Request document, Response document.
/// </summary>
public sealed class DockFactory : Factory
{
    private readonly MainWindowViewModel _mainVm;
    private readonly EnvironmentsViewModel _environmentsViewModel;
    private readonly OptionsViewModel _optionsViewModel;
    private readonly CookieJarViewModel _cookieJarViewModel;

    public DockFactory(MainWindowViewModel mainVm, EnvironmentsViewModel environmentsViewModel, OptionsViewModel optionsViewModel, CookieJarViewModel cookieJarViewModel)
    {
        _mainVm = mainVm;
        _environmentsViewModel = environmentsViewModel;
        _optionsViewModel = optionsViewModel;
        _cookieJarViewModel = cookieJarViewModel;
    }

    /// <summary>The left-side ToolDock; used to activate the Options tool programmatically.</summary>
    public ToolDock? LeftToolDock { get; private set; }

    /// <summary>The Explorer (left panel) tool dockable.</summary>
    public LeftPanelViewModel? LeftPanelViewModel { get; private set; }

    /// <summary>The Options tool dockable.</summary>
    public OptionsViewModel? OptionsViewModel { get; private set; }
    public EnvironmentsViewModel? EnvironmentsViewModel { get; private set; }
    public LogPanelViewModel? LogPanelViewModel { get; private set; }
    public CookieJarViewModel? CookieJarViewModel { get; private set; }
    public LayoutManagementViewModel? LayoutManagementViewModel { get; private set; }

    public override IRootDock CreateLayout()
    {
        var leftPanel = new LeftPanelViewModel(_mainVm);
        var options = _optionsViewModel;
        var environments = _environmentsViewModel;
        var logs = new LogPanelViewModel(_mainVm);
        var cookieJar = _cookieJarViewModel;
        var layoutManagement = new LayoutManagementViewModel(_mainVm);
        LeftPanelViewModel = leftPanel;
        OptionsViewModel = options;
        EnvironmentsViewModel = environments;
        LogPanelViewModel = logs;
        CookieJarViewModel = cookieJar;
        LayoutManagementViewModel = layoutManagement;

        var leftToolDock = new ToolDock
        {
            Id = "left-tool-dock",
            Proportion = 0.25,
            ActiveDockable = leftPanel,
            VisibleDockables = CreateList<IDockable>(leftPanel, options, environments, logs, cookieJar, layoutManagement),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible
        };
        LeftToolDock = leftToolDock;

        var request = new RequestViewModel(_mainVm);
        var response = new ResponseViewModel(_mainVm);

        var requestDock = new DocumentDock
        {
            Id = "request-dock",
            Proportion = 0.6,
            ActiveDockable = request,
            VisibleDockables = CreateList<IDockable>(request),
            IsCollapsable = false
        };

        var responseDock = new DocumentDock
        {
            Id = "response-dock",
            Proportion = 0.4,
            ActiveDockable = response,
            VisibleDockables = CreateList<IDockable>(response),
            IsCollapsable = false
        };

        var documentLayout = new ProportionalDock
        {
            Id = "document-layout",
            Proportion = 0.75,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                requestDock,
                new ProportionalDockSplitter { Id = "document-splitter" },
                responseDock)
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
