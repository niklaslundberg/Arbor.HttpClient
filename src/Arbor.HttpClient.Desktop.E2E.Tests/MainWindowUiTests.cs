using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task OpenLogWindowCommand_ShouldActivateDockableLogPanel()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            leftToolDock.Should().NotBeNull();
            leftToolDock!.VisibleDockables!.Should().Contain(d => d.Id == "log-panel");

            viewModel.OpenLogWindowCommand.Execute(null);

            leftToolDock.ActiveDockable?.Id.Should().Be("log-panel");
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task OpenEnvironmentsCommand_ShouldActivateDockableEnvironmentsPanel()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            leftToolDock.Should().NotBeNull();
            leftToolDock!.VisibleDockables!.Should().Contain(d => d.Id == "environments");

            viewModel.OpenEnvironmentsCommand.Execute(null);

            leftToolDock.ActiveDockable?.Id.Should().Be("environments");
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
                "http://localhost:5000/job",
                null,
                null,
                60,
                true));

            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler = new StubHttpMessageHandler(_ =>
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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

            viewModel.RequestEditor.RequestUrl = "http://localhost:5000/items?first=1&second=2#keep";
            viewModel.RequestEditor.RequestQueryParameters.Should().HaveCount(2);
            viewModel.RequestEditor.RequestQueryParameters[0].Key.Should().Be("first");
            viewModel.RequestEditor.RequestQueryParameters[0].Value.Should().Be("1");
            viewModel.RequestEditor.RequestQueryParameters[1].Key.Should().Be("second");
            viewModel.RequestEditor.RequestQueryParameters[1].Value.Should().Be("2");

            viewModel.RequestEditor.RequestQueryParameters[0].IsEnabled = false;
            viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/items?second=2#keep");

            viewModel.RequestEditor.AddQueryParameterCommand.Execute(null);
            var added = viewModel.RequestEditor.RequestQueryParameters.Last();
            added.Key = "third";
            added.Value = "3";

            viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/items?second=2&third=3#keep");

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
            var handler = new StubHttpMessageHandler(_ =>
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
                logWindowViewModel);

            viewModel.RequestEditor.RequestName = "response test";
            viewModel.RequestEditor.RequestUrl = "http://localhost:5000/data";
            viewModel.RequestEditor.SelectedMethod = "GET";

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
    public async Task SendRequestAndPreview_ShouldResolveVariables_InUrlHeadersAndBody()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
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

            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            capturedUri.Should().NotBeNull();
            capturedUri!.AbsoluteUri.Should().Be("http://localhost:5000/api?search=term");
            capturedHeaderValue.Should().Be("blue");
            capturedBody.Should().Be("{\"token\":\"abc123\",\"env\":\"dev\"}");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task SendRequestAndPreview_ShouldApplyAuthHelperAuthorizationHeader()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
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

            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            capturedAuthorization.Should().Be("Bearer abc123");
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RequestUrlEditor_AutocompleteShouldInsertFilteredEnvironmentVariable()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
            requestUrlEditor!.Text = string.Empty;
            requestUrlEditor.CaretOffset = 0;

            requestUrlEditor.TextArea.PerformTextInput("{");
            requestUrlEditor.TextArea.PerformTextInput("{");
            requestUrlEditor.TextArea.PerformTextInput("t");
            requestUrlEditor.TextArea.PerformTextInput("o");

            var controller = requestView.RequestUrlAutoCompleteControllerForTests;
            controller.Should().NotBeNull();
            var completionWindow = controller!.CurrentCompletionWindow;
            completionWindow.Should().NotBeNull();
            completionWindow!.IsOpen.Should().BeTrue();
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            optionsVm.SelectedOptionsPage = "ScheduledJobs";

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
    public async Task OptionsChanges_ShouldAutoSaveAndLogToDebug()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
            var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
            var logWindowViewModel = new LogWindowViewModel(inMemorySink);
            var optionsPath = Path.Combine(Path.GetTempPath(), $"arbor-options-autosave-{Guid.NewGuid():N}.json");
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

            await Task.Delay(1200);

            var saved = optionsStore.Load();
            saved.Http.TlsVersion.Should().Be("Tls13");
            inMemorySink.GetSnapshot().Should().Contain(entry => entry.Message.Contains("Saved application options", StringComparison.Ordinal));

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task EnvironmentEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var environmentRepository = new InMemoryEnvironmentRepository();
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            await Task.Delay(1200);

            var all = await environmentRepository.GetAllAsync();
            all.Should().ContainSingle(e => e.Name == "myenv");
            viewModel.ActiveEnvironment.Should().NotBeNull();
            viewModel.ActiveEnvironment!.Name.Should().Be("myenv");
            viewModel.IsEnvironmentPanelVisible.Should().BeTrue();

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ScheduledJobEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var scheduledJobRepository = new InMemoryScheduledJobRepository();
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
                logWindowViewModel);

            viewModel.AddScheduledJobCommand.Execute(null);
            var job = viewModel.ScheduledJobs.Should().ContainSingle().Subject;
            job.Name = "sync job";
            job.Url = "http://localhost:5000/sync";

            await Task.Delay(1200);

            var all = await scheduledJobRepository.GetAllAsync();
            all.Should().ContainSingle(config =>
                string.Equals(config.Name, "sync job", StringComparison.Ordinal) &&
                string.Equals(config.Url, "http://localhost:5000/sync", StringComparison.Ordinal));

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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
    public async Task SaveNewEnvironment_ShouldAutoActivateItAndResolveVariablesInPreviewAndSend()
    {
        // Regression test: after saving a brand-new environment (ActiveEnvironment was null),
        // SaveEnvironmentAsync must automatically set ActiveEnvironment to the created entry
        // so that variable tokens in the URL/body/headers resolve immediately without the user
        // having to manually select the environment from the dropdown.
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            Uri? capturedUri = null;
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(req =>
            {
                capturedUri = req.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var environmentRepository = new InMemoryEnvironmentRepository();
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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

            viewModel.RequestEditor.RequestUrl = "http://{{host}}/api";
            viewModel.RequestEditor.SelectedMethod = "GET";

            // Simulate "+ New Environment" → fills in name and a variable
            viewModel.NewEnvironmentCommand.Execute(null);
            viewModel.NewEnvironmentName = "myenv";
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost:5000"));

            // Save – this is the path where ActiveEnvironment was previously never restored
            await viewModel.SaveEnvironmentCommand.ExecuteAsync(null);

            // After save, ActiveEnvironment should be auto-selected to the newly created env
            viewModel.ActiveEnvironment.Should().NotBeNull("SaveEnvironmentAsync must auto-select the new environment");
            viewModel.ActiveEnvironment!.Name.Should().Be("myenv");

            // Variables should now be reflected in the preview
            viewModel.RequestEditor.RequestPreview.Should().Contain("http://localhost:5000/api",
                "{{host}} should be resolved to 'localhost:5000' in the preview");

            // And the actual request should also resolve variables
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            capturedUri.Should().NotBeNull();
            capturedUri!.AbsoluteUri.Should().Be("http://localhost:5000/api",
                "{{host}} must be resolved to 'localhost:5000' when the request is sent");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task DisabledEnvironmentVariable_ShouldNotResolveInPreviewOrSend()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            Uri? capturedUri = null;
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(req =>
            {
                capturedUri = req.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK);
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
                logWindowViewModel);

            viewModel.RequestEditor.RequestUrl = "http://{{host}}/api";
            viewModel.RequestEditor.SelectedMethod = "GET";

            viewModel.NewEnvironmentCommand.Execute(null);
            viewModel.NewEnvironmentName = "myenv";
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost:5000", false));

            await viewModel.SaveEnvironmentCommand.ExecuteAsync(null);

            viewModel.RequestEditor.RequestPreview.Should().NotContain("localhost:5000");

            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            capturedUri.Should().BeNull("request should not be sent when a disabled variable leaves an invalid URL");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task AddEnvironmentVariable_ShouldKeepDraftRowWhileEditingEnvironment()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
            var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
            var logWindowViewModel = new LogWindowViewModel(inMemorySink);
            var environmentRepository = new InMemoryEnvironmentRepository();
            var environmentId = await environmentRepository.SaveAsync("dev",
            [
                new EnvironmentVariable("host", "localhost")
            ]);

            using var viewModel = new MainWindowViewModel(
                httpRequestService,
                repository,
                new InMemoryCollectionRepository(),
                environmentRepository,
                new InMemoryScheduledJobRepository(),
                scheduledJobService,
                logWindowViewModel);

            await viewModel.InitializeAsync();

            var environment = viewModel.Environments.Single(e => e.Id == environmentId);
            viewModel.EditEnvironmentCommand.Execute(environment);
            viewModel.ActiveEnvironmentVariables.Should().HaveCount(1);

            viewModel.AddEnvironmentVariableCommand.Execute(null);
            viewModel.ActiveEnvironmentVariables.Should().HaveCount(2);

            await Task.Delay(700);

            viewModel.ActiveEnvironmentVariables.Should().HaveCount(2,
                "a new blank variable row should remain visible while the user is still editing it");
            viewModel.ActiveEnvironmentVariables.Last().Name.Should().BeEmpty();

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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler2 = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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

    [Fact]
    public async Task SendRequest_ShouldRespectFollowRedirectsOverride()
    {
        using var server = new RedirectTestServer();
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            using var defaultClient = new global::System.Net.Http.HttpClient();
            using var followClient = new global::System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true });
            using var noFollowClient = new global::System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
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
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;
            viewModel.ResponseStatus.Should().StartWith("302");

            viewModel.RequestEditor.FollowRedirectsForRequest = true;
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;
            viewModel.ResponseStatus.Should().Be("200 OK");
            viewModel.RawResponseBody.Should().Contain("redirect-complete");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ScheduledJob_ShouldRespectFollowRedirectsOverride()
    {
        using var server = new RedirectTestServer();
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            using var defaultClient = new global::System.Net.Http.HttpClient();
            using var followClient = new global::System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true });
            using var noFollowClient = new global::System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
            var httpRequestService = new HttpRequestService(defaultClient, repository);
            httpRequestService.SetHttpClientFactory(followRedirects =>
                (followRedirects ?? true) ? followClient : noFollowClient);

            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
            using var scheduledJobService = new ScheduledJobService(httpRequestService, logger);

            var noFollowId = 1;
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

            await Task.Delay(1300);
            scheduledJobService.Stop(noFollowId);
            server.FinalRequestCount.Should().Be(0);

            var followId = 2;
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

            await Task.Delay(1300);
            scheduledJobService.Stop(followId);
            server.FinalRequestCount.Should().BeGreaterThan(0);

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
            _cts.Cancel();
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

            _cts.Dispose();
        }
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T match && string.Equals(match.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } visibleDockables)
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
}
