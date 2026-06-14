using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Variables;
using Arbor.HttpClient.Desktop.Localization;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class MainWindowUiTests
{
    private static async Task WaitForUiThreadAsync() =>
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

    [AvaloniaFact(Timeout = 10_000)]
    public async Task MainWindowViewModel_ShouldRestoreImplicitLayoutFromInitialOptions()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                Layouts = new LayoutOptions
                {
                    CurrentLayout = new DockLayoutSnapshot
                    {
                        LeftToolProportion = 0.4,
                        DocumentProportion = 0.6,
                        ActiveToolDockableId = "options",
                        LeftToolDockableOrder = ["options", "left-panel"]
                    }
                }
            });

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        var documentLayout = FindDockById<ProportionalDock>(viewModel.Layout!, "document-layout");

        leftToolDock.Should().NotBeNull();
        documentLayout.Should().NotBeNull();
        leftToolDock.Proportion.Should().BeApproximately(0.4, 0.0001);
        documentLayout.Proportion.Should().BeApproximately(0.6, 0.0001);
        leftToolDock.ActiveDockable?.Id.Should().Be("options");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task MainWindowViewModel_ShouldSaveRestoreAndRemoveNamedLayouts()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        var documentLayout = FindDockById<ProportionalDock>(viewModel.Layout!, "document-layout");
        leftToolDock.Should().NotBeNull();
        documentLayout.Should().NotBeNull();

        leftToolDock.Proportion = 0.35;
        documentLayout.Proportion = 0.65;
        leftToolDock.ActiveDockable = leftToolDock.VisibleDockables!.First(d => d.Id == "options");

        viewModel.SaveLayoutAsNewCommand.Execute().Subscribe();
        viewModel.SavedLayoutNames.Should().ContainSingle();
        var layoutName = viewModel.SavedLayoutNames.Single();

        leftToolDock.Proportion = 0.2;
        documentLayout.Proportion = 0.8;
        leftToolDock.ActiveDockable = leftToolDock.VisibleDockables!.First(d => d.Id == "left-panel");

        // Selection was already layoutName after save; clear it first so the re-selection triggers restore
        viewModel.SelectedLayoutName = null;
        viewModel.SelectedLayoutName = layoutName;

        leftToolDock.Proportion.Should().BeApproximately(0.35, 0.0001);
        documentLayout.Proportion.Should().BeApproximately(0.65, 0.0001);
        leftToolDock.ActiveDockable?.Id.Should().Be("options");

        viewModel.RemoveLayoutCommand.Execute(layoutName).Subscribe();
        viewModel.SavedLayoutNames.Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Layout_DefaultSplitView_ShouldShowRequestAboveResponse()
    {
        // Verifies the default layout keeps a single request dock; response is integrated in request view.

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var documentLayout = FindDockById<ProportionalDock>(viewModel.Layout!, "document-layout");
        var requestDock = FindDockById<DocumentDock>(viewModel.Layout!, "request-dock");
        var responseDock = FindDockById<DocumentDock>(viewModel.Layout!, "response-dock");

        documentLayout.Should().NotBeNull("document-layout must exist in the default dock layout");
        requestDock.Should().NotBeNull("request-dock must exist in the default dock layout");
        responseDock.Should().BeNull("response is integrated into request and should not exist as separate dock");

        documentLayout.VisibleDockables!.Should().ContainSingle(dockable => dockable.Id == "request-dock");
        requestDock!.Proportion.Should().BeGreaterThan(0, "request dock must have a positive proportion");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Layout_SplitViewProportions_ShouldPersistAcrossRestarts()
    {
        // Verifies that the request/response split proportions are saved and restored

        // ── First "application run" ──────────────────────────────────────
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var requestDock = FindDockById<DocumentDock>(viewModel.Layout!, "request-dock");
        var responseDock = FindDockById<DocumentDock>(viewModel.Layout!, "response-dock");
        requestDock.Should().NotBeNull();
        responseDock.Should().BeNull("response is integrated into request and should not exist as separate dock");

        // Simulate user resizing the remaining document dock.
        requestDock.Proportion = 0.3;

        // Simulate OnClosing: persist then capture
        var savedLayout = viewModel.CaptureCurrentLayout();

        // ── Second "application run" ─────────────────────────────────────
        var handler2 = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService2 = new HttpRequestService(new System.Net.Http.HttpClient(handler2), repository);
        var inMemorySink2 = new InMemorySink();
        var logger2 = new LoggerConfiguration().WriteTo.Sink(inMemorySink2).CreateLogger();
        var scheduledJobService2 = new ScheduledJobService(httpRequestService2, logger2);
        var logWindowViewModel2 = new LogWindowViewModel(inMemorySink2);

        using var viewModel2 = new MainWindowViewModel(
            httpRequestService2,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService2,
            logWindowViewModel2,
            initialOptions: new ApplicationOptions { Layouts = savedLayout });

        var requestDock2 = FindDockById<DocumentDock>(viewModel2.Layout!, "request-dock");
        var responseDock2 = FindDockById<DocumentDock>(viewModel2.Layout!, "response-dock");
        requestDock2.Should().NotBeNull();
        responseDock2.Should().BeNull("response is integrated into request and should not be restored as separate dock");
        requestDock2!.Proportion.Should().BeApproximately(0.3, 0.001, "request dock proportion should be restored");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Layout_DockTree_InPlace_ShouldRestoreProportionsFromTree()
    {
        // Verifies that when the saved DockTree has the same structure as the current layout
        // the in-place update path is taken (no rebuild) and proportions are restored.

        // ── First "application run" ──────────────────────────────────────
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var requestDock = FindDockById<DocumentDock>(viewModel.Layout!, "request-dock");
        var responseDock = FindDockById<DocumentDock>(viewModel.Layout!, "response-dock");
        requestDock.Should().NotBeNull();
        responseDock.Should().BeNull("response is integrated into request and should not exist as separate dock");

        // Simulate user resizing the request dock in-place.
        requestDock!.Proportion = 0.3;

        // Simulate OnClosing: persist then capture
        var savedLayout = viewModel.CaptureCurrentLayout();

        // ── Second "application run" ─────────────────────────────────────
        var handler2 = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient2 = new System.Net.Http.HttpClient(handler2);
        var httpRequestService2 = new HttpRequestService(httpClient2, repository);
        using var viewModel2 = new MainWindowViewModel(
            httpRequestService2,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            new ScheduledJobService(httpRequestService2, new LoggerConfiguration().CreateLogger()),
            new LogWindowViewModel(new InMemorySink()),
            initialOptions: new ApplicationOptions { Layouts = savedLayout });

        var requestDock2 = FindDockById<DocumentDock>(viewModel2.Layout!, "request-dock");
        var responseDock2 = FindDockById<DocumentDock>(viewModel2.Layout!, "response-dock");
        requestDock2.Should().NotBeNull();
        responseDock2.Should().BeNull("response is integrated into request and should not be restored as separate dock");
        requestDock2!.Proportion.Should().BeApproximately(0.3, 0.001,
            "request dock proportion should be restored in-place from DockTree");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Layout_DockTree_ShouldRebuildWhenToolDockIdDiffers()
    {
        // Verifies the full-rebuild path: when the saved DockTree contains a ToolDock whose
        // ID is not present in the current layout (e.g. user docked a panel to a new position
        // creating a dock with a new ID), the layout is fully rebuilt from the saved tree and
        // all content dockables (like "log-panel") remain accessible in the result.

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);

        // Construct a DockTree snapshot where the left-side ToolDock has a non-default ID.
        // "custom-left-dock" is not present in the default layout, so DockTreeRequiresRebuild
        // returns true and a full tree rebuild is performed.
        var changedTree = new DockTreeNode
        {
            Type = "Root",
            Id = "root",
            Children =
            [
                new DockTreeNode
                    {
                        Type = "Proportional",
                        Id = "main-layout",
                        Orientation = "Horizontal",
                        Children =
                        [
                            new DockTreeNode
                            {
                                Type = "Tool",
                                Id = "custom-left-dock",   // <-- different from default "left-tool-dock"
                                Proportion = 0.25,
                                Alignment = "Left",
                                GripMode = "Visible",
                                ContentIds = ["left-panel", "options", "environments", "log-panel", "cookie-jar", "layout-management"],
                                ActiveContentId = "log-panel"
                            },
                            new DockTreeNode { Type = "Splitter", Id = "main-splitter" },
                            new DockTreeNode
                            {
                                Type = "Proportional",
                                Id = "document-layout",
                                Proportion = 0.75,
                                Orientation = "Vertical",
                                Children =
                                [
                                    new DockTreeNode
                                    {
                                        Type = "Document",
                                        Id = "request-dock",
                                        Proportion = 1,
                                        ContentIds = ["request"]
                                    }
                                ]
                            }
                        ]
                    }
            ]
        };

        var savedLayout = new LayoutOptions
        {
            CurrentLayout = new DockLayoutSnapshot
            {
                LeftToolProportion = 0.25,
                DocumentProportion = 0.75,
                DockTree = changedTree
            },
            SavedLayouts = []
        };

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            new ScheduledJobService(httpRequestService, new LoggerConfiguration().CreateLogger()),
            new LogWindowViewModel(new InMemorySink()),
            initialOptions: new ApplicationOptions { Layouts = savedLayout });

        // After the rebuild, "left-tool-dock" (the default ID) should not exist because
        // the layout was rebuilt from the custom tree that used "custom-left-dock".
        var defaultToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        var customToolDock = FindDockById<ToolDock>(viewModel.Layout!, "custom-left-dock");
        var logPanel = FindDockById<IDockable>(viewModel.Layout!, "log-panel");

        defaultToolDock.Should().BeNull("old 'left-tool-dock' should not exist after rebuild with custom tree");
        customToolDock.Should().NotBeNull("rebuilt layout should contain 'custom-left-dock' from saved tree");
        logPanel.Should().NotBeNull("log-panel content dockable must be accessible after rebuild");
        logPanel.Owner.Should().BeSameAs(customToolDock,
            "log-panel should be owned by the rebuilt tool dock");
    }


    [AvaloniaFact(Timeout = 10_000)]
    public async Task OpenLogWindowCommand_ShouldActivateDockableLogPanel()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();
        leftToolDock.VisibleDockables!.Should().Contain(d => d.Id == "log-panel");

        viewModel.OpenLogWindowCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("log-panel");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OpenEnvironmentsCommand_ShouldActivateDockableEnvironmentsPanel()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();
        leftToolDock.VisibleDockables!.Should().Contain(d => d.Id == "environments");

        viewModel.OpenEnvironmentsCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("environments");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowCollectionsTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();

        // Switch to Environments first so the left panel is no longer active
        viewModel.OpenEnvironmentsCommand.Execute().Subscribe();
        leftToolDock.ActiveDockable?.Id.Should().Be("environments");

        // Now click Collections — should switch back to the Explorer (left-panel) dock
        viewModel.ShowCollectionsTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("Collections");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowHistoryTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();

        // Switch to Environments first so the left panel is no longer active
        viewModel.OpenEnvironmentsCommand.Execute().Subscribe();
        leftToolDock.ActiveDockable?.Id.Should().Be("environments");

        // Now click History — should switch back to the Explorer (left-panel) dock
        viewModel.ShowHistoryTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("History");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowScheduledJobsTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();

        // Switch to Environments first so the left panel is no longer active
        viewModel.OpenEnvironmentsCommand.Execute().Subscribe();
        leftToolDock.ActiveDockable?.Id.Should().Be("environments");

        // Now click Scheduled Jobs — should switch back to the Explorer (left-panel) dock
        viewModel.ShowScheduledJobsTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("ScheduledJobs");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OpenLayoutPanelCommand_ShouldActivateDockableLayoutPanel()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();
        leftToolDock.VisibleDockables!.Should().Contain(d => d.Id == "layout-management");

        viewModel.OpenLayoutPanelCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("layout-management");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task InitializeAsync_ShouldNotAutoStartScheduledJobs_WhenApplicationOptionIsDisabled()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var scheduledJobRepository = new InMemoryScheduledJobRepository();
        var jobId = await scheduledJobRepository.SaveAsync(new ScheduledJobConfig(
            0,
            "Job 1",
            "GET",
            "http://localhost:5000/job",
            null,
            null,
            60,
            true));

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            scheduledJobRepository,
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                ScheduledJobs = new ScheduledJobsOptions
                {
                    AutoStartOnLaunch = false
                }
            });

        await viewModel.InitializeAsync();

        viewModel.ScheduledJobs.Should().HaveCount(1);
        viewModel.ScheduledJobs.Single().AutoStart.Should().BeTrue();
        viewModel.ScheduledJobs.Single().IsRunning.Should().BeFalse();
        scheduledJobService.IsRunning(jobId).Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendButton_ShouldUpdateResponseStatusAndBody()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                ReasonPhrase = "Accepted",
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            });

        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "UI Test";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        viewModel.RequestEditor.SelectedMethod = "GET";

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        // Allow Dock to render its panel contents
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

        // The Send button is now inside RequestView which is rendered by the DockControl.
        // Dock uses deferred content materialization, so visual-tree traversal may not find
        // the button during headless tests. Execute the command via the ViewModel directly,
        // which is what the button's Command binding ultimately calls.
        viewModel.Layout.Should().NotBeNull("dock layout should be initialized");
        await viewModel.SendRequestCommand.Execute();

        viewModel.ResponseStatus.Should().Be("202 Accepted");
        viewModel.ResponseBody.Should().Contain("ok");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
        var screenshotPath = Path.Join(Path.GetTempPath(), "arbor-httpclient-ui.png");
        screenshot?.Save(screenshotPath);

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ExecutePrimaryAction_ShouldCancelInFlightManualRequest()
    {

        var repository = new InMemoryRequestHistoryRepository();
        using var handler = new AsyncStubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("completed")
            };
        });
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "Cancel test";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/slow";
        viewModel.RequestEditor.SelectedMethod = "GET";
        viewModel.PrimaryActionLabel.Should().Be("Send");

        var sendCompleted = viewModel.SendRequestCommand.IsExecuting
            .SkipWhile(executing => !executing)
            .Where(executing => !executing)
            .FirstAsync()
            .ToTask();

        viewModel.ExecutePrimaryActionCommand.Execute().Subscribe();
        await Task.Delay(30, TestContext.Current.CancellationToken);
        viewModel.IsRequestInProgress.Should().BeTrue();
        viewModel.PrimaryActionLabel.Should().Be("Cancel");

        viewModel.ExecutePrimaryActionCommand.Execute().Subscribe();
        await sendCompleted;

        viewModel.PrimaryActionLabel.Should().Be("Send");
        viewModel.ErrorMessage.Should().Be("Request cancelled.");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ExecutePrimaryAction_ShouldShowTimeoutMessage_WhenManualRequestTimesOut()
    {

        var repository = new InMemoryRequestHistoryRepository();
        using var handler = new AsyncStubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("completed")
            };
        });
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        // Set timeout AFTER constructor — the constructor resets it to DefaultRequestTimeoutSeconds (100s).
        httpRequestService.SetDefaultRequestTimeout(TimeSpan.FromMilliseconds(50));

        viewModel.RequestEditor.RequestName = "Timeout test";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/slow";
        viewModel.RequestEditor.SelectedMethod = "GET";

        var sendCompleted = viewModel.SendRequestCommand.IsExecuting
            .SkipWhile(executing => !executing)
            .Where(executing => !executing)
            .FirstAsync()
            .ToTask();

        viewModel.ExecutePrimaryActionCommand.Execute().Subscribe();
        await sendCompleted;

        viewModel.ErrorMessage.Should().Be("Request timed out.");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenStartingNewRequest_ClearsPreviousResponseState()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = new AsyncStubHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestStarted.TrySetResult();
            await allowResponse.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.ResponseBody = "{\"previous\":true}";
        viewModel.RawResponseBody = "{\"previous\":true}";
        viewModel.ResponseStatus = "200 OK";
        viewModel.ResponseHeaders.Add("Content-Type: application/json");
        viewModel.HasResponseHeaders = true;
        viewModel.HasTextResponse = true;

        viewModel.RequestEditor.RequestName = "clear response state";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/slow";
        viewModel.RequestEditor.SelectedMethod = "GET";

        var sendTask = viewModel.SendRequestCommand.Execute().ToTask();
        await requestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(30, TestContext.Current.CancellationToken);

        viewModel.IsRequestInProgress.Should().BeTrue();
        viewModel.ResponseBody.Should().BeEmpty();
        viewModel.RawResponseBody.Should().BeEmpty();
        viewModel.ResponseStatus.Should().BeEmpty();
        viewModel.ResponseHeaders.Should().BeEmpty();
        viewModel.HasTextResponse.Should().BeFalse();

        allowResponse.TrySetResult();
        await sendTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestTabs_ShouldRestorePerTabResponseStateWhenSwitchingTabs()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var responseIndex = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            return Interlocked.Increment(ref responseIndex) switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"tab\":1}", Encoding.UTF8, "application/json")
                },
                2 => new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("<item><tab>2</tab></item>", Encoding.UTF8, "application/xml")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });

        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.ActiveRequestTab.Should().NotBeNull();
        var firstTab = viewModel.ActiveRequestTab!;
        viewModel.RequestEditor.Should().BeSameAs(firstTab.RequestEditor);
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/json";
        viewModel.RequestEditor.SelectedMethod = "GET";

        await viewModel.SendRequestCommand.Execute();
        viewModel.ResponseBody.Should().Contain("\"tab\": 1");
        viewModel.SelectedResponseTabIndex = 2;

        viewModel.NewRequestTabCommand.Execute().Subscribe();
        await WaitForUiThreadAsync();
        viewModel.ActiveRequestTab.Should().NotBeNull();
        var secondTab = viewModel.ActiveRequestTab!;
        secondTab.Should().NotBeSameAs(firstTab);
        viewModel.RequestEditor.Should().BeSameAs(secondTab.RequestEditor);
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/xml";
        viewModel.RequestEditor.SelectedMethod = "GET";

        await viewModel.SendRequestCommand.Execute();
        viewModel.ResponseBody.Should().Contain("<tab>2</tab>");
        viewModel.SelectedResponseTabIndex = 3;

        viewModel.ActiveRequestTab = firstTab;
        await WaitForUiThreadAsync();
        viewModel.RequestEditor.Should().BeSameAs(firstTab.RequestEditor);
        viewModel.ResponseBody.Should().Contain("\"tab\": 1");
        viewModel.ResponseBodyTabLabel.Should().Be("JSON");
        viewModel.ResponseStatus.Should().Be("200 OK");
        viewModel.ResponseHeaders.Should().Contain(header => header == "Content-Type: application/json; charset=utf-8");
        viewModel.SelectedResponseTabIndex.Should().Be(2);

        viewModel.ActiveRequestTab = secondTab;
        await WaitForUiThreadAsync();
        viewModel.RequestEditor.Should().BeSameAs(secondTab.RequestEditor);
        viewModel.ResponseBody.Should().Contain("<tab>2</tab>");
        viewModel.ResponseBodyTabLabel.Should().Be("XML");
        viewModel.ResponseStatus.Should().Be("202 Accepted");
        viewModel.ResponseHeaders.Should().Contain(header => header == "Content-Type: application/xml; charset=utf-8");
        viewModel.SelectedResponseTabIndex.Should().Be(3);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadHistoryRequestCommand_WithHistoryEntry_LoadsRequestIntoEditor()
    {
        var repository = new InMemoryRequestHistoryRepository();
        await repository.SaveAsync(new RequestHistoryEntry(
            Name: "History entry",
            Method: "POST",
            Url: "http://localhost:5000/api/items",
            Body: "{\"name\":\"item\"}",
            CreatedAtUtc: DateTimeOffset.UtcNow));

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        var historyItem = viewModel.History.Should().ContainSingle().Subject;
        viewModel.LoadHistoryRequestCommand.Execute(historyItem).Subscribe();

        viewModel.RequestEditor.SelectedMethod.Should().Be("POST");
        viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/api/items");
        viewModel.RequestEditor.RequestBody.Should().Be("{\"name\":\"item\"}");
        viewModel.RequestEditor.RequestName.Should().Be("History entry");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenNoCollectionSelected_SavesRequestToImplicitCollection()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Implicit Save";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit";
        viewModel.RequestEditor.SelectedMethod = "GET";
        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.FirstOrDefault(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));

        implicitCollection.Should().NotBeNull();
        implicitCollection!.Requests.Should().ContainSingle(request =>
            string.Equals(request.Name, "Implicit Save", StringComparison.Ordinal)
            && string.Equals(request.Method, "GET", StringComparison.Ordinal)
            && string.Equals(request.Path, "http://localhost:5000/implicit", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenRequestHasSensitiveHeaders_DoesNotPersistSensitiveHeadersInImplicitCollection()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Sensitive Header Save";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/secure";
        viewModel.RequestEditor.SelectedMethod = "GET";
        viewModel.RequestEditor.RequestHeaders.Clear();
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "Authorization",
            Value = "******",
            IsEnabled = true
        });
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "X-Correlation-Id",
            Value = "abc-123",
            IsEnabled = true
        });
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "X-Session-Token",
            Value = "token-value",
            IsEnabled = true
        });
        viewModel.RequestEditor.EnsurePlaceholderRows();

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "Sensitive Header Save", StringComparison.Ordinal));

        collectionRequest.Headers.Should().ContainSingle(header =>
            string.Equals(header.Name, "X-Correlation-Id", StringComparison.Ordinal)
            && string.Equals(header.Value, "abc-123", StringComparison.Ordinal));
        collectionRequest.Headers.Should().NotContain(header =>
            string.Equals(header.Name, "Authorization", StringComparison.OrdinalIgnoreCase));
        collectionRequest.Headers.Should().NotContain(header =>
            string.Equals(header.Name, "X-Session-Token", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendGraphQlRequestAsync_SavesActualGraphQlPayloadInImplicitCollection()
    {
        await using var demoServer = new DemoServer();
        await demoServer.StartAsync();

        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "GraphQL implicit save";
        viewModel.RequestEditor.RequestUrl = $"http://localhost:{demoServer.Port}/echo";
        viewModel.RequestEditor.SelectedRequestType = RequestType.GraphQL;
        viewModel.GraphQlEditor.Query = "query Ping { ping }";
        viewModel.GraphQlEditor.VariablesJson = "{\"userId\":42}";
        viewModel.GraphQlEditor.OperationName = "Ping";

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "GraphQL implicit save", StringComparison.Ordinal));

        collectionRequest.Method.Should().Be("POST");
        collectionRequest.ContentType.Should().Be("application/json");

        using var bodyDocument = JsonDocument.Parse(collectionRequest.Body!);
        bodyDocument.RootElement.GetProperty("query").GetString().Should().Be("query Ping { ping }");
        bodyDocument.RootElement.GetProperty("operationName").GetString().Should().Be("Ping");
        bodyDocument.RootElement.GetProperty("variables").GetProperty("userId").GetInt32().Should().Be(42);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendGraphQlRequestAsync_WithInvalidVariablesJson_SavesBodyWithNullVariables()
    {
        await using var demoServer = new DemoServer();
        await demoServer.StartAsync();

        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "GraphQL invalid vars";
        viewModel.RequestEditor.RequestUrl = $"http://localhost:{demoServer.Port}/echo";
        viewModel.RequestEditor.SelectedRequestType = RequestType.GraphQL;
        viewModel.GraphQlEditor.Query = "query Ping { ping }";
        viewModel.GraphQlEditor.VariablesJson = "{bad-json";
        viewModel.GraphQlEditor.OperationName = "Ping";

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "GraphQL invalid vars", StringComparison.Ordinal));

        using var bodyDocument = JsonDocument.Parse(collectionRequest.Body!);
        bodyDocument.RootElement.GetProperty("variables").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenImplicitCollectionIsSelected_RefreshesSelectedCollectionItems()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Initial implicit request";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit/one";
        viewModel.RequestEditor.SelectedMethod = "GET";
        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        viewModel.SelectedCollection = implicitCollection;

        viewModel.RequestEditor.RequestName = "Second implicit request";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit/two";
        await viewModel.SendRequestCommand.Execute();

        viewModel.CollectionItems.Should().Contain(item =>
            string.Equals(item.Name, "Second implicit request", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SetCollectionSortByCommand_InTreeView_ChangesGroupOrder()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        await collectionRepository.SaveAsync(
            "Sort Test",
            sourcePath: null,
            baseUrl: null,
            requests:
            [
                new CollectionRequest("Zulu", "GET", "/a/one", null),
                new CollectionRequest("Alpha", "GET", "/z/two", null)
            ]);

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();
        viewModel.SelectedCollection = viewModel.Collections.First(collection => collection.Name == "Sort Test");
        viewModel.IsCollectionTreeView = true;

        viewModel.CollectionGroups.Select(group => group.GroupKey).Should().Equal("a", "z");

        viewModel.SetCollectionSortByCommand.Execute("Name").Subscribe();

        viewModel.CollectionGroups.Select(group => group.GroupKey).Should().Equal("z", "a");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadEnvironmentsAsync_WhenActiveEnvironmentWasRemoved_SelectsFirstAvailableEnvironment()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var developmentId = await environmentRepository.SaveAsync("Development", [new EnvironmentVariable("key", "dev")]);
        var productionId = await environmentRepository.SaveAsync("Production", [new EnvironmentVariable("key", "prod")]);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            environmentRepository,
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();
        viewModel.ActiveEnvironment = viewModel.Environments.First(environment => environment.Id == productionId);

        await environmentRepository.DeleteAsync(productionId);
        await viewModel.EnvironmentsPanel.LoadEnvironmentsAsync();

        viewModel.ActiveEnvironment.Should().NotBeNull();
        viewModel.ActiveEnvironment!.Id.Should().Be(developmentId);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestUrlAndQueryParameters_ShouldStayInSync_AndPreserveFragment()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/items?first=1&second=2#keep";
        viewModel.RequestEditor.RequestQueryParameters.Should().HaveCount(3);
        viewModel.RequestEditor.RequestQueryParameters[0].Key.Should().Be("first");
        viewModel.RequestEditor.RequestQueryParameters[0].Value.Should().Be("1");
        viewModel.RequestEditor.RequestQueryParameters[1].Key.Should().Be("second");
        viewModel.RequestEditor.RequestQueryParameters[1].Value.Should().Be("2");

        viewModel.RequestEditor.RequestQueryParameters[0].IsEnabled = false;
        viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/items?second=2#keep");

        viewModel.RequestEditor.AddQueryParameterCommand.Execute().Subscribe();
        var added = viewModel.RequestEditor.RequestQueryParameters[viewModel.RequestEditor.RequestQueryParameters.Count - 1];
        added.Key = "third";
        added.Value = "3";

        viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/items?second=2&third=3#keep");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequest_ShouldBuildInterpretedAndRawResponseViews()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("{\"message\":\"hello\"}", Encoding.UTF8, "application/json")
            });

        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "response test";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/data";
        viewModel.RequestEditor.SelectedMethod = "GET";

        await viewModel.SendRequestCommand.Execute();

        viewModel.ResponseBodyTabLabel.Should().Be("JSON");
        viewModel.ResponseBody.Should().Contain("\n");
        viewModel.RawResponseBody.Should().Be("{\"message\":\"hello\"}");
        viewModel.ResponseRawText.Should().Contain("Content-Type:");
        viewModel.ResponseRawText.Should().Contain("{\"message\":\"hello\"}");
        viewModel.IsBinaryResponse.Should().BeFalse();
        viewModel.IsResponseWebViewAvailable.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequest_WithHtmlResponse_ShouldEnableResponseWebView()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><h1>docs</h1></body></html>", Encoding.UTF8, "text/html")
            });

        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/docs.html";
        await viewModel.SendRequestCommand.Execute();

        viewModel.IsResponseWebViewAvailable.Should().BeTrue();
        viewModel.ResponseWebViewUri.Should().StartWith("data:text/html");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAndPreview_ShouldResolveVariables_InUrlHeadersAndBody()
    {

        Uri? capturedUri = null;
        string? capturedHeaderValue = null;
        string? capturedBody = null;
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            if (request.Headers.TryGetValues("X-Tenant", out var headerValues))
            {
                capturedHeaderValue = headerValues.SingleOrDefault();
            }

            capturedBody = request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "variable resolution test";
        viewModel.RequestEditor.SelectedMethod = "POST";
        viewModel.RequestEditor.RequestUrl = "http://{{host}}/api?{{queryKey}}={{queryValue}}";
        viewModel.RequestEditor.RequestBody = "{\"token\":\"{{token}}\",\"env\":\"{{environment}}\"}";

        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost:5000"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("queryKey", "search"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("queryValue", "term"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerName", "Tenant"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerValue", "blue"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "abc123"));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("environment", "dev"));

        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "X-{{headerName}}",
            Value = "{{headerValue}}",
            IsEnabled = true
        });

        viewModel.RequestEditor.RequestPreview.Should().Contain("POST http://localhost:5000/api?search=term HTTP/");
        viewModel.RequestEditor.RequestPreview.Should().Contain("X-Tenant: blue");
        viewModel.RequestEditor.RequestPreview.Should().Contain("\"token\":\"abc123\"");
        viewModel.RequestEditor.RequestPreview.Should().Contain("\"env\":\"dev\"");

        await viewModel.SendRequestCommand.Execute();

        capturedUri.Should().NotBeNull();
        capturedUri!.AbsoluteUri.Should().Be("http://localhost:5000/api?search=term");
        capturedHeaderValue.Should().Be("blue");
        capturedBody.Should().Be("{\"token\":\"abc123\",\"env\":\"dev\"}");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAndPreview_ShouldApplyAuthHelperAuthorizationHeader()
    {

        string? capturedAuthorization = null;
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Headers.TryGetValues("Authorization", out var headerValues))
            {
                capturedAuthorization = headerValues.SingleOrDefault();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "auth helper test";
        viewModel.RequestEditor.SelectedMethod = "GET";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        viewModel.RequestEditor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        viewModel.RequestEditor.AuthBearerToken = "{{token}}";

        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "Authorization",
            Value = "Bearer old-token",
            IsEnabled = true
        });
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "abc123"));

        viewModel.RequestEditor.RequestPreview.Should().Contain("Authorization: Bearer abc123");
        viewModel.RequestEditor.RequestPreview.Should().NotContain("Bearer old-token");

        await viewModel.SendRequestCommand.Execute();

        capturedAuthorization.Should().Be("Bearer abc123");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestUrlEditor_AutocompleteShouldInsertFilteredEnvironmentVariable()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var mainViewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        mainViewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "abc"));
        mainViewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost"));

        var requestView = new RequestView
        {
            DataContext = new RequestViewModel(mainViewModel)
        };
        var window = new Window { Width = 900, Height = 500, Content = requestView };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var requestUrlEditor = requestView.FindControl<TextEditor>("RequestUrlEditor");
        requestUrlEditor.Should().NotBeNull();
        requestView.RequestUrlEditorForTests.Should().NotBeNull();
        requestUrlEditor.Text = string.Empty;
        requestUrlEditor.CaretOffset = 0;

        requestUrlEditor.TextArea.PerformTextInput("{");
        requestUrlEditor.TextArea.PerformTextInput("{");
        requestUrlEditor.TextArea.PerformTextInput("t");
        requestUrlEditor.TextArea.PerformTextInput("o");

        var controller = requestView.RequestUrlAutoCompleteControllerForTests;
        controller.Should().NotBeNull();
        var completionWindow = controller.CurrentCompletionWindow;
        completionWindow.Should().NotBeNull();
        completionWindow.IsOpen.Should().BeTrue();
        var completionItem = completionWindow.CompletionList.CompletionData.Single(data => data.Text == "token");
        completionItem.Complete(
            requestUrlEditor.TextArea,
            new TextSegment
            {
                StartOffset = completionWindow.StartOffset,
                Length = completionWindow.EndOffset - completionWindow.StartOffset
            },
            EventArgs.Empty);

        requestUrlEditor.Text.Should().Be("{{token}}");
        mainViewModel.RequestEditor.RequestUrl.Should().Be("{{token}}");

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestView_HttpRequest_ShouldShowOptionsAsLastTabInsteadOfExpander()
    {

        var repository = new InMemoryRequestHistoryRepository();
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var mainViewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);
        mainViewModel.RequestEditor.SelectedRequestType = RequestType.Http;

        var requestView = new RequestView
        {
            DataContext = new RequestViewModel(mainViewModel)
        };
        var window = new Window { Width = 900, Height = 500, Content = requestView };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var tabControl = window.GetVisualDescendants()
            .OfType<TabControl>()
            .Single(control => control.Items.OfType<TabItem>().Any(item => string.Equals(item.Header?.ToString(), "Query", StringComparison.Ordinal)));
        var hasRequestOptionsExpander = window.GetVisualDescendants()
            .OfType<Expander>()
            .Any(expander => string.Equals(expander.Header?.ToString(), "Options", StringComparison.Ordinal));

        VerifyTabRealized(tabControl, "Options");
        var tabItems = tabControl.Items.OfType<TabItem>().ToList();
        tabItems[tabItems.Count - 1].Header?.ToString().Should().Be("Options");
        hasRequestOptionsExpander.Should().BeFalse();

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestView_VariableTextBoxes_ShouldDisableAcceptsTab()
    {

        var repository = new InMemoryRequestHistoryRepository();
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, repository);
        var inMemorySink = new InMemorySink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var mainViewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);
        mainViewModel.RequestEditor.SelectedRequestType = RequestType.Http;
        mainViewModel.RequestEditor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;

        var requestView = new RequestView
        {
            DataContext = new RequestViewModel(mainViewModel)
        };
        var window = new Window { Width = 900, Height = 500, Content = requestView };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var tabControl = window.GetVisualDescendants()
            .OfType<TabControl>()
            .Single(control => control.Items.OfType<TabItem>().Any(item => string.Equals(item.Header?.ToString(), "Query", StringComparison.Ordinal)));
        VerifyTabRealized(tabControl, "Query");
        VerifyTabRealized(tabControl, "Headers");
        VerifyTabRealized(tabControl, "Auth");

        var variableTextBox = new VariableTextBox();
        variableTextBox.AcceptsTabForTests.Should().BeFalse();

        window.Close();
    }

    private static void VerifyTabRealized(TabControl tabControl, string tabHeader)
    {
        var tabItems = tabControl.Items.OfType<TabItem>().ToList();
        var tabItem = tabItems.Single(item => string.Equals(item.Header?.ToString(), tabHeader, StringComparison.Ordinal));
        tabItem.IsVisible.Should().BeTrue();
        tabControl.SelectedIndex = tabItems.IndexOf(tabItem);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);
        tabControl.SelectedItem.Should().Be(tabItem);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsView_ShouldDisplayScheduledJobsPage_WithAutoStartAndIntervalOptions()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var optionsVm = new OptionsViewModel(viewModel);
        var window = new Window { Width = 820, Height = 560, DataContext = optionsVm, Content = new OptionsView() };
        window.Show();

        // Navigate to the Scheduled Jobs page via the ViewModel
        optionsVm.SelectedOptionsPage = "ScheduledJobs";

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var scheduledJobsPage = window.GetVisualDescendants().OfType<ScheduledJobsOptionsPageView>().Single();
        scheduledJobsPage.IsVisible.Should().BeTrue("the extracted Scheduled Jobs page should be visible when the Scheduled Jobs options page is selected");

        var textBlocks = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scheduledJobsPage).OfType<TextBlock>().Select(tb => tb.Text).ToList();

        textBlocks
            .Any(t => string.Equals(t, "Default interval for new jobs", StringComparison.Ordinal))
            .Should()
            .BeTrue("default interval label should be on the Scheduled Jobs page");

        var checkBoxes = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scheduledJobsPage).OfType<CheckBox>();
        checkBoxes
            .Any(cb => string.Equals(cb.Content?.ToString(), "Auto-start scheduled jobs on launch", StringComparison.Ordinal))
            .Should()
            .BeTrue("auto-start toggle should be on the Scheduled Jobs page");

        var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
        var screenshotPath = Path.Join(Path.GetTempPath(), "arbor-httpclient-options-view.png");
        screenshot?.Save(screenshotPath);

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsView_ShouldDisplayManageOptionsPage_WithImportAndExportButtons()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var optionsVm = new OptionsViewModel(viewModel);
        var window = new Window { Width = 820, Height = 560, DataContext = optionsVm, Content = new OptionsView() };
        window.Show();

        optionsVm.SelectedOptionsPage = "ManageOptions";
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var manageOptionsPage = window.GetVisualDescendants().OfType<ManageOptionsPageView>().Single();
        manageOptionsPage.IsVisible.Should().BeTrue("the Manage Options page should be visible when selected");

        var buttons = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(manageOptionsPage).OfType<Button>().ToList();
        buttons.Any(button => string.Equals(button.Content?.ToString(), Strings.OptionsImportJson, StringComparison.Ordinal))
            .Should()
            .BeTrue("import button should be present on the Manage Options page");
        buttons.Any(button => string.Equals(button.Content?.ToString(), Strings.OptionsExportJson, StringComparison.Ordinal))
            .Should()
            .BeTrue("export button should be present on the Manage Options page");

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsChanges_ShouldAutoSaveAndLogToDebug()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);
        var optionsPath = Path.Join(Path.GetTempPath(), $"arbor-options-autosave-{Guid.NewGuid():N}.json");
        var optionsStore = new ApplicationOptionsStore(optionsPath);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            logger,
            optionsStore);

        viewModel.SelectedTlsVersionOption = "Tls13";

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var saved = optionsStore.Load();
        saved.Http.TlsVersion.Should().Be("Tls13");
        inMemorySink.GetSnapshot().Should().Contain(entry => entry.Message.Contains("Saved application options", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task EnvironmentEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var environmentRepository = new InMemoryEnvironmentRepository();
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            environmentRepository,
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.NewEnvironmentCommand.Execute(null);
        viewModel.NewEnvironmentName = "myenv";
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost"));

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var all = await environmentRepository.GetAllAsync();
        all.Should().ContainSingle(e => e.Name == "myenv");
        viewModel.ActiveEnvironment.Should().NotBeNull();
        viewModel.ActiveEnvironment!.Name.Should().Be("myenv");
        viewModel.IsEnvironmentPanelVisible.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ScheduledJobEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var scheduledJobRepository = new InMemoryScheduledJobRepository();
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            scheduledJobRepository,
            scheduledJobService,
            logWindowViewModel);

        viewModel.AddScheduledJobCommand.Execute().Subscribe();
        var job = viewModel.ScheduledJobs.Should().ContainSingle().Subject;
        job.Name = "sync job";
        job.Url = "http://localhost:5000/sync";

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var all = await scheduledJobRepository.GetAllAsync();
        all.Should().ContainSingle(config =>
            string.Equals(config.Name, "sync job", StringComparison.Ordinal) &&
            string.Equals(config.Url, "http://localhost:5000/sync", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task FloatingWindow_PositionShouldBeRestoredOnStartup()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        // Create a ViewModel that starts with a floating left-panel at a known position
        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                Layouts = new LayoutOptions
                {
                    CurrentLayout = new DockLayoutSnapshot
                    {
                        LeftToolProportion = 0.25,
                        DocumentProportion = 0.75,
                        LeftToolDockableOrder = ["options"],
                        FloatingWindows =
                        [
                            new FloatingWindowSnapshot
                                {
                                    X = 100,
                                    Y = 200,
                                    Width = 400,
                                    Height = 300,
                                    DockableIds = ["left-panel"],
                                    ActiveDockableId = "left-panel"
                                }
                        ]
                    }
                }
            });

        // The floating window should have been created and positioned
        viewModel.Layout!.Windows.Should().HaveCount(1);
        var floatWin = viewModel.Layout.Windows![0];
        floatWin.X.Should().BeApproximately(100, 0.001);
        floatWin.Y.Should().BeApproximately(200, 0.001);
        floatWin.Width.Should().BeApproximately(400, 0.001);
        floatWin.Height.Should().BeApproximately(300, 0.001);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task FloatingWindow_PositionShouldBeRestoredFromSavedLayout()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        var floatingSnapshot = new DockLayoutSnapshot
        {
            LeftToolProportion = 0.25,
            DocumentProportion = 0.75,
            LeftToolDockableOrder = ["options"],
            FloatingWindows =
            [
                new FloatingWindowSnapshot
                    {
                        X = 250,
                        Y = 350,
                        Width = 500,
                        Height = 400,
                        DockableIds = ["left-panel"],
                        ActiveDockableId = "left-panel"
                    }
            ]
        };

        // Pre-load a named saved layout with floating windows
        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                Layouts = new LayoutOptions
                {
                    SavedLayouts =
                    [
                        new NamedDockLayout { Name = "Floating Layout", Layout = floatingSnapshot }
                    ]
                }
            });

        viewModel.SavedLayoutNames.Should().ContainSingle(n => n == "Floating Layout");
        viewModel.Layout!.Windows?.Count.Should().Be(0, "no windows before selecting the layout");

        // SelectedLayoutName was pre-set during init (with suppress=true), so set to null
        // first to force a change event when we select it again.
        viewModel.SelectedLayoutName = null;
        viewModel.SelectedLayoutName = "Floating Layout";

        viewModel.Layout!.Windows.Should().HaveCount(1);
        var floatWin = viewModel.Layout.Windows![0];
        floatWin.X.Should().BeApproximately(250, 0.001);
        floatWin.Y.Should().BeApproximately(350, 0.001);
        floatWin.Width.Should().BeApproximately(500, 0.001);
        floatWin.Height.Should().BeApproximately(400, 0.001);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task MainWindowViewModel_CloseFloatingWindowsThenDispose_ShouldNotThrow()
    {
        // Regression test for the NullReferenceException that occurred when:
        // 1. Main window closed (OnClosing fires – persist + close floating windows)
        // 2. Dispose called (OnClosed fires via App.axaml.cs window.Closed handler)
        // Previously, Dispose also called PersistCurrentLayout, which triggered
        // CaptureLayoutSnapshot on an already-cleaned-up layout.

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftPanel = FindDockById<IDockable>(viewModel.Layout!, "left-panel");
        leftPanel.Should().NotBeNull();
        viewModel.Factory!.FloatDockable(leftPanel);
        viewModel.Layout!.Windows.Should().HaveCount(1);

        // Simulate the OnClosing handler sequence – must not throw
        var onClosingException = Record.Exception(() =>
        {
            viewModel.PersistCurrentLayout();
            viewModel.CloseFloatingWindows();
        });
        onClosingException.Should().BeNull("OnClosing sequence must not throw");

        viewModel.Layout.Windows.Should().BeEmpty("floating windows removed by CloseFloatingWindows");

        // Simulate the window.Closed handler sequence – must not throw
        var onClosedException = Record.Exception(() => viewModel.Dispose());
        onClosedException.Should().BeNull("Dispose after CloseFloatingWindows must not throw");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequest_ShouldRespectFollowRedirectsOverride()
    {
        using var server = new RedirectTestServer();

        var repository = new InMemoryRequestHistoryRepository();
        using var defaultClient = new System.Net.Http.HttpClient();
        using var followClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true });
        using var noFollowClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        var httpRequestService = new HttpRequestService(defaultClient, repository);
        httpRequestService.SetHttpClientFactory(followRedirects =>
            (followRedirects ?? true) ? followClient : noFollowClient);

        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.RequestEditor.RequestName = "redirect test";
        viewModel.RequestEditor.SelectedMethod = "GET";
        viewModel.RequestEditor.RequestUrl = server.RedirectUrl;

        viewModel.RequestEditor.FollowRedirectsForRequest = false;
        await viewModel.SendRequestCommand.Execute();
        viewModel.ResponseStatus.Should().StartWith("302");

        viewModel.RequestEditor.FollowRedirectsForRequest = true;
        await viewModel.SendRequestCommand.Execute();
        viewModel.ResponseStatus.Should().Be("200 OK");
        viewModel.RawResponseBody.Should().Contain("redirect-complete");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ScheduledJob_ShouldRespectFollowRedirectsOverride()
    {
        using var server = new RedirectTestServer();

        var repository = new InMemoryRequestHistoryRepository();
        using var defaultClient = new System.Net.Http.HttpClient();
        using var followClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true });
        using var noFollowClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        var httpRequestService = new HttpRequestService(defaultClient, repository);
        httpRequestService.SetHttpClientFactory(followRedirects =>
            (followRedirects ?? true) ? followClient : noFollowClient);

        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        using var scheduledJobService = new ScheduledJobService(httpRequestService, logger);

        const int noFollowId = 1;
        scheduledJobService.Start(new ScheduledJobConfig(
            noFollowId,
            "redirect-off",
            "GET",
            server.RedirectUrl,
            null,
            null,
            1,
            AutoStart: false,
            FollowRedirects: false));

        await Task.Delay(1300, TestContext.Current.CancellationToken);
        scheduledJobService.Stop(noFollowId);
        server.FinalRequestCount.Should().Be(0);

        const int followId = 2;
        scheduledJobService.Start(new ScheduledJobConfig(
            followId,
            "redirect-on",
            "GET",
            server.RedirectUrl,
            null,
            null,
            1,
            AutoStart: false,
            FollowRedirects: true));

        await Task.Delay(1300, TestContext.Current.CancellationToken);
        scheduledJobService.Stop(followId);
        server.FinalRequestCount.Should().BeGreaterThan(0);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ReapplyStartupLayout_ShouldRestoreProportions_EvenAfterPspOverwroteModel()
    {
        // Verifies that ReapplyStartupLayout() correctly re-applies saved dock proportions
        // to the dock model.  This is the belt-and-suspenders call made from window.Opened
        // to handle the case where ProportionalStackPanel.AssignProportions runs before the
        // TwoWay binding is established and propagates equal-distribution proportions back to
        // IDockable.Proportion, overwriting the values that ApplyLayoutSnapshot set.

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        const double savedLeftProportion = 0.4;
        const double savedDocProportion = 0.6;
        const double savedRequestProportion = 0.35;
        const double savedResponseProportion = 0.65;
        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                Layouts = new LayoutOptions
                {
                    CurrentLayout = new DockLayoutSnapshot
                    {
                        LeftToolProportion = savedLeftProportion,
                        DocumentProportion = savedDocProportion,
                        RequestDockProportion = savedRequestProportion,
                        ResponseDockProportion = savedResponseProportion,
                    }
                }
            });

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        var documentLayout = FindDockById<ProportionalDock>(viewModel.Layout!, "document-layout");
        var requestDock = FindDockById<DocumentDock>(viewModel.Layout!, "request-dock");
        var responseDock = FindDockById<DocumentDock>(viewModel.Layout!, "response-dock");

        leftToolDock.Should().NotBeNull();
        documentLayout.Should().NotBeNull();
        requestDock.Should().NotBeNull();
        responseDock.Should().BeNull("response is integrated into request and should not be restored as separate dock");

        // Simulate the scenario where the Dock PSP's TwoWay binding has already overwritten
        // the model proportions with equal-distribution values before the first visual render.
        // (This is the root cause: PSP.AssignProportions fires before the binding is set up.)
        leftToolDock.Proportion = 0.5;
        documentLayout.Proportion = 0.5;
        requestDock.Proportion = 0.5;

        // Now call ReapplyStartupLayout — this is what window.Opened does to correct the
        // PSP-corrupted proportions once visual bindings are established.
        viewModel.ReapplyStartupLayout();

        // The model must be restored to the saved values.
        leftToolDock.Proportion.Should().BeApproximately(savedLeftProportion, 0.001,
            "left tool dock proportion should be restored by ReapplyStartupLayout");
        documentLayout.Proportion.Should().BeApproximately(savedDocProportion, 0.001,
            "document layout proportion should be restored by ReapplyStartupLayout");
        requestDock.Proportion.Should().BeApproximately(savedRequestProportion, 0.001,
            "request dock proportion should be restored by ReapplyStartupLayout");

        // A second call must be a no-op (snapshot cleared after first use).
        leftToolDock.Proportion = 0.9;
        viewModel.ReapplyStartupLayout();
        leftToolDock.Proportion.Should().BeApproximately(0.9, 0.001,
            "second call to ReapplyStartupLayout should be a no-op");
    }



    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestUrlEditor_SetWithNewline_NewlineIsStrippedAndViewModelIsUpdated()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var mainViewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var requestView = new RequestView
        {
            DataContext = new RequestViewModel(mainViewModel)
        };
        var window = new Window { Width = 900, Height = 500, Content = requestView };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var requestUrlEditor = requestView.FindControl<TextEditor>("RequestUrlEditor");
        requestUrlEditor.Should().NotBeNull();

        // Simulate pasting a URL that contains an embedded newline (e.g. from clipboard).
        requestUrlEditor.Text = "http://example.com\npath";

        requestUrlEditor.Text.Should().Be("http://example.compath",
            "the URL editor must strip newlines immediately after they are entered");
        mainViewModel.RequestEditor.RequestUrl.Should().Be("http://example.compath",
            "the ViewModel must receive the stripped URL, not the original with newline");

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RequestUrl_SetOnViewModelWithNewline_NewlineIsStrippedWhenSyncedToEditor()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var mainViewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var requestView = new RequestView
        {
            DataContext = new RequestViewModel(mainViewModel)
        };
        var window = new Window { Width = 900, Height = 500, Content = requestView };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var requestUrlEditor = requestView.FindControl<TextEditor>("RequestUrlEditor");
        requestUrlEditor.Should().NotBeNull();

        // Simulate the ViewModel receiving a URL with a newline (e.g. from persisted state).
        mainViewModel.RequestEditor.RequestUrl = "http://example.com\r\npath";

        requestUrlEditor.Text.Should().Be("http://example.compath",
            "the URL editor must strip newlines when syncing from ViewModel");
        mainViewModel.RequestEditor.RequestUrl.Should().Be("http://example.compath",
            "the ViewModel must have the stripped URL after sync");

        window.Close();
    }



    private sealed class RedirectTestServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loopTask;

        public RedirectTestServer()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            BaseUrl = $"http://127.0.0.1:{port}";
            RedirectUrl = $"{BaseUrl}/redirect";
            FinalUrl = $"{BaseUrl}/final";

            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _loopTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public string BaseUrl { get; }
        public string RedirectUrl { get; }
        public string FinalUrl { get; }
        public int FinalRequestCount { get; private set; }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                using var response = context.Response;
                if (context.Request.Url?.AbsolutePath == "/redirect")
                {
                    response.StatusCode = (int)HttpStatusCode.Redirect;
                    response.RedirectLocation = FinalUrl;
                    response.Close();
                    continue;
                }

                if (context.Request.Url?.AbsolutePath == "/final")
                {
                    FinalRequestCount++;
                    var payload = Encoding.UTF8.GetBytes("redirect-complete");
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/plain";
                    response.ContentLength64 = payload.Length;
                    await response.OutputStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    response.Close();
                    continue;
                }

                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
            }
        }

        public void Dispose()
        {
            using var cancellationTokenSource = _cts;
            cancellationTokenSource.Cancel();
            _listener.Stop();
            _listener.Close();
            try
            {
                _loopTask.GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort stop for tests.
            }
        }
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T match && string.Equals(match.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        if (dockable is IDock { VisibleDockables: { } visibleDockables })
        {
            foreach (var child in visibleDockables)
            {
                var result = FindDockById<T>(child, id);
                if (result is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private sealed class AsyncStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
