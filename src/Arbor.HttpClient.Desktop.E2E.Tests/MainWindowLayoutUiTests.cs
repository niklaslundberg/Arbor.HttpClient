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
using static Arbor.HttpClient.Desktop.E2E.Tests.UiTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class MainWindowLayoutUiTests
{
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

        viewModel.LayoutManagement.SaveLayoutAsNewCommand.Execute().Subscribe();
        viewModel.LayoutManagement.SavedLayoutNames.Should().ContainSingle();
        var layoutName = viewModel.LayoutManagement.SavedLayoutNames.Single();

        leftToolDock.Proportion = 0.2;
        documentLayout.Proportion = 0.8;
        leftToolDock.ActiveDockable = leftToolDock.VisibleDockables!.First(d => d.Id == "left-panel");

        // Selection was already layoutName after save; clear it first so the re-selection triggers restore
        viewModel.LayoutManagement.SelectedLayoutName = null;
        viewModel.LayoutManagement.SelectedLayoutName = layoutName;

        leftToolDock.Proportion.Should().BeApproximately(0.35, 0.0001);
        documentLayout.Proportion.Should().BeApproximately(0.65, 0.0001);
        leftToolDock.ActiveDockable?.Id.Should().Be("options");

        viewModel.LayoutManagement.RemoveLayoutCommand.Execute(layoutName).Subscribe();
        viewModel.LayoutManagement.SavedLayoutNames.Should().BeEmpty();
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

        viewModel.LayoutManagement.SavedLayoutNames.Should().ContainSingle(n => n == "Floating Layout");
        viewModel.Layout!.Windows?.Count.Should().Be(0, "no windows before selecting the layout");

        // SelectedLayoutName was pre-set during init (with suppress=true), so set to null
        // first to force a change event when we select it again.
        viewModel.LayoutManagement.SelectedLayoutName = null;
        viewModel.LayoutManagement.SelectedLayoutName = "Floating Layout";

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
}
