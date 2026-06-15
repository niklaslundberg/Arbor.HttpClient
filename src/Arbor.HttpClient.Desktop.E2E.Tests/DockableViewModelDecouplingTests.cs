using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Messaging;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.GraphQl;
using Arbor.HttpClient.Desktop.Features.History;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Scripting;
using Arbor.HttpClient.Desktop.Features.Sse;
using Arbor.HttpClient.Desktop.Features.WebSocket;
using Arbor.HttpClient.Testing.Repositories;
using ReactiveUI;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Verifies that the dockable view models no longer depend on the whole
/// <c>MainWindowViewModel</c>: <see cref="LogPanelViewModel"/> takes the log feature VM
/// directly, and <see cref="LayoutManagementViewModel"/> owns its saved-layout state and
/// commands directly (taking only the dock-tree/persistence delegates it needs).
/// </summary>
public class DockableViewModelDecouplingTests
{
    [Fact]
    public void LogPanelViewModel_ExposesProvidedLogWindowViewModel()
    {
        using var sink = new InMemorySink();
        using var logs = new LogWindowViewModel(sink);

        var dockable = new LogPanelViewModel(logs);

        dockable.Logs.Should().BeSameAs(logs);
        dockable.Id.Should().Be("log-panel");
        dockable.Title.Should().Be("Logs");
    }

    [Fact]
    public void LayoutManagementViewModel_OwnsSavedLayoutStateAndCommands()
    {
        using var dockable = new LayoutManagementViewModel(
            refreshDockTreeCache: () => { },
            captureLayoutSnapshot: () => null,
            applyLayoutSnapshot: _ => { },
            persistLayoutOptions: () => { },
            getDefaultLayout: () => null);

        dockable.Id.Should().Be("layout-management");
        dockable.Title.Should().Be("Layout");
        dockable.SavedLayoutNames.Should().BeEmpty();
        dockable.SelectedLayoutName.Should().BeNull();
        dockable.SaveLayoutAsNewCommand.Should().NotBeNull();
        dockable.SaveLayoutToExistingCommand.Should().NotBeNull();
        dockable.RemoveLayoutCommand.Should().NotBeNull();
        dockable.RestoreDefaultLayoutCommand.Should().NotBeNull();
    }

    [Fact]
    public void LeftPanelViewModel_ExposesContextAndProxiesRemoveScheduledJobCommand()
    {
        var context = new FakeLeftPanelContext();

        var dockable = new LeftPanelViewModel(context);

        dockable.App.Should().BeSameAs(context);
        dockable.Id.Should().Be("left-panel");
        dockable.Title.Should().Be("Explorer");
        dockable.RemoveScheduledJobCommand.Should().BeSameAs(context.ScheduledJobsPanel.RemoveJobCommand);
    }

    [Fact]
    public void RequestViewModel_ExposesContextAndProxiesEditorCommands()
    {
        var editor = new RequestEditorViewModel(new VariableResolver(), () => []);
        var context = new FakeRequestPanelContext(editor);

        var dockable = new RequestViewModel(context);

        dockable.App.Should().BeSameAs(context);
        dockable.Id.Should().Be("request");
        dockable.Title.Should().Be("Request");
        dockable.RemoveHeaderCommand.Should().BeSameAs(editor.RemoveHeaderCommand);
        dockable.RemoveQueryParameterCommand.Should().BeSameAs(editor.RemoveQueryParameterCommand);
    }

    /// <summary>
    /// Minimal <see cref="ILeftPanelContext"/> stub. Only the members the dockable VM touches in
    /// C# (<see cref="ILeftPanelContext.RemoveScheduledJobCommand"/>) are asserted; the rest are
    /// AXAML-bound and exercised by the rendering screenshot tests, so they return inert defaults.
    /// </summary>
    private sealed class FakeLeftPanelContext : ILeftPanelContext
    {
        private static readonly ICommand NoOp = ReactiveCommand.Create(() => { });

        public IReadOnlyList<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; } = [];

        public string LeftPanelTab => "History";
        public ICommand ShowHistoryTabCommand => NoOp;
        public ICommand ShowCollectionsTabCommand => NoOp;
        public ICommand ShowScheduledJobsTabCommand => NoOp;

        public HistoryPanelViewModel HistoryPanel { get; } =
            new(new InMemoryRequestHistoryRepository(), new MessageBus());

        public ObservableCollection<Collection> Collections { get; } = [];
        public Collection? SelectedCollection { get; set; }
        public ICommand LoadCollectionRequestCommand => NoOp;
        public ICommand AddRequestToCollectionCommand => NoOp;
        public ICommand ImportCollectionCommand => NoOp;
        public ICommand DeleteCollectionCommand => NoOp;

