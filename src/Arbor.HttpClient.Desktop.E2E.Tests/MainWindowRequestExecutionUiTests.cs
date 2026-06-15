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
public class MainWindowRequestExecutionUiTests
{
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

        viewModel.Response.ResponseBody = "{\"previous\":true}";
        viewModel.Response.RawResponseBody = "{\"previous\":true}";
        viewModel.Response.ResponseStatus = "200 OK";
        viewModel.ResponseHeaders.Add("Content-Type: application/json");
        viewModel.Response.HasResponseHeaders = true;
        viewModel.Response.HasTextResponse = true;

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
}
