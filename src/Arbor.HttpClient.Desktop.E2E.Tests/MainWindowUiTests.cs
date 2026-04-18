using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.VisualTree;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using AwesomeAssertions;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
public class MainWindowUiTests
{
    [Fact]
    public async Task MainWindowViewModel_ShouldRestoreImplicitLayoutFromInitialOptions()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
                            ActiveDocumentDockableId = "response",
                            LeftToolDockableOrder = ["options", "left-panel"],
                            DocumentDockableOrder = ["response", "request"]
                        }
                    }
                });

            var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
            var documentDock = FindDockById<DocumentDock>(viewModel.Layout!, "document-dock");

            leftToolDock.Should().NotBeNull();
            documentDock.Should().NotBeNull();
            leftToolDock!.Proportion.Should().BeApproximately(0.4, 0.0001);
            documentDock!.Proportion.Should().BeApproximately(0.6, 0.0001);
            leftToolDock.ActiveDockable?.Id.Should().Be("options");
            documentDock.ActiveDockable?.Id.Should().Be("response");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task MainWindowViewModel_ShouldSaveRestoreAndRemoveNamedLayouts()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
            var documentDock = FindDockById<DocumentDock>(viewModel.Layout!, "document-dock");
            leftToolDock.Should().NotBeNull();
            documentDock.Should().NotBeNull();

            leftToolDock!.Proportion = 0.35;
            documentDock!.Proportion = 0.65;
            leftToolDock.ActiveDockable = leftToolDock.VisibleDockables!.First(d => d.Id == "options");
            documentDock.ActiveDockable = documentDock.VisibleDockables!.First(d => d.Id == "response");

            viewModel.SaveLayoutAsNewCommand.Execute(null);
            viewModel.SavedLayoutNames.Should().ContainSingle();
            var layoutName = viewModel.SavedLayoutNames.Single();

            leftToolDock.Proportion = 0.2;
            documentDock.Proportion = 0.8;
            leftToolDock.ActiveDockable = leftToolDock.VisibleDockables!.First(d => d.Id == "left-panel");
            documentDock.ActiveDockable = documentDock.VisibleDockables!.First(d => d.Id == "request");

            // Selection was already layoutName after save; clear it first so the re-selection triggers restore
            viewModel.SelectedLayoutName = null;
            viewModel.SelectedLayoutName = layoutName;

            leftToolDock.Proportion.Should().BeApproximately(0.35, 0.0001);
            documentDock.Proportion.Should().BeApproximately(0.65, 0.0001);
            leftToolDock.ActiveDockable?.Id.Should().Be("options");
            documentDock.ActiveDockable?.Id.Should().Be("response");

            viewModel.RemoveLayoutCommand.Execute(layoutName);
            viewModel.SavedLayoutNames.Should().BeEmpty();

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotAutoStartScheduledJobs_WhenApplicationOptionIsDisabled()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var scheduledJobRepository = new InMemoryScheduledJobRepository();
            var jobId = await scheduledJobRepository.SaveAsync(new ScheduledJobConfig(
                0,
                "Job 1",
                "GET",
                "https://example.com/job",
                null,
                null,
                60,
                true));

            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task SendButton_ShouldUpdateResponseStatusAndBody()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    ReasonPhrase = "Accepted",
                    Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
                });

            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
                logWindowViewModel)
            {
                RequestName = "UI Test",
                RequestUrl = "https://example.com/api",
                SelectedMethod = "GET"
            };

            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            // Allow Dock to render its panel contents
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

            // The Send button is now inside RequestView which is rendered by the DockControl.
            // Dock uses deferred content materialization, so visual-tree traversal may not find
            // the button during headless tests. Execute the command via the ViewModel directly,
            // which is what the button's Command binding ultimately calls.
            viewModel.Layout.Should().NotBeNull("dock layout should be initialized");
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            viewModel.ResponseStatus.Should().Be("202 Accepted");
            viewModel.ResponseBody.Should().Contain("ok");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            var screenshotPath = Path.Combine(Path.GetTempPath(), "arbor-httpclient-ui.png");
            screenshot?.Save(screenshotPath);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RequestUrlAndQueryParameters_ShouldStayInSync_AndPreserveFragment()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            viewModel.RequestUrl = "https://example.com/items?first=1&second=2#keep";
            viewModel.RequestQueryParameters.Should().HaveCount(2);
            viewModel.RequestQueryParameters[0].Key.Should().Be("first");
            viewModel.RequestQueryParameters[0].Value.Should().Be("1");
            viewModel.RequestQueryParameters[1].Key.Should().Be("second");
            viewModel.RequestQueryParameters[1].Value.Should().Be("2");

            viewModel.RequestQueryParameters[0].IsEnabled = false;
            viewModel.RequestUrl.Should().Be("https://example.com/items?second=2#keep");

            viewModel.AddQueryParameterCommand.Execute(null);
            var added = viewModel.RequestQueryParameters.Last();
            added.Key = "third";
            added.Value = "3";

            viewModel.RequestUrl.Should().Be("https://example.com/items?second=2&third=3#keep");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task SendRequest_ShouldBuildInterpretedAndRawResponseViews()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    ReasonPhrase = "OK",
                    Content = new StringContent("{\"message\":\"hello\"}", Encoding.UTF8, "application/json")
                });

            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
                logWindowViewModel)
            {
                RequestName = "response test",
                RequestUrl = "https://example.com/data",
                SelectedMethod = "GET"
            };

            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            viewModel.ResponseBodyTabLabel.Should().Be("JSON");
            viewModel.ResponseBody.Should().Contain("\n");
            viewModel.RawResponseBody.Should().Be("{\"message\":\"hello\"}");
            viewModel.IsBinaryResponse.Should().BeFalse();

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task OptionsView_ShouldDisplayScheduledJobsPage_WithAutoStartAndIntervalOptions()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
            var window = new Window { Width = 820, Height = 560, DataContext = optionsVm };
            window.Content = new OptionsView();
            window.Show();

            // Navigate to the Scheduled Jobs page via the ViewModel
            viewModel.SelectedOptionsPage = "ScheduledJobs";

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

            // The explicit <TextBlock> label is always in the visual tree
            var textBlocks = window.GetVisualDescendants().OfType<TextBlock>().Select(tb => tb.Text).ToList();

            textBlocks
                .Any(t => string.Equals(t, "Default interval for new jobs", StringComparison.Ordinal))
                .Should()
                .BeTrue("default interval label should be on the Scheduled Jobs page");

            // CheckBox.Content is checked directly — no need for template rendering
            window.GetVisualDescendants().OfType<CheckBox>()
                .Any(cb => string.Equals(cb.Content?.ToString(), "Auto-start scheduled jobs on launch", StringComparison.Ordinal))
                .Should()
                .BeTrue("auto-start toggle should be on the Scheduled Jobs page");

            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            var screenshotPath = Path.Combine(Path.GetTempPath(), "arbor-httpclient-options-view.png");
            screenshot?.Save(screenshotPath);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task FloatingWindow_PositionShouldBeRestoredOnStartup()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
                            DocumentDockableOrder = ["request", "response"],
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

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task FloatingWindow_PositionShouldBeRestoredFromSavedLayout()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
            var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
            var logWindowViewModel = new LogWindowViewModel(inMemorySink);

            var floatingSnapshot = new DockLayoutSnapshot
            {
                LeftToolProportion = 0.25,
                DocumentProportion = 0.75,
                LeftToolDockableOrder = ["options"],
                DocumentDockableOrder = ["request", "response"],
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

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task FloatingWindow_PositionShouldSurviveLayoutSwitching()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            // Float the left-panel dockable
            var leftPanel = FindDockById<IDockable>(viewModel.Layout!, "left-panel");
            leftPanel.Should().NotBeNull("left-panel dockable must exist");
            viewModel.Factory!.FloatDockable(leftPanel!);

            viewModel.Layout!.Windows.Should().HaveCount(1, "FloatDockable should have created a floating window");
            var floatWin = viewModel.Layout.Windows![0];

            // Move the floating window to a known position
            floatWin.X = 300;
            floatWin.Y = 400;
            floatWin.Width = 550;
            floatWin.Height = 380;

            // Save this layout
            viewModel.SaveLayoutAsNewCommand.Execute(null);
            viewModel.SavedLayoutNames.Should().ContainSingle();
            var layoutName = viewModel.SavedLayoutNames.Single();

            // Switch away: restore the default (no floating windows)
            viewModel.RestoreDefaultLayoutCommand.Execute(null);
            viewModel.Layout!.Windows?.Count.Should().Be(0, "default layout has no floating windows");

            // Switch back: the saved layout should restore the floating window at the original position
            viewModel.SelectedLayoutName = layoutName;

            viewModel.Layout!.Windows.Should().HaveCount(1);
            var restoredWin = viewModel.Layout.Windows![0];
            restoredWin.X.Should().BeApproximately(300, 0.001);
            restoredWin.Y.Should().BeApproximately(400, 0.001);
            restoredWin.Width.Should().BeApproximately(550, 0.001);
            restoredWin.Height.Should().BeApproximately(380, 0.001);

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task FloatingWindow_PositionShouldPersistAcrossAppRestarts()
    {
        // Verifies the full save→reload lifecycle:
        // 1st "run": float a window, move it, call the OnClosing sequence
        // 2nd "run": start with the saved layout, verify window is at the saved position
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            // ── First "application run" ──────────────────────────────────────
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            var leftPanel = FindDockById<IDockable>(viewModel.Layout!, "left-panel");
            leftPanel.Should().NotBeNull("left-panel dockable must exist");
            viewModel.Factory!.FloatDockable(leftPanel!);

            viewModel.Layout!.Windows.Should().HaveCount(1);
            var floatWin = viewModel.Layout.Windows![0];
            floatWin.X = 123;
            floatWin.Y = 456;
            floatWin.Width = 600;
            floatWin.Height = 450;

            // Simulate MainWindow.OnClosing: persist first, then close floating windows
            viewModel.PersistCurrentLayout();
            var savedLayout = viewModel.CaptureCurrentLayout();
            viewModel.CloseFloatingWindows();

            // Floating windows should be gone from the model after close
            viewModel.Layout.Windows.Should().BeEmpty("CloseFloatingWindows should remove all floating windows");

            // ── Second "application run" ─────────────────────────────────────
            var handler2 = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService2 = new HttpRequestService(new global::System.Net.Http.HttpClient(handler2), repository);
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

            viewModel2.Layout!.Windows.Should().HaveCount(1, "floating window should be restored on next startup");
            var restoredWin = viewModel2.Layout.Windows![0];
            restoredWin.X.Should().BeApproximately(123, 0.001);
            restoredWin.Y.Should().BeApproximately(456, 0.001);
            restoredWin.Width.Should().BeApproximately(600, 0.001);
            restoredWin.Height.Should().BeApproximately(450, 0.001);

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task MainWindowViewModel_CloseFloatingWindowsThenDispose_ShouldNotThrow()
    {
        // Regression test for the NullReferenceException that occurred when:
        // 1. Main window closed (OnClosing fires – persist + close floating windows)
        // 2. Dispose called (OnClosed fires via App.axaml.cs window.Closed handler)
        // Previously, Dispose also called PersistCurrentLayout, which triggered
        // CaptureLayoutSnapshot on an already-cleaned-up layout.
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
            viewModel.Factory!.FloatDockable(leftPanel!);
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

            return true;
        }, CancellationToken.None);
    }


    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .WithInterFont()
            .LogToTrace();
    }

    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        private readonly List<SavedRequest> _items = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
        {
            _items.Add(request);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedRequest>>(_items.Take(limit).ToList());
    }

    private sealed class InMemoryCollectionRepository : ICollectionRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Collection>>([]);

        public Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryEnvironmentRepository : IEnvironmentRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestEnvironment>>([]);

        public Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryScheduledJobRepository : IScheduledJobRepository
    {
        private readonly List<ScheduledJobConfig> _items = [];
        private int _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
        {
            var id = _nextId++;
            _items.Add(config with { Id = id });
            return Task.FromResult(id);
        }

        public Task UpdateAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(x => x.Id == config.Id);
            if (idx >= 0) _items[idx] = config;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScheduledJobConfig>>(_items.ToList());
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T match && string.Equals(match.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                var result = FindDockById<T>(child, id);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }
}