        public bool IsNewCollectionFormVisible => false;
        public string NewCollectionName { get; set; } = string.Empty;
        public ICommand ShowNewCollectionFormCommand => NoOp;
        public ICommand CreateCollectionCommand => NoOp;
        public ICommand CancelNewCollectionCommand => NoOp;
        public bool IsRenameCollectionFormVisible => false;
        public string RenameCollectionName { get; set; } = string.Empty;
        public ICommand ShowRenameCollectionFormCommand => NoOp;
        public ICommand ConfirmRenameCollectionCommand => NoOp;
        public ICommand CancelRenameCollectionCommand => NoOp;

        public ObservableCollection<RequestHeaderViewModel> CollectionInheritedHeaders { get; } = [];
        public ICommand AddCollectionInheritedHeaderCommand => NoOp;
        public ICommand RemoveCollectionInheritedHeaderCommand => NoOp;

        public string CollectionSearchQuery { get; set; } = string.Empty;
        public string CollectionSortBy => "Default";
        public string CollectionDisplayMode => "NameAndPath";
        public bool IsCollectionTreeView => false;
        public ObservableCollection<CollectionItemViewModel> FilteredCollectionItems { get; } = [];
        public ObservableCollection<CollectionGroupViewModel> CollectionGroups { get; } = [];
        public ICommand SetCollectionSortByCommand => NoOp;
        public ICommand SetCollectionDisplayModeCommand => NoOp;
        public ICommand ToggleCollectionTreeViewCommand => NoOp;

        public ScheduledJobsPanelViewModel ScheduledJobsPanel { get; } = new(
            new InMemoryScheduledJobRepository(),
            new ScheduledJobService(
                new HttpRequestService(new System.Net.Http.HttpClient(), new InMemoryRequestHistoryRepository()),
                new LoggerConfiguration().CreateLogger()),
            defaultIntervalSeconds: () => 60,
            followRedirects: () => false,
            autoStartOnLaunch: () => false);

        public RequestEditorViewModel RequestEditor => null!;
        public ResponseActionsViewModel ResponseActions => null!;
    }

    /// <summary>
    /// Minimal <see cref="IRequestPanelContext"/> stub. Only the editor-command proxies the
    /// dockable VM exposes in C# are asserted; the rest of the (large) surface is AXAML-bound and
    /// exercised by the rendering screenshot tests, so it returns inert defaults.
    /// </summary>
    private sealed class FakeRequestPanelContext(RequestEditorViewModel requestEditor) : IRequestPanelContext
    {
        private static readonly ICommand NoOp = ReactiveCommand.Create(() => { });

#pragma warning disable CS0067 // event is part of the contract but never raised by this stub
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public IReadOnlyList<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; } = [];

        public ObservableCollection<RequestTabViewModel> RequestTabs { get; } = [];
        public RequestTabViewModel? ActiveRequestTab { get; set; }
        public ICommand NewRequestTabCommand => NoOp;
        public ICommand CloseRequestTabCommand => NoOp;

        public RequestEditorViewModel RequestEditor { get; } = requestEditor;
        public GraphQlViewModel GraphQlEditor => null!;
        public WebSocketViewModel WebSocketSession => null!;
        public SseViewModel SseSession => null!;
        public ScriptViewModel ScriptEditor => null!;

        public string PrimaryActionLabel => "Send";
        public string ErrorMessage => string.Empty;
        public string RequestTimeoutDefaultWatermark => string.Empty;
        public ICommand ExecutePrimaryActionCommand => NoOp;
        public ICommand OpenRequestBodyInExternalEditorCommand => NoOp;

        public bool IsDemoServerBannerVisible => false;
        public ICommand StartDemoServerCommand => NoOp;
        public ICommand DismissDemoServerBannerCommand => NoOp;

        public bool IsRequestInProgress => false;

        public string ResponseStatus => string.Empty;
        public int ResponseStatusCode => 0;
        public string ResponseTimeDisplay => string.Empty;
        public string ResponseSizeDisplay => string.Empty;
        public string ResponseBody => string.Empty;
        public string RawResponseBody => string.Empty;
        public string ResponseRawText => string.Empty;
        public string ResponseContentType => string.Empty;
        public string ResponseBodyTabLabel => "Body";
        public int SelectedResponseTabIndex { get; set; }
        public bool IsResponseWebViewAvailable => false;
        public string ResponseWebViewUri => "about:blank";
        public bool IsBinaryResponse => false;
        public bool HasResponseHeaders => false;
        public bool HasTextResponse => false;
        public ObservableCollection<string> ResponseHeaders { get; } = [];
        public ResponseActionsViewModel ResponseActions => null!;

        public string UiFontFamily => "Cascadia Code";
        public double UiFontSize => 13d;
    }
}
