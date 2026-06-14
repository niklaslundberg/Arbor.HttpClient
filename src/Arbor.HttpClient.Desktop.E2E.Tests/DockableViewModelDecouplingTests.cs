using System.Collections.ObjectModel;
using System.Windows.Input;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using ReactiveUI;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Verifies that the dockable view models no longer depend on the whole
/// <c>MainWindowViewModel</c>: <see cref="LogPanelViewModel"/> takes the log feature VM
/// directly, and <see cref="LayoutManagementViewModel"/> takes the narrow
/// <see cref="ILayoutManagementContext"/> contract.
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
    public void LayoutManagementViewModel_ProxiesCommandsFromContext()
    {
        var context = new FakeLayoutManagementContext();

        var dockable = new LayoutManagementViewModel(context);

        dockable.App.Should().BeSameAs(context);
        dockable.Id.Should().Be("layout-management");
        dockable.Title.Should().Be("Layout");
        dockable.SaveLayoutAsNewCommand.Should().BeSameAs(context.SaveLayoutAsNewCommand);
        dockable.SaveLayoutToExistingCommand.Should().BeSameAs(context.SaveLayoutToExistingCommand);
        dockable.RemoveLayoutCommand.Should().BeSameAs(context.RemoveLayoutCommand);
        dockable.RestoreDefaultLayoutCommand.Should().BeSameAs(context.RestoreDefaultLayoutCommand);
    }

    [Fact]
    public void LeftPanelViewModel_ExposesContextAndProxiesRemoveScheduledJobCommand()
    {
        var context = new FakeLeftPanelContext();

        var dockable = new LeftPanelViewModel(context);

        dockable.App.Should().BeSameAs(context);
        dockable.Id.Should().Be("left-panel");
        dockable.Title.Should().Be("Explorer");
        dockable.RemoveScheduledJobCommand.Should().BeSameAs(context.RemoveScheduledJobCommand);
    }

    private sealed class FakeLayoutManagementContext : ILayoutManagementContext
    {
        public ObservableCollection<string> SavedLayoutNames { get; } = [];

        public string? SelectedLayoutName { get; set; }

        public ICommand SaveLayoutAsNewCommand { get; } = ReactiveCommand.Create(() => { });

        public ICommand SaveLayoutToExistingCommand { get; } = ReactiveCommand.Create<string?>(_ => { });

        public ICommand RemoveLayoutCommand { get; } = ReactiveCommand.Create<string?>(_ => { });

        public ICommand RestoreDefaultLayoutCommand { get; } = ReactiveCommand.Create(() => { });
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

        public string HistorySearchQuery { get; set; } = string.Empty;
        public ObservableCollection<RequestHistoryEntry> History { get; } = [];
        public ICommand LoadHistoryRequestCommand => NoOp;

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

        public ObservableCollection<ScheduledJobViewModel> ScheduledJobs { get; } = [];
        public ICommand AddScheduledJobCommand => NoOp;
        public ICommand RemoveScheduledJobCommand { get; } = ReactiveCommand.Create<ScheduledJobViewModel?>(_ => { });

        public RequestEditorViewModel RequestEditor => null!;
        public ResponseActionsViewModel ResponseActions => null!;
    }
}
